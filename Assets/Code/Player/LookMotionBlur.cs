using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorGame
{
    /// <summary>
    /// LookMotionBlur — FPS Horror Camera Effects
    ///
    /// ทำให้กล้องมีเอฟเฟกต์แบบ FPS Horror ตอนหัน:
    ///   1. Motion Blur ผ่าน URP Volume (ยิ่งหันเร็ว = blur มาก)
    ///   2. Camera Roll Tilt (กล้องเอียงซ้าย-ขวาเล็กน้อยตอนหัน)
    ///   3. FOV Sway (FOV ขยายนิดๆ ตอนหันเร็ว — เพิ่มความตึงเครียด)
    ///
    /// Setup:
    ///   1. วาง Script นี้บน Camera GameObject (ตัวเดียวกับที่มี MouseLook หรือ parent ก็ได้)
    ///   2. ต้องมี Volume component ในฉาก (Global Volume) ที่มี Motion Blur override
    ///   3. ลาก Volume ใส่ช่อง targetVolume หรือปล่อยให้หาเองอัตโนมัติ
    ///   4. ถ้าต้องการ FOV Sway ให้ลาก Camera ใส่ playerCamera
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class LookMotionBlur : MonoBehaviour
    {
        // ─── References ────────────────────────────
        [Header("References")]
        [Tooltip("Volume ที่มี Motion Blur override — ถ้าว่างจะหาอัตโนมัติ")]
        public Volume targetVolume;

        [Tooltip("MouseLook script (Camera.cs) — ถ้าว่างจะหาอัตโนมัติ")]
        public MouseLook mouseLook;

        // ─── Motion Blur Settings ──────────────────
        [Header("Motion Blur")]
        [Tooltip("เปิด/ปิด Motion Blur ตอนหัน")]
        public bool enableMotionBlur = true;

        [Tooltip("ความเร็วหัน (deg/s) ที่จะเริ่ม blur")]
        public float blurThreshold = 20f;

        [Tooltip("ความเร็วหัน (deg/s) ที่ blur จะถึงค่าสูงสุด")]
        public float blurMaxSpeed = 180f;

        [Tooltip("ค่า blur สูงสุด (0–1) แนะนำ 0.25–0.5")]
        [Range(0f, 1f)]
        public float maxBlurIntensity = 0.35f;

        [Tooltip("ความเร็วในการเพิ่ม blur (ค่าสูง = blur ขึ้นเร็ว)")]
        public float blurRiseSpeed = 12f;

        [Tooltip("ความเร็วในการลด blur ตอนหยุด (ค่าสูง = หายเร็ว)")]
        public float blurDecaySpeed = 6f;

        // ─── Camera Tilt (Roll) ────────────────────
        [Header("Camera Tilt (Roll)")]
        [Tooltip("เปิด/ปิด camera roll ตอนหัน")]
        public bool enableCameraTilt = true;

        [Tooltip("มุมเอียงสูงสุด (องศา) ตอนหันเต็มสปีด")]
        public float maxTiltAngle = 3.5f;

        [Tooltip("ความนุ่มนวลของการเอียง (ค่าสูง = เอียงเร็ว)")]
        public float tiltSmoothSpeed = 8f;

        // ─── FOV Sway ──────────────────────────────
        [Header("FOV Sway (เพิ่มความตึงเครียด)")]
        [Tooltip("เปิด/ปิด FOV ขยายตอนหันเร็ว")]
        public bool enableFovSway = true;

        [Tooltip("จำนวน FOV ที่เพิ่มขึ้นตอนหันเต็มสปีด")]
        public float maxFovIncrease = 3f;

        [Tooltip("ความนุ่มนวลของ FOV sway")]
        public float fovSmoothSpeed = 5f;

        // ─── Private ───────────────────────────────
        private MotionBlur    _motionBlur;
        private Camera        _cam;
        private float         _baseFov;
        private float         _currentBlurIntensity  = 0f;
        private float         _currentTilt           = 0f;
        private float         _currentFovDelta       = 0f;

        // Fallback: euler tracking เมื่อไม่มี MouseLook
        private float         _prevEulerY = 0f;
        private float         _prevEulerX = 0f;

        // ─── Lifecycle ─────────────────────────────
        void Start()
        {
            _cam    = GetComponent<Camera>();
            _baseFov = _cam.fieldOfView;

            // หา MouseLook อัตโนมัติ (Camera.cs อยู่บน GameObject เดียวกัน)
            if (mouseLook == null)
                mouseLook = GetComponent<MouseLook>();

            // หา Volume ที่มี MotionBlur อัตโนมัติ
            if (targetVolume == null)
                targetVolume = FindFirstObjectByType<Volume>();

            // ดึง MotionBlur component จาก Volume
            if (targetVolume != null)
            {
                if (!targetVolume.profile.TryGet(out _motionBlur))
                {
                    // เพิ่ม MotionBlur ใหม่เข้าไปถ้ายังไม่มี
                    _motionBlur = targetVolume.profile.Add<MotionBlur>(false);
                }
                // ตรวจสอบให้แน่ใจว่า override เปิดอยู่
                _motionBlur.active = true;
                _motionBlur.intensity.overrideState = true;
                _motionBlur.quality.overrideState   = true;

                // ตั้งค่า quality เป็น Medium
                _motionBlur.quality.value = MotionBlurQuality.Medium;

                // เริ่มต้นด้วย intensity = 0
                _motionBlur.intensity.value = 0f;
            }
            else
            {
                Debug.LogWarning("[LookMotionBlur] ไม่พบ Volume ในฉาก — Motion Blur จะไม่ทำงาน");
            }

            _prevEulerY = transform.eulerAngles.y;
            _prevEulerX = transform.eulerAngles.x;
        }

        void Update()
        {
            // อ่าน angular velocity จาก MouseLook ถ้ามี, หรือคำนวณเอง
            float angVelX, angVelY;
            if (mouseLook != null)
            {
                angVelX = mouseLook.angularVelocityX;
                angVelY = mouseLook.angularVelocityY;
            }
            else
            {
                // Fallback: คำนวณจาก euler delta
                float curY    = transform.eulerAngles.y;
                float curX    = transform.eulerAngles.x;
                float deltaY  = Mathf.Abs(Mathf.DeltaAngle(_prevEulerY, curY));
                float deltaX  = Mathf.Abs(Mathf.DeltaAngle(_prevEulerX, curX));
                _prevEulerY   = curY;
                _prevEulerX   = curX;
                angVelX       = deltaX / Time.deltaTime;
                angVelY       = deltaY / Time.deltaTime;
            }

            float angularSpeed = Mathf.Sqrt(angVelX * angVelX + angVelY * angVelY);
            float deltaYSigned = mouseLook != null
                ? (mouseLook.angularVelocityY > 0.1f ? 1f : -1f) * mouseLook.angularVelocityY
                : 0f;

            // ─── Motion Blur ──────────────────────
            UpdateMotionBlur(angularSpeed);

            // ─── Camera Tilt ──────────────────────
            UpdateCameraTilt(angVelY);

            // ─── FOV Sway ────────────────────────
            UpdateFovSway(angularSpeed);
        }

        void OnDestroy()
        {
            // Reset ค่าทั้งหมดตอน script ถูกทำลาย
            if (_motionBlur != null)
                _motionBlur.intensity.value = 0f;

            if (_cam != null)
                _cam.fieldOfView = _baseFov;
        }

        // ──────────────────────────────────────────
        //  Motion Blur
        // ──────────────────────────────────────────
        void UpdateMotionBlur(float angularSpeed)
        {
            if (!enableMotionBlur || _motionBlur == null) return;

            // คำนวณ target intensity จาก angular speed
            float t = Mathf.InverseLerp(blurThreshold, blurMaxSpeed, angularSpeed);
            float targetIntensity = t * maxBlurIntensity;

            // Asymmetric smoothing: เพิ่มเร็ว ลดช้า
            float smoothSpeed = targetIntensity > _currentBlurIntensity ? blurRiseSpeed : blurDecaySpeed;
            _currentBlurIntensity = Mathf.Lerp(_currentBlurIntensity, targetIntensity, Time.deltaTime * smoothSpeed);

            _motionBlur.intensity.value = _currentBlurIntensity;
        }

        // ──────────────────────────────────────────
        //  Camera Tilt (Roll)
        // ──────────────────────────────────────────
        void UpdateCameraTilt(float angVelY)
        {
            if (!enableCameraTilt) return;

            // angVelY เป็น deg/s (บวกเสมอ) — หาทิศจาก MouseLook
            float sign = (mouseLook != null && Input.GetAxis("Mouse X") < 0f) ? -1f : 1f;
            float t    = Mathf.Clamp01(angVelY / blurMaxSpeed);
            float targetTilt = -sign * t * maxTiltAngle;

            _currentTilt = Mathf.Lerp(_currentTilt, targetTilt, Time.deltaTime * tiltSmoothSpeed);

            // Apply roll ผ่าน Quaternion multiply เพื่อไม่บล็อก pitch/yaw ของ MouseLook
            Quaternion roll       = Quaternion.AngleAxis(_currentTilt, Vector3.forward);
            Quaternion pitchOnly  = Quaternion.Euler(transform.localEulerAngles.x, 0f, 0f);
            transform.localRotation = pitchOnly * roll;
        }

        // ──────────────────────────────────────────
        //  FOV Sway
        // ──────────────────────────────────────────
        void UpdateFovSway(float angularSpeed)
        {
            if (!enableFovSway || _cam == null) return;

            float t = Mathf.InverseLerp(blurThreshold, blurMaxSpeed, angularSpeed);
            float targetFovDelta = t * maxFovIncrease;

            _currentFovDelta = Mathf.Lerp(_currentFovDelta, targetFovDelta, Time.deltaTime * fovSmoothSpeed);
            _cam.fieldOfView = _baseFov + _currentFovDelta;
        }

        // ──────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────

        /// <summary>
        /// เรียกตอน base FOV เปลี่ยน (เช่น crouch/sprint FOV change)
        /// </summary>
        public void SetBaseFov(float newBaseFov)
        {
            _baseFov = newBaseFov;
        }

        /// <summary>
        /// เปิด/ปิด effect ทั้งหมดชั่วคราว (เช่น ตอน cutscene)
        /// </summary>
        public void SetEnabled(bool value)
        {
            enableMotionBlur  = value;
            enableCameraTilt  = value;
            enableFovSway     = value;

            if (!value)
            {
                if (_motionBlur != null) _motionBlur.intensity.value = 0f;
                if (_cam != null) _cam.fieldOfView = _baseFov;
            }
        }
    }
}
