using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorGame
{
    /// <summary>
    /// A self-contained unlock helper added to the Player at runtime when a cutscene ends.
    /// Runs entirely on the Player GameObject so it cannot be killed if CutsceneController
    /// gets deactivated by the Timeline system.
    ///
    /// KEY FIX: Rotation fields and camera swap happen ONLY on the FIRST call.
    /// Subsequent calls only re-enable scripts and unlock movement without touching rotation,
    /// so the player's mouse input is not overwritten every frame.
    /// </summary>
    public class CutsceneUnlockHelper : MonoBehaviour
    {
        // ─── Configuration set by AttachTo ───────────────────────────────────
        private float     _targetYaw;
        private Camera    _playerMainCamera;
        private Camera    _cutsceneCamera;
        private float     _unlockDuration = 1.5f;

        // ─── Cached components ───────────────────────────────────────────────
        private Movement         _movement;
        private MouseLook        _mouseLook;
        private CharacterController _cc;
        private PlayerInteraction   _interaction;

        // ─── State ───────────────────────────────────────────────────────────
        // Rotation reset and camera swap must only fire ONCE.
        // If they fire every frame they override the player's mouse input and freeze the camera.
        private bool _rotationResetDone = false;

        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Attach (or replace) a CutsceneUnlockHelper on the player GameObject.
        /// Safe to call multiple times; old instances are removed first.
        /// </summary>
        public static void AttachTo(
            GameObject player,
            float      targetYaw,
            Camera     playerMainCamera,
            Camera     cutsceneCamera,
            float      unlockDuration = 1.5f)
        {
            if (player == null) return;

            // Remove any stale helper so only one runs at a time
            CutsceneUnlockHelper existing = player.GetComponent<CutsceneUnlockHelper>();
            if (existing != null)
            {
                existing.StopAllCoroutines();
                Destroy(existing);
            }

            CutsceneUnlockHelper helper = player.AddComponent<CutsceneUnlockHelper>();
            helper._targetYaw        = targetYaw;
            helper._playerMainCamera = playerMainCamera;
            helper._cutsceneCamera   = cutsceneCamera;
            helper._unlockDuration   = unlockDuration;

            Debug.Log("[CutsceneUnlockHelper] Attached to Player. Starting persistent unlock loop.");
        }

        // ─────────────────────────────────────────────────────────────────────

        private void Start()
        {
            _movement    = GetComponent<Movement>();
            _cc          = GetComponent<CharacterController>();
            _mouseLook   = GetComponentInChildren<MouseLook>(true);
            _interaction = GetComponentInChildren<PlayerInteraction>(true);

            StartCoroutine(PersistentUnlockLoop());
        }

        private IEnumerator PersistentUnlockLoop()
        {
            float elapsed = 0f;

            while (elapsed < _unlockDuration)
            {
                elapsed += Time.deltaTime;
                RestoreControl();
                yield return null;
            }

            Debug.Log("[CutsceneUnlockHelper] Unlock loop complete. Player control fully restored.");
            Destroy(this);
        }

        // ─────────────────────────────────────────────────────────────────────

        private void RestoreControl()
        {
            // ── 1. Re-enable Movement and clear lock/hide state ───────────────
            if (_movement != null)
            {
                _movement.enabled = true;

                // Fix null camera reference if it was cleared by cutscene
                if (_movement.playerCamera == null)
                {
                    Camera cam = GetComponentInChildren<Camera>(true);
                    if (cam != null) _movement.playerCamera = cam.transform;
                }

                _movement.SetMovementLock(false);
                _movement.SetHiding(false);
            }

            // ── 2. Re-enable CharacterController ─────────────────────────────
            if (_cc != null) _cc.enabled = true;

            // ── 3. Re-enable MouseLook and its InputAction ────────────────────
            if (_mouseLook != null)
            {
                _mouseLook.enabled = true;

                try
                {
                    var field = typeof(MouseLook).GetField(
                        "lookAction",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

                    if (field != null)
                    {
                        InputAction lookAct = (InputAction)field.GetValue(_mouseLook);
                        if (lookAct != null) lookAct.Enable();
                    }
                }
                catch { /* reflection best-effort */ }
            }

            // ── 4. Re-enable PlayerInteraction ────────────────────────────────
            if (_interaction != null) _interaction.enabled = true;

            // ── 5. Restore cursor lock ────────────────────────────────────────
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;

            // ── 6–8  ONE-TIME ONLY: rotation reset + camera swap ─────────────
            // These must NOT repeat every frame — doing so overwrites the player's
            // accumulated mouse rotation and makes the camera appear completely frozen.
            if (!_rotationResetDone)
            {
                _rotationResetDone = true;

                // 6. Point camera horizontally (eye-level)
                if (_playerMainCamera != null)
                    _playerMainCamera.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

                if (_mouseLook != null)
                    _mouseLook.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

                // 7. Sync internal rotation fields of Movement & MouseLook so
                //    their first Update() starts from the correct facing direction
                //    instead of snapping back to wherever they were during the cutscene.
                SetRotationFields(_movement,  _targetYaw);
                SetRotationFields(_mouseLook, _targetYaw);

                // 8. Restore player camera, turn off cutscene camera
                if (_playerMainCamera != null)
                {
                    _playerMainCamera.gameObject.SetActive(true);
                    _playerMainCamera.enabled = true;
                }
                if (_cutsceneCamera != null)
                {
                    _cutsceneCamera.enabled = false;
                    _cutsceneCamera.gameObject.SetActive(false);
                }

                Debug.Log($"[CutsceneUnlockHelper] One-time rotation reset applied. TargetYaw={_targetYaw}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────

        private void SetRotationFields(MonoBehaviour script, float yaw)
        {
            if (script == null) return;
            try
            {
                var type = script.GetType();
                var xf = type.GetField("xRotation",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                var yf = type.GetField("yRotation",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                if (xf != null) xf.SetValue(script, 0f);
                if (yf != null) yf.SetValue(script, yaw);
            }
            catch { }
        }
    }
}
