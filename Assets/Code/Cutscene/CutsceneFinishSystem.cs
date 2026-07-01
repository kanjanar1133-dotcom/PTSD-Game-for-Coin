using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.InputSystem;

namespace HorrorGame
{
    /// <summary>
    /// A completely independent system that handles player locking, teleporting, rotation matching,
    /// and GameObject activation/deactivation at the end of a cutscene without modifying existing code.
    /// </summary>
    public class CutsceneFinishSystem : MonoBehaviour
    {
        [Header("Timeline Settings")]
        [Tooltip("The PlayableDirector that plays the cutscene.")]
        public PlayableDirector playableDirector;

        [Header("Player Settings")]
        [Tooltip("The main Player GameObject.")]
        public GameObject player;

        [Tooltip("The First Person Camera attached to the Player.")]
        public Camera firstPersonCamera;

        [Header("Target Location Settings")]
        [Tooltip("The transform representing the destination and facing direction of the player after the cutscene ends.")]
        public Transform playerEndPoint;

        [Tooltip("If true, the camera will copy the X-axis tilt (pitch) of the PlayerEndPoint. If false, it will look straight ahead (horizontal).")]
        public bool useEndPointPitch = false;

        [Header("GameObject Activation Settings")]
        [Tooltip("GameObjects to ACTIVATE when the cutscene ends (e.g., Player UI, default hud).")]
        public GameObject[] objectsToActivate;

        [Tooltip("GameObjects to DEACTIVATE when the cutscene ends (e.g., Cutscene cameras, Cinemachine Virtual Cameras). IMPORTANT: Deactivating the cutscene camera/virtual camera stops camera shaking and releases control back to the player.")]
        public GameObject[] objectsToDeactivate;

        private void OnEnable()
        {
            if (playableDirector != null)
            {
                playableDirector.played += OnTimelineStarted;
                playableDirector.stopped += OnTimelineStopped;
            }
        }

        private void OnDisable()
        {
            if (playableDirector != null)
            {
                playableDirector.played -= OnTimelineStarted;
                playableDirector.stopped -= OnTimelineStopped;
            }
        }

        private void OnTimelineStarted(PlayableDirector director)
        {
            if (director != playableDirector) return;

            Debug.Log("[CutsceneFinishSystem] Timeline started. Locking player input.");
            SetPlayerInputState(false);
        }

        private void OnTimelineStopped(PlayableDirector director)
        {
            if (director != playableDirector) return;

            Debug.Log("[CutsceneFinishSystem] Timeline stopped. Executing teleport, rotation, and object state changes.");

            // ── Release Timeline Hold mode ──────────────────────────────────────
            // When a PlayableDirector has extrapolationMode = Hold, it continues to
            // evaluate its tracks every LateUpdate even after stopping.
            // This silently resets the player's position/rotation every frame,
            // making it look like the player cannot move even though all scripts
            // report "enabled" and "not locked".
            // Setting None + disabling the component fully releases all track overrides.
            if (playableDirector != null)
            {
                playableDirector.extrapolationMode = UnityEngine.Playables.DirectorWrapMode.None;
                playableDirector.enabled = false;
                Debug.Log("[CutsceneFinishSystem] PlayableDirector disabled — transform hold released.");
            }

            // ── Safety Net: Attach a persistent unlock helper directly on the Player
            // IMMEDIATELY (synchronous, no yield) so it runs even if the coroutine below
            // is cancelled because this GameObject gets deactivated by objectsToDeactivate.
            if (player != null)
            {
                GameObject resolvedPlayer = player;
                if (resolvedPlayer == null) resolvedPlayer = GameObject.FindGameObjectWithTag("Player");

                if (resolvedPlayer != null)
                {
                    float immediateYaw = (playerEndPoint != null)
                        ? playerEndPoint.rotation.eulerAngles.y
                        : resolvedPlayer.transform.eulerAngles.y;

                    Debug.Log($"[CutsceneFinishSystem] Attaching CutsceneUnlockHelper to Player. TargetYaw={immediateYaw}");
                    CutsceneUnlockHelper.AttachTo(
                        resolvedPlayer,
                        immediateYaw,
                        firstPersonCamera,
                        null,
                        unlockDuration: 2.0f);
                }
            }

            StartCoroutine(ExecuteTeleportAndUnlock());
        }

        private IEnumerator ExecuteTeleportAndUnlock()
        {
            // Wait 2 frames to ensure the Timeline and Cinemachine have finished updating their final states
            yield return null;
            yield return null;

            // 1. Deactivate cutscene cameras / virtual cameras to release Cinemachine camera overrides (stops screen shaking)
            if (objectsToDeactivate != null)
            {
                foreach (var obj in objectsToDeactivate)
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                        Debug.Log($"[CutsceneFinishSystem] Deactivated GameObject: {obj.name}");
                    }
                }
            }

            // 2. Activate any required gameplay HUDs / objects
            if (objectsToActivate != null)
            {
                foreach (var obj in objectsToActivate)
                {
                    if (obj != null)
                    {
                        obj.SetActive(true);
                        Debug.Log($"[CutsceneFinishSystem] Activated GameObject: {obj.name}");
                    }
                }
            }

            // Wait 1 more frame for Cinemachine brain to fully blend back to player's camera after deactivation
            yield return null;

            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) player = GameObject.Find("Player");
            }

            if (player == null || playerEndPoint == null)
            {
                Debug.LogError("[CutsceneFinishSystem] Player or PlayerEndPoint references are missing! Cannot complete teleport.");
                yield break;
            }

            // 3. Cache player components
            CharacterController cc = player.GetComponent<CharacterController>();
            Movement movement = player.GetComponent<Movement>();
            MouseLook mouseLook = player.GetComponentInChildren<MouseLook>(true);

            // 4. Temporarily disable CharacterController to apply position and rotation changes safely
            if (cc != null) cc.enabled = false;

            // 5. Teleport Player to PlayerEndPoint position
            player.transform.position = playerEndPoint.position;

            // 6. Calculate rotation angles
            float targetYaw = playerEndPoint.rotation.eulerAngles.y;
            float targetPitch = useEndPointPitch ? playerEndPoint.rotation.eulerAngles.x : 0f;
            
            // Normalize pitch angle for typical first-person camera clamps (-90 to 90)
            if (targetPitch > 180f) targetPitch -= 360f;

            // Rotate Player Transform (Y-yaw)
            player.transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);

            // Rotate First Person Camera (X-pitch)
            if (firstPersonCamera != null)
            {
                firstPersonCamera.transform.localRotation = Quaternion.Euler(targetPitch, 0f, 0f);
            }
            if (mouseLook != null)
            {
                mouseLook.transform.localRotation = Quaternion.Euler(targetPitch, 0f, 0f);
            }

            // Synchronize physics engine with manual transform changes
            Physics.SyncTransforms();

            // 7. Force reset internal script rotation variables to prevent the camera/player from snapping back
            SetPrivateRotationFields(movement, targetPitch, targetYaw);
            SetPrivateRotationFields(mouseLook, targetPitch, targetYaw);

            // 8. Re-enable CharacterController
            if (cc != null) cc.enabled = true;

            // 9. Re-enable Player Input
            SetPlayerInputState(true);

            // 10. Re-lock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log($"[CutsceneFinishSystem] Successfully teleported player to {playerEndPoint.position} and locked facing direction to Y: {targetYaw}, X: {targetPitch}.");
        }

        private void SetPlayerInputState(bool isEnabled)
        {
            if (player == null) return;

            Movement movement = player.GetComponent<Movement>();
            MouseLook mouseLook = player.GetComponentInChildren<MouseLook>(true);
            PlayerInteraction interaction = player.GetComponentInChildren<PlayerInteraction>(true);

            if (movement != null)
            {
                movement.enabled = isEnabled;
                movement.SetMovementLock(!isEnabled);
                movement.SetHiding(false);
            }

            if (mouseLook != null)
            {
                mouseLook.enabled = isEnabled;
                if (isEnabled)
                {
                    // Re-enable Input System Action if it was disabled by other systems
                    try
                    {
                        var lookActField = typeof(MouseLook).GetField("lookAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (lookActField != null)
                        {
                            InputAction lookAct = (InputAction)lookActField.GetValue(mouseLook);
                            if (lookAct != null) lookAct.Enable();
                        }
                    }
                    catch { }
                }
            }

            if (interaction != null)
            {
                interaction.enabled = isEnabled;
            }
        }

        private void SetPrivateRotationFields(MonoBehaviour script, float xRot, float yRot)
        {
            if (script == null) return;
            try
            {
                var type = script.GetType();
                var xf = type.GetField("xRotation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var yf = type.GetField("yRotation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (xf != null) xf.SetValue(script, xRot);
                if (yf != null) yf.SetValue(script, yRot);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CutsceneFinishSystem] Could not update rotation fields on {script.name}: {e.Message}");
            }
        }
    }
}
