using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace HorrorGame
{
    /// <summary>
    /// PlayerHands — มือ First-Person ของตัวละคร
    ///
    /// ใช้ driven animation ด้วย code (ไม่ต้องการ Animator):
    ///  - Idle   : มือนิ่งๆ พร้อม sway เล็กน้อย
    ///  - Walk   : มือเด้งซ้ายขวาตาม head bob
    ///  - Run    : มือเด้งแรงขึ้น + เอนไปข้างหน้า
    ///  - Crouch : มือลดต่ำลง
    ///  - Pickup : มือยกขึ้นหยิบของ (เล่นผ่าน TriggerPickup())
    ///
    /// Setup:
    ///   1. สร้าง GameObject ลูกของ Camera ตั้งชื่อ "HandRoot"
    ///   2. วางโมเดลมือเป็นลูกของ HandRoot
    ///   3. วาง Script นี้ที่ Player
    ///   4. ลาก HandRoot ใส่ช่อง handRoot
    ///   5. ลาก Movement ใส่ช่อง movement
    /// </summary>
    public class PlayerHands : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Transform ของ HandRoot (ลูกของ Camera)")]
        public Transform handRoot;

        [Tooltip("Movement script ของผู้เล่น")]
        public Movement movement;

        [Tooltip("CharacterController ของผู้เล่น")]
        public CharacterController characterController;

        // ─── Idle Sway ─────────────────────────────
        [Header("Idle Sway")]
        public float idleSwayAmplitude = 0.006f;
        public float idleSwayFrequency = 0.8f;

        // ─── Walk Bob ──────────────────────────────
        [Header("Walk Bob")]
        public float walkBobAmplitudeX  = 0.05f;
        public float walkBobAmplitudeY  = 0.04f;
        public float walkBobFrequency   = 1.8f;

        public float runBobAmplitudeX   = 0.09f;
        public float runBobAmplitudeY   = 0.07f;
        public float runBobFrequency    = 2.8f;

        // ─── Positions ─────────────────────────────
        [Header("Hand Positions")]
        [Tooltip("ตำแหน่ง local เริ่มต้น (Idle)")]
        public Vector3 idlePosition     = new Vector3(0.25f, -0.3f, 0.5f);

        [Tooltip("ตำแหน่งตอน Crouch (ลดต่ำลง)")]
        public Vector3 crouchPosition   = new Vector3(0.25f, -0.4f, 0.45f);

        [Tooltip("ตำแหน่งตอนวิ่ง (เอนมาข้างหน้า)")]
        public Vector3 runPosition      = new Vector3(0.2f, -0.28f, 0.55f);

        [Tooltip("ตำแหน่งตอน Pickup (ยกขึ้น)")]
        public Vector3 pickupPosition   = new Vector3(0.25f, -0.1f, 0.4f);

        // ─── Smoothing ─────────────────────────────
        [Header("Smoothing")]
        public float positionSmoothSpeed = 8f;
        public float bobSmoothSpeed      = 6f;

        // ─── Mouse Sway ────────────────────────────
        [Header("Mouse Sway (เมื่อหมุนกล้อง)")]
        [Tooltip("มือแกว่งตามการหมุนเมาส์")]
        public float mouseSwayAmount = 0.02f;
        public float mouseSwaySmooth = 5f;

        // ─── Private ───────────────────────────────
        private Vector3 _bobOffset;
        private Vector3 _swayOffset;
        private Vector3 _mouseSwayOffset;
        private float   _bobTimer   = 0f;
        private float   _idleTimer  = 0f;
        private bool    _isPickingUp = false;
        private Vector3 _targetPosition;
        private Vector2 _lastMouseDelta;

        void Start()
        {
            if (movement == null)
                movement = GetComponentInChildren<Movement>();
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (handRoot != null)
                handRoot.localPosition = idlePosition;

            _targetPosition = idlePosition;
        }

        void Update()
        {
            if (handRoot == null) return;
            if (_isPickingUp) return;

            DetectState(out bool isMoving, out bool isRunning, out bool isCrouching);

            UpdateMouseSway();
            UpdateBob(isMoving, isRunning, isCrouching);
            UpdateIdleSway(isMoving);
            UpdateTargetPosition(isMoving, isRunning, isCrouching);
            ApplyAll();
        }

        // ──────────────────────────────────────────
        //  State
        // ──────────────────────────────────────────
        void DetectState(out bool isMoving, out bool isRunning, out bool isCrouching)
        {
            if (characterController == null)
            {
                isMoving = isRunning = isCrouching = false;
                return;
            }
            Vector3 vel = characterController.velocity;
            vel.y       = 0;
            float speed = vel.magnitude;
            isMoving    = speed > 0.3f;
            isRunning   = speed > 4f;
            isCrouching = speed > 0.1f && speed < 2f;
        }

        // ──────────────────────────────────────────
        //  Mouse Sway
        // ──────────────────────────────────────────
        void UpdateMouseSway()
        {
            Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            Vector3 targetSway = new Vector3(-mouseDelta.y * mouseSwayAmount,
                                              mouseDelta.x * mouseSwayAmount, 0f) * 0.01f;
            _mouseSwayOffset = Vector3.Lerp(_mouseSwayOffset, targetSway, Time.deltaTime * mouseSwaySmooth);
        }

        // ──────────────────────────────────────────
        //  Walk Bob
        // ──────────────────────────────────────────
        void UpdateBob(bool isMoving, bool isRunning, bool isCrouching)
        {
            if (!isMoving)
            {
                _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, Time.deltaTime * bobSmoothSpeed);
                return;
            }

            float freq = isRunning ? runBobFrequency : walkBobFrequency;
            float ampX = isRunning ? runBobAmplitudeX : walkBobAmplitudeX;
            float ampY = isRunning ? runBobAmplitudeY : walkBobAmplitudeY;

            _bobTimer += Time.deltaTime * freq * Mathf.PI * 2f;

            Vector3 target = new Vector3(
                Mathf.Cos(_bobTimer) * ampX,
                Mathf.Abs(Mathf.Sin(_bobTimer)) * ampY,  // เฉพาะ sine บวก = เด้งขึ้น
                0f
            );

            _bobOffset = Vector3.Lerp(_bobOffset, target, Time.deltaTime * bobSmoothSpeed);
        }

        // ──────────────────────────────────────────
        //  Idle Sway
        // ──────────────────────────────────────────
        void UpdateIdleSway(bool isMoving)
        {
            _idleTimer += Time.deltaTime * idleSwayFrequency * Mathf.PI * 2f;
            float swayWeight = isMoving ? 0f : 1f;
            Vector3 target = new Vector3(
                Mathf.Sin(_idleTimer * 0.6f) * idleSwayAmplitude,
                Mathf.Sin(_idleTimer) * idleSwayAmplitude * 0.5f,
                0f
            ) * swayWeight;
            _swayOffset = Vector3.Lerp(_swayOffset, target, Time.deltaTime * 4f);
        }

        // ──────────────────────────────────────────
        //  Target Position
        // ──────────────────────────────────────────
        void UpdateTargetPosition(bool isMoving, bool isRunning, bool isCrouching)
        {
            if (isRunning)           _targetPosition = runPosition;
            else if (isCrouching)    _targetPosition = crouchPosition;
            else                     _targetPosition = idlePosition;
        }

        // ──────────────────────────────────────────
        //  Apply
        // ──────────────────────────────────────────
        void ApplyAll()
        {
            Vector3 finalPos = _targetPosition + _bobOffset + _swayOffset + _mouseSwayOffset;
            handRoot.localPosition = Vector3.Lerp(handRoot.localPosition, finalPos,
                                                   Time.deltaTime * positionSmoothSpeed);
        }

        // ──────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────

        /// <summary>
        /// เรียกจาก PlayerInteraction ตอนกด E หยิบของ
        /// มือจะยกขึ้น → กลับสู่ idle
        /// </summary>
        public void TriggerPickup()
        {
            StartCoroutine(PickupAnimation());
        }

        IEnumerator PickupAnimation()
        {
            _isPickingUp = true;

            // ยกขึ้น
            float t = 0f;
            Vector3 start = handRoot.localPosition;
            while (t < 1f)
            {
                t += Time.deltaTime * 5f;
                handRoot.localPosition = Vector3.Lerp(start, pickupPosition, t);
                yield return null;
            }

            yield return new WaitForSeconds(0.15f);

            // ลงกลับ
            t = 0f;
            start = handRoot.localPosition;
            while (t < 1f)
            {
                t += Time.deltaTime * 4f;
                handRoot.localPosition = Vector3.Lerp(start, idlePosition, t);
                yield return null;
            }

            _isPickingUp = false;
        }

        void OnDrawGizmosSelected()
        {
            // แสดง idle position ใน Scene view
            if (handRoot == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(handRoot.parent != null
                ? handRoot.parent.TransformPoint(idlePosition)
                : transform.position + idlePosition, 0.02f);
        }
    }
}
