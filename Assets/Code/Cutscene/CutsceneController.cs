using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.InputSystem;

namespace HorrorGame
{
    /// <summary>
    /// ควบคุมคัตซีน Timeline
    /// ใช้ Coroutine รันบนตัว Player โดยตรงเพื่อความปลอดภัยจากการโดน Deactivate โดย Timeline
    /// และทำการ Force Unlock ซ้ำๆ ทุกเฟรมเป็นเวลา 1.0 วินาที เพื่อเขียนทับการล็อคทั้งหมด
    /// </summary>
    public class CutsceneController : MonoBehaviour
    {
        [Header("Timeline Settings")]
        public PlayableDirector playableDirector;

        [Header("Player Reference")]
        [Tooltip("ลาก Player GameObject มาใส่โดยตรง")]
        public GameObject player;

        [Header("Camera Settings (Optional)")]
        [Tooltip("ปล่อยว่างหากใช้ Timeline จัดการกล้องเอง")]
        public Camera playerMainCamera;
        public Camera cutsceneCamera;

        [Header("Final Position Settings")]
        [Tooltip("Player จะถูกวาร์ปมาที่นี่ทันทีตอนเริ่มคัตซีน (หมุนแกน Y ตามวัตถุนี้)")]
        public Transform finalPositionTarget;

        [Header("Auto Play")]
        public bool playOnStart = false;

        private bool _isPlaying = false;
        private Movement _movement;
        private PlayerInteraction _interaction;
        private MouseLook _mouseLook;
        private CharacterController _cc;

        void Start()
        {
            if (player == null)
                player = GameObject.FindGameObjectWithTag("Player");

            CacheComponents();

            if (playableDirector != null)
                playableDirector.stopped += OnTimelineStopped;

            if (playOnStart)
                StartCutscene();
        }

        void OnDestroy()
        {
            if (playableDirector != null)
                playableDirector.stopped -= OnTimelineStopped;
        }

        private void CacheComponents()
        {
            if (player != null)
            {
                _movement = player.GetComponent<Movement>();
                _interaction = player.GetComponent<PlayerInteraction>();
                _cc = player.GetComponent<CharacterController>();
                _mouseLook = player.GetComponentInChildren<MouseLook>(true);
            }
        }

        // ─────────────────────────────
        public void StartCutscene()
        {
            if (_isPlaying) return;
            _isPlaying = true;

            Debug.Log("🎬 [CutsceneController] เริ่มคัตซีน");

            CacheComponents();

            // แก้ไขบั๊ก playerCamera ของสคริปต์ Movement เป็น null 
            if (_movement != null && _movement.playerCamera == null)
            {
                Camera mainCam = player.GetComponentInChildren<Camera>(true);
                if (mainCam != null) _movement.playerCamera = mainCam.transform;
            }

            // ─── Step 1: วาร์ป Player ไปจุดหมายทันที ───
            if (finalPositionTarget != null && _movement != null)
            {
                _movement.TeleportTo(finalPositionTarget.position, finalPositionTarget.rotation);
                Debug.Log($"🎬 [CutsceneController] วาร์ป Player ไป {player.transform.position}");
            }

            // ─── Step 2: Lock Player ───
            if (_movement != null)
                _movement.SetMovementLock(true);

            if (_interaction != null)
                _interaction.enabled = false;

            // ─── Step 3: สลับกล้อง (ถ้ากำหนดไว้) ───
            if (playerMainCamera != null)
            {
                playerMainCamera.enabled = false;
                playerMainCamera.gameObject.SetActive(false);
            }
            if (cutsceneCamera != null)
            {
                cutsceneCamera.gameObject.SetActive(true);
                cutsceneCamera.enabled = true;
            }

            // ─── Step 4: เล่น Timeline ───
            if (playableDirector != null)
                playableDirector.Play();
        }

        private void OnTimelineStopped(PlayableDirector director)
        {
            if (director != playableDirector) return;
            EndCutscene();
        }

        public void EndCutscene()
        {
            if (!_isPlaying) return;
            _isPlaying = false;

            Debug.Log("🎬 [CutsceneController] คัตซีนสิ้นสุด → กำลังเริ่มกระบวนการคืนการควบคุม...");

            // ดึงทิศทางการหันหน้าโดยตรงจาก finalPositionTarget (องศาการหมุน Y)
            float targetYaw = finalPositionTarget != null ? finalPositionTarget.rotation.eulerAngles.y : (player != null ? player.transform.rotation.eulerAngles.y : 0f);

            // ค้นหา Player
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) player = GameObject.Find("Player");
            }

            if (player != null)
            {
                _movement = player.GetComponent<Movement>();
                if (_movement != null)
                {
                    // เริ่มต้นรัน Coroutine ปลดล็อคบนตัวของ Player (สคริปต์ Movement) 
                    // ซึ่งจะปลอดภัยกว่า เพราะตัวละครไม่มีทางถูกปิดใช้งาน (Deactivate) ระหว่างสลับฉากจบ
                    _movement.StartCoroutine(ForceUnlockCoroutine(targetYaw));
                }
                else
                {
                    ForceUnlockImmediate(targetYaw);
                }
            }

            // ─── Persistent Safety Net ───────────────────────────────────────────
            // Attach a self-contained helper directly on the Player that hammers the
            // unlock every frame for 1.5 s, even if THIS GameObject is deactivated by
            // Timeline.  This is additive – it never removes or replaces existing code.
            if (player != null)
            {
                CutsceneUnlockHelper.AttachTo(
                    player,
                    targetYaw,
                    playerMainCamera,
                    cutsceneCamera,
                    unlockDuration: 1.5f);
            }
        }

        private IEnumerator ForceUnlockCoroutine(float targetYaw)
        {
            float elapsed = 0f;
            // ทำการปลดล็อคและเซ็ตค่าหันมุมกล้องอย่างต่อเนื่องเป็นเวลา 1.0 วินาที
            // เพื่อสู้และเอาชนะ Timeline/Cinemachine ในทุกเฟรมนับจากนี้
            while (elapsed < 1.0f)
            {
                elapsed += Time.deltaTime;
                ForceUnlockImmediate(targetYaw);
                yield return null;
            }
        }

        private void ForceUnlockImmediate(float targetYaw)
        {
            CacheComponents();

            if (player == null) return;

            // 1. บังคับกู้คืนความพร้อมใช้งานของสคริปต์หลักและตัวควบคุมฟิสิกส์ทั้งหมด
            if (_movement != null)
            {
                _movement.enabled = true;

                if (_movement.playerCamera == null)
                {
                    Camera mainCam = player.GetComponentInChildren<Camera>(true);
                    if (mainCam != null) _movement.playerCamera = mainCam.transform;
                }

                // เรียกฟังก์ชัน TeleportTo เพื่อควบคุมแกน Y และแก้ปัญหา CharacterController ดึงกล้อง
                _movement.TeleportTo(player.transform.position, Quaternion.Euler(0f, targetYaw, 0f));
                _movement.SetMovementLock(false);
                _movement.SetHiding(false);
            }
            else
            {
                if (_cc != null) _cc.enabled = false;
                player.transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
                Physics.SyncTransforms();
                if (_cc != null) _cc.enabled = true;
            }

            if (_cc != null) _cc.enabled = true;
            if (_mouseLook != null) _mouseLook.enabled = true;
            if (_interaction != null) _interaction.enabled = true;

            // 2. บังคับกู้คืน InputAction สำหรับเมาส์เดลต้า
            if (_mouseLook != null)
            {
                try
                {
                    var lookActField = typeof(MouseLook).GetField("lookAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (lookActField != null)
                    {
                        InputAction lookAct = (InputAction)lookActField.GetValue(_mouseLook);
                        if (lookAct != null) lookAct.Enable();
                    }
                }
                catch { }
            }

            // 3. ล็อคและซ่อน Cursor ใหม่
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // 4. บังคับทิศทางกล้องให้มองระดับสายตา (ตรงหน้า)
            if (playerMainCamera != null)
            {
                playerMainCamera.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            }
            if (_mouseLook != null)
            {
                _mouseLook.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            }

            ResetRotationField(_movement, targetYaw);
            ResetRotationField(_mouseLook, targetYaw);

            // 5. สลับกล้องคืน
            if (playerMainCamera != null)
            {
                playerMainCamera.gameObject.SetActive(true);
                playerMainCamera.enabled = true;
            }
            if (cutsceneCamera != null)
            {
                cutsceneCamera.enabled = false;
                cutsceneCamera.gameObject.SetActive(false);
            }
        }

        private void ResetRotationField(MonoBehaviour script, float yaw)
        {
            if (script == null) return;
            try
            {
                var type = script.GetType();
                var xf = type.GetField("xRotation",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var yf = type.GetField("yRotation",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (xf != null) xf.SetValue(script, 0f);
                if (yf != null) yf.SetValue(script, yaw);
            }
            catch { }
        }
    }
}
