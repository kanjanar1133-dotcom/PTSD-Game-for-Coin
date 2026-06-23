using UnityEngine;
using System.Collections.Generic;

namespace HorrorGame
{
    /// <summary>
    /// CameraEffects — FPS Head Bob + Stabilization (แบบ Unity Quick Guide)
    ///
    /// หลักการ:
    ///   • "Head" (playerCamera position) เด้งขึ้นลงตาม sine wave ตอนเดิน/วิ่ง
    ///   • "Camera" Stabilize โดยมองไปที่ world-space point ข้างหน้าคงที่
    ///     → ทำให้กล้องนิ่ง แม้หัวจะขยับ (ไม่เวียนหัว)
    ///   • Idle breathing, Landing dip, Camera shake ยังคงอยู่ครบ
    ///
    /// Setup:
    ///   1. วาง Script บน Player GameObject
    ///   2. ลาก Camera Transform ใส่ช่อง playerCamera
    ///   3. ลาก Movement ใส่ช่อง movement (หรือปล่อยให้หาเอง)
    ///
    /// เรียก Shake จาก script อื่น:
    ///   CameraEffects.Shake(0.6f, 0.4f);  // jumpscare
    ///   CameraEffects.Shake(0.3f, 0.2f);  // ประตูปิดดัง
    /// </summary>
    public class CameraEffects : MonoBehaviour
    {
        [Header("References")]
        public Transform playerCamera;
        public Movement  movement;

        // ─── Head Bob ──────────────────────────────────────────────
        [Header("Head Bob")]
        public bool enableHeadBob = true;

        [Tooltip("ความถี่การเด้งตอนเดิน")]
        public float walkBobFrequency   = 1.8f;
        public float runBobFrequency    = 2.8f;
        public float crouchBobFrequency = 1.2f;

        [Tooltip("ขนาดการเด้งแกน Y (ขึ้น-ลง)")]
        public float walkBobAmplitudeY   = 0.055f;
        public float runBobAmplitudeY    = 0.09f;
        public float crouchBobAmplitudeY = 0.025f;

        [Tooltip("ขนาดการแกว่งแกน X (ซ้าย-ขวา)")]
        public float walkBobAmplitudeX   = 0.028f;
        public float runBobAmplitudeX    = 0.05f;
        public float crouchBobAmplitudeX = 0.012f;

        // ─── Stabilization ─────────────────────────────────────────
        [Header("Stabilization")]
        [Tooltip("เปิด/ปิด stabilization (กล้องมองตรงแม้หัวจะขยับ)")]
        public bool enableStabilization = true;

        [Tooltip("ระยะข้างหน้าที่กล้องจะมองไปเพื่อ stabilize (m)")]
        public float stabilizationDistance = 10f;

        [Tooltip("ความเร็ว stabilization — สูง = นิ่งมาก, ต่ำ = ตามหัวมากขึ้น")]
        [Range(0f, 1f)]
        public float stabilizationAmount = 0.65f;

        // ─── Idle Breathing ────────────────────────────────────────
        [Header("Idle Breathing")]
        public bool  enableBreathing   = true;
        public float breathFrequency   = 0.3f;
        public float breathAmplitudeY  = 0.008f;
        public float breathAmplitudeX  = 0.004f;

        // ─── Landing Dip ───────────────────────────────────────────
        [Header("Landing Dip")]
        public bool  enableLandingDip = true;
        public float landingDipAmount = 0.06f;
        public float landingDipSpeed  = 8f;

        // ─── Smoothing ─────────────────────────────────────────────
        [Header("Smoothing")]
        public float transitionSpeed = 6f;

        // ─── Camera Shake (Event) ───────────────────────────────────
        [Header("Camera Shake (Event)")]
        [Tooltip("เปิด/ปิด shake ทั้งหมด")]
        public bool  enableShake             = true;
        public float shakeMaxPositionAmount  = 0.12f;
        public float shakeMaxRotationAmount  = 4f;
        public float shakeNoiseSpeed         = 14f;
        public float traumaDecayRate         = 1.8f;

        [Header("Ambient Horror Shake")]
        [Range(0f, 0.5f)]
        public float ambientTrauma    = 0.12f;
        public float ambientNoiseSpeed = 2.5f;

        // ─── Movement Shake ────────────────────────────────────────
        [Header("Movement Shake (เดิน/วิ่ง)")]
        [Tooltip("เปิด/ปิด shake ตอนเดิน/วิ่ง")]
        public bool enableMovementShake = true;

        [Tooltip("ขนาด position shake ตอนเดิน (m)")]
        public float walkShakePosition = 0.05f;

        [Tooltip("ขนาด rotation shake ตอนเดิน (องศา)")]
        public float walkShakeRotation = 1.8f;

        [Tooltip("ขนาด position shake ตอนวิ่ง (m) — ควรน้อยกว่า Shake() event")]
        public float runShakePosition  = 0.09f;

        [Tooltip("ขนาด rotation shake ตอนวิ่ง (องศา)")]
        public float runShakeRotation  = 3.2f;

        [Tooltip("ความถี่ Perlin noise ตอนเดิน/วิ่ง")]
        public float moveShakeNoiseSpeed = 11f;

        [Tooltip("ความเร็ว (m/s) ที่เริ่ม shake")]
        public float moveShakeMinSpeed  = 0.4f;

        [Tooltip("ความเร็ว (m/s) ที่ถือว่าวิ่งเต็มที่")]
        public float moveShakeMaxSpeed  = 6.5f;

        // ─── Private: State ────────────────────────────────────────
        private CharacterController _cc;
        private bool  _wasGrounded = true;
        private float _prevVelY    = 0f;
        private bool  _isMoving    = false;
        private bool  _isRunning   = false;
        private bool  _isCrouching = false;

        // ─── Private: Bob ──────────────────────────────────────────
        private float   _bobTimer     = 0f;
        private float   _breathTimer  = 0f;
        private Vector3 _bobOffset;
        private Vector3 _breathOffset;
        private float   _landingDip   = 0f;
        private Vector3 _baseLocalPos;

        // ─── Private: Stabilization ────────────────────────────────
        // world-space point ที่กล้องจะมองไปเพื่อ stabilize
        private Vector3 _stabilizationTarget;

        // ─── Private: Shake ────────────────────────────────────────
        private static CameraEffects _instance;

        private float _trauma = 0f;
        private float _noiseX, _noiseY, _noiseZ, _noiseRX, _noiseRY, _noiseRZ;

        private Vector3    _shakePositionOffset;
        private Quaternion _shakeRotationOffset = Quaternion.identity;

        // Movement shake (แยกออกจาก event shake)
        private Vector3    _moveShakePosOffset;
        private Quaternion _moveShakeRotOffset = Quaternion.identity;

        // สถานะซ่อน — หยุด movement shake ทันที + ลด ambient
        private bool _isHiding = false;

        private struct ShakeRequest
        {
            public float trauma, duration, elapsed;
        }
        private List<ShakeRequest> _shakeQueue = new List<ShakeRequest>();

        // ═══════════════════════════════════════════════════════════
        //  Lifecycle
        // ═══════════════════════════════════════════════════════════
        void Start()
        {
            _instance = this;

            if (movement == null) movement = GetComponentInChildren<Movement>();
            if (movement == null) movement = GetComponent<Movement>();

            if (playerCamera == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) playerCamera = cam.transform;
            }

            _cc = GetComponent<CharacterController>();
            if (_cc == null) _cc = GetComponentInChildren<CharacterController>();

            if (playerCamera != null)
                _baseLocalPos = playerCamera.localPosition;

            // Stabilization target เริ่มต้นที่จุดข้างหน้า
            if (playerCamera != null)
                _stabilizationTarget = playerCamera.position + playerCamera.forward * stabilizationDistance;

            // Perlin seeds แบบสุ่ม
            _noiseX  = Random.Range(0f,    1000f);
            _noiseY  = Random.Range(1000f, 2000f);
            _noiseZ  = Random.Range(2000f, 3000f);
            _noiseRX = Random.Range(3000f, 4000f);
            _noiseRY = Random.Range(4000f, 5000f);
            _noiseRZ = Random.Range(5000f, 6000f);
        }

        void OnEnable()  { _instance = this; }
        void OnDisable() { if (_instance == this) _instance = null; }

        void Update()
        {
            if (playerCamera == null) return;

            DetectState();
            UpdateBob();
            UpdateBreathing();
            UpdateLandingDip();
            UpdateMovementShake(); // ← shake ตาม speed (อ่าน CC โดยตรง)
            UpdateShake();         // ← event shake (Shake() API + ambient)
            ApplyPositionOffsets();
        }

        void LateUpdate()
        {
            if (playerCamera == null) return;
            ApplyStabilization();     // stabilize rotation หลัง MouseLook set pitch แล้ว
            ApplyShakeRotation();     // คูณ event shake rotation ทับ
            ApplyMovementShakeRot();  // คูณ movement shake rotation ทับ
        }

        // ═══════════════════════════════════════════════════════════
        //  State Detection
        // ═══════════════════════════════════════════════════════════
        void DetectState()
        {
            if (_cc == null)
            {
                _cc = GetComponentInParent<CharacterController>();
                if (_cc == null) return;
            }

            bool grounded = _cc.isGrounded;

            // Landing dip
            if (!_wasGrounded && grounded && enableLandingDip)
            {
                float fallSpeed = Mathf.Abs(_prevVelY);
                if (fallSpeed > 2f)
                    _landingDip = Mathf.Clamp(
                        landingDipAmount * (fallSpeed / 5f), 0f, landingDipAmount * 2f);
            }

            _wasGrounded = grounded;
            _prevVelY    = _cc.velocity.y;

            Vector3 flatVel = _cc.velocity;
            flatVel.y  = 0f;
            float speed = flatVel.magnitude;

            _isMoving   = speed > 0.3f;
            _isRunning  = speed > 4f;
            _isCrouching = !_isRunning && speed > 0.1f && speed < 2f;
        }

        // ═══════════════════════════════════════════════════════════
        //  Head Bob
        // ═══════════════════════════════════════════════════════════
        void UpdateBob()
        {
            if (!enableHeadBob || !_isMoving)
            {
                _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, Time.deltaTime * transitionSpeed);
                return;
            }

            float freq = _isRunning   ? runBobFrequency
                       : _isCrouching ? crouchBobFrequency
                       : walkBobFrequency;

            float ampY = _isRunning   ? runBobAmplitudeY
                       : _isCrouching ? crouchBobAmplitudeY
                       : walkBobAmplitudeY;

            float ampX = _isRunning   ? runBobAmplitudeX
                       : _isCrouching ? crouchBobAmplitudeX
                       : walkBobAmplitudeX;

            _bobTimer += Time.deltaTime * freq * Mathf.PI * 2f;

            float targetY = Mathf.Sin(_bobTimer) * ampY;
            float targetX = Mathf.Cos(_bobTimer * 0.5f) * ampX;

            _bobOffset = Vector3.Lerp(
                _bobOffset,
                new Vector3(targetX, targetY, 0f),
                Time.deltaTime * transitionSpeed);
        }

        // ═══════════════════════════════════════════════════════════
        //  Idle Breathing
        // ═══════════════════════════════════════════════════════════
        void UpdateBreathing()
        {
            if (!enableBreathing) return;

            _breathTimer += Time.deltaTime * breathFrequency * Mathf.PI * 2f;

            float bY = Mathf.Sin(_breathTimer) * breathAmplitudeY;
            float bX = Mathf.Sin(_breathTimer * 0.5f) * breathAmplitudeX;

            float weight = _isMoving ? 0.3f : 1f;
            _breathOffset = Vector3.Lerp(
                _breathOffset,
                new Vector3(bX, bY, 0f) * weight,
                Time.deltaTime * 4f);
        }

        // ═══════════════════════════════════════════════════════════
        //  Landing Dip
        // ═══════════════════════════════════════════════════════════
        void UpdateLandingDip()
        {
            if (!enableLandingDip) return;
            _landingDip = Mathf.MoveTowards(_landingDip, 0f, Time.deltaTime * landingDipSpeed);
        }

        // ═══════════════════════════════════════════════════════════
        //  Apply Position (Head Bob + Shake position)
        // ═══════════════════════════════════════════════════════════
        void ApplyPositionOffsets()
        {
            Vector3 offset = _bobOffset + _breathOffset
                           + new Vector3(0f, -_landingDip, 0f)
                           + _shakePositionOffset
                           + _moveShakePosOffset; // ← movement shake position

            playerCamera.localPosition = Vector3.Lerp(
                playerCamera.localPosition,
                _baseLocalPos + offset,
                Time.deltaTime * transitionSpeed);
        }

        // ═══════════════════════════════════════════════════════════
        //  Stabilization — หัวใจของเทคนิคนี้
        //
        //  ทุก frame:
        //    1. อัพเดต stabilization target ที่ world-space ข้างหน้า
        //       (ตำแหน่งก่อน bob ขยับ — เหมือนจุดที่ตาจะมองถ้าไม่มี bob)
        //    2. Lerp rotation ของกล้องไปมองจุดนั้น
        //       → กล้องจึง "นิ่ง" แม้ position จะเด้ง
        // ═══════════════════════════════════════════════════════════
        void ApplyStabilization()
        {
            if (!enableStabilization) return;

            // คำนวณ "ตำแหน่งหัวที่ไม่ bob" = world pos ของกล้องถ้าไม่มี bob offset
            // ใช้ parent + baseLocalPos เป็น reference
            Vector3 unbobbedWorldPos = transform.TransformPoint(_baseLocalPos);

            // อัพเดต target ให้อยู่ที่ระยะ stabilizationDistance ข้างหน้า
            // ทิศทางคือ forward ของ parent (หมุนตาม MouseLook yaw)
            // + pitch จาก xRotation ของ MouseLook (ก้ม/เงย)
            Vector3 lookDir = playerCamera.forward; // ทิศที่ MouseLook กำหนด (pitch + yaw)
            Vector3 targetPoint = unbobbedWorldPos + lookDir * stabilizationDistance;

            // Smooth update target เพื่อไม่ให้กระตุก
            _stabilizationTarget = Vector3.Lerp(
                _stabilizationTarget, targetPoint, Time.deltaTime * 25f);

            // คำนวณ rotation ที่กล้องควรหันไป
            Vector3 dirToTarget = (_stabilizationTarget - playerCamera.position).normalized;
            if (dirToTarget == Vector3.zero) return;

            Quaternion targetRot = Quaternion.LookRotation(dirToTarget, playerCamera.up);

            // Lerp ระหว่าง "ตามหัว" กับ "มองจุดข้างหน้า"
            // stabilizationAmount = 1 → นิ่งสุด, 0 → ตามหัวเต็มที่
            playerCamera.rotation = Quaternion.Slerp(
                playerCamera.rotation,
                targetRot,
                stabilizationAmount);
        }

        // ═══════════════════════════════════════════════════════════
        //  Movement Shake — อ่าน CC velocity โดยตรง ไม่ผ่าน trauma
        // ═══════════════════════════════════════════════════════════
        void UpdateMovementShake()
        {
            // หยุดทันทีตอนซ่อน (ตู้) — CC ถูกปิดทำให้ velocity คืนค่าแปลก
            if (_isHiding || !enableMovementShake)
            {
                _moveShakePosOffset = Vector3.Lerp(_moveShakePosOffset, Vector3.zero,       Time.deltaTime * 15f);
                _moveShakeRotOffset = Quaternion.Slerp(_moveShakeRotOffset, Quaternion.identity, Time.deltaTime * 15f);
                return;
            }

            // หา CC ถ้ายังไม่มี
            if (_cc == null) _cc = GetComponentInParent<CharacterController>();
            if (_cc == null) _cc = GetComponent<CharacterController>();

            float speed = 0f;
            // เช็ค cc.enabled ด้วย — CC ที่ปิดจะคืน velocity ที่ไม่ถูกต้อง
            if (_cc != null && _cc.enabled)
            {
                Vector3 flatVel = _cc.velocity;
                flatVel.y = 0f;
                speed     = flatVel.magnitude;
            }

            // t = 0 (ยืน) → 1 (วิ่งเต็มสปีด)
            float t = Mathf.InverseLerp(moveShakeMinSpeed, moveShakeMaxSpeed, speed);

            if (t < 0.001f)
            {
                _moveShakePosOffset = Vector3.Lerp(_moveShakePosOffset, Vector3.zero,       Time.deltaTime * 10f);
                _moveShakeRotOffset = Quaternion.Slerp(_moveShakeRotOffset, Quaternion.identity, Time.deltaTime * 10f);
                return;
            }

            // แบ่งเป็น walk / run zone
            // walk zone: t 0→0.6,  run zone: t 0.6→1
            float walkT = Mathf.Clamp01(t / 0.6f);
            float runT  = Mathf.Clamp01((t - 0.6f) / 0.4f);

            float maxPos = Mathf.Lerp(walkShakePosition, runShakePosition, runT) * (walkT < 1f ? walkT : 1f);
            float maxRot = Mathf.Lerp(walkShakeRotation, runShakeRotation, runT) * (walkT < 1f ? walkT : 1f);

            // Perlin noise → -1..1
            float time = Time.time * moveShakeNoiseSpeed;
            float px = (Mathf.PerlinNoise(_noiseX  + 777f, time)         * 2f - 1f) * maxPos;
            float py = (Mathf.PerlinNoise(_noiseY  + 777f, time + 13.7f) * 2f - 1f) * maxPos;
            float rx = (Mathf.PerlinNoise(_noiseRX + 777f, time + 27.3f) * 2f - 1f) * maxRot;
            float ry = (Mathf.PerlinNoise(_noiseRY + 777f, time + 41.9f) * 2f - 1f) * maxRot * 0.4f;
            float rz = (Mathf.PerlinNoise(_noiseRZ + 777f, time + 58.1f) * 2f - 1f) * maxRot * 0.35f;

            _moveShakePosOffset = new Vector3(px, py, 0f);
            _moveShakeRotOffset = Quaternion.Euler(rx, ry, rz);
        }

        void ApplyMovementShakeRot()
        {
            if (!enableMovementShake) return;
            playerCamera.localRotation = playerCamera.localRotation * _moveShakeRotOffset;
        }

        // ═══════════════════════════════════════════════════════════
        //  Apply Shake Rotation (ทับบน stabilized rotation)
        // ═══════════════════════════════════════════════════════════
        void ApplyShakeRotation()
        {
            if (!enableShake) return;
            playerCamera.localRotation = playerCamera.localRotation * _shakeRotationOffset;
        }

        // ═══════════════════════════════════════════════════════════
        //  Camera Shake — Trauma Model + Perlin Noise
        // ═══════════════════════════════════════════════════════════
        void UpdateShake()
        {
            if (!enableShake) return;

            float t = Time.time;

            // Process queue
            for (int i = _shakeQueue.Count - 1; i >= 0; i--)
            {
                var req = _shakeQueue[i];
                req.elapsed += Time.deltaTime;

                float ratio = 1f - Mathf.Clamp01(req.elapsed / req.duration);
                AddTrauma(req.trauma * ratio * Time.deltaTime * (1f / req.duration));

                _shakeQueue[i] = req;
                if (req.elapsed >= req.duration)
                    _shakeQueue.RemoveAt(i);
            }

            // Decay
            _trauma = Mathf.MoveTowards(_trauma, 0f, Time.deltaTime * traumaDecayRate);

            float effective = Mathf.Clamp01(_trauma + ambientTrauma);
            float shake     = effective * effective;

            if (shake < 0.0001f)
            {
                _shakePositionOffset = Vector3.Lerp(_shakePositionOffset, Vector3.zero, Time.deltaTime * 12f);
                _shakeRotationOffset = Quaternion.Slerp(_shakeRotationOffset, Quaternion.identity, Time.deltaTime * 12f);
                return;
            }

            float ns = shakeNoiseSpeed;
            float pX = (Mathf.PerlinNoise(_noiseX, t * ns)         * 2f - 1f) * shake * shakeMaxPositionAmount;
            float pY = (Mathf.PerlinNoise(_noiseY, t * ns + 17f)   * 2f - 1f) * shake * shakeMaxPositionAmount;

            // Ambient ช้า
            if (ambientTrauma > 0f)
            {
                float at = ambientTrauma * ambientTrauma;
                pX += (Mathf.PerlinNoise(_noiseX + 9999f, t * ambientNoiseSpeed) * 2f - 1f) * at * shakeMaxPositionAmount;
                pY += (Mathf.PerlinNoise(_noiseY + 9999f, t * ambientNoiseSpeed + 17f) * 2f - 1f) * at * shakeMaxPositionAmount;
            }

            _shakePositionOffset = new Vector3(pX, pY, 0f);

            float rX = (Mathf.PerlinNoise(_noiseRX, t * ns + 33f) * 2f - 1f) * shake * shakeMaxRotationAmount;
            float rY = (Mathf.PerlinNoise(_noiseRY, t * ns + 57f) * 2f - 1f) * shake * shakeMaxRotationAmount * 0.5f;
            float rZ = (Mathf.PerlinNoise(_noiseRZ, t * ns + 81f) * 2f - 1f) * shake * shakeMaxRotationAmount * 0.4f;

            _shakeRotationOffset = Quaternion.Euler(rX, rY, rZ);
        }

        // ═══════════════════════════════════════════════════════════
        //  Public API
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// สั่น camera — เรียกจาก script อื่นได้เลย
        /// trauma 0–1 (ความรุนแรง), duration (วินาที)
        /// ตัวอย่าง: CameraEffects.Shake(0.6f, 0.4f);
        /// </summary>
        public static void Shake(float trauma, float duration = 0.3f)
        {
            if (_instance == null || !_instance.enableShake) return;
            _instance.AddShakeRequest(Mathf.Clamp01(trauma), Mathf.Max(0.05f, duration));
        }

        /// <summary>
        /// เรียกจาก HidingSpot เพื่อหยุด/เปิด movement shake
        /// ตอนซ่อน (CC ถูกปิด) → หยุด shake ทันที
        /// ตอนออก (CC ถูกเปิด) → เปิด shake กลับ
        /// </summary>
        public static void SetHiding(bool hiding)
        {
            if (_instance == null) return;
            _instance._isHiding = hiding;

            if (hiding)
            {
                // ล้าง shake ทันทีเมื่อซ่อน
                _instance._moveShakePosOffset = Vector3.zero;
                _instance._moveShakeRotOffset = Quaternion.identity;
                _instance._trauma             = 0f;
                _instance._shakeQueue.Clear();
                _instance._shakePositionOffset = Vector3.zero;
                _instance._shakeRotationOffset = Quaternion.identity;
            }
        }

        /// <summary>
        /// เรียกเมื่อ Eye Height เปลี่ยน (crouch) ให้ update base position
        /// </summary>
        public void UpdateBasePosition()
        {
            if (playerCamera != null)
                _baseLocalPos = playerCamera.localPosition;
        }

        // ─── Private helpers ───────────────────────────────────────
        void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        void AddShakeRequest(float trauma, float duration)
        {
            _trauma = Mathf.Clamp01(_trauma + trauma * 0.7f);
            _shakeQueue.Add(new ShakeRequest { trauma = trauma, duration = duration, elapsed = 0f });
        }
    }
}
