using UnityEngine;
using System.Collections;

namespace HorrorGame
{
    /// <summary>
    /// Singleton จัดการอาการ PTSD ทั้งหมดของเกม
    /// Script อื่นเรียก PTSDSymptomManager.Instance.TriggerSymptom() เพื่อกระตุ้นอาการ
    ///
    /// Setup:
    /// - วางไว้ใน Scene ตัวเดียว (singleton)
    /// - ลาก BlinkEffect ใส่ช่อง blinkEffect
    /// - ลาก AudioSource + AudioClip ต่างๆ
    /// - (URP) ลาก Volume ที่มี Vignette / Color Adjustments component
    /// </summary>
    public class PTSDSymptomManager : MonoBehaviour
    {
        public static PTSDSymptomManager Instance { get; private set; }

        [Header("References")]
        [Tooltip("BlinkEffect บน Canvas สำหรับกระพริบตา")]
        public BlinkEffect blinkEffect;

        [Tooltip("HallucinationCutscene ที่จะเรียกตอนอาการรุนแรง (ตัวเลือก)")]
        public HallucinationCutscene hallucinationCutscene;

        [Header("Screen Shake")]
        public bool useScreenShake = true;
        public float shakeIntensity = 0.06f;
        public float shakeDuration   = 0.8f;

        [Header("Vignette (ต้องการ URP Volume)")]
        [Tooltip("เปิดเพื่อใช้ vignette แดง — ต้องการ Post Processing Volume ใน Scene")]
        public bool useVignette = false;

        [Header("Desaturate (ต้องการ URP Volume)")]
        [Tooltip("เปิดเพื่อใช้ desaturate ขาวดำ — ต้องการ Post Processing Volume")]
        public bool useDesaturate = false;

        [Header("Audio")]
        public AudioSource audioSource;

        [Tooltip("เสียง heartbeat ตอนอาการกำเริบ")]
        public AudioClip heartbeatClip;

        [Tooltip("เสียงปืน / เสียง flashback")]
        public AudioClip[] flashbackSounds;

        [Tooltip("เสียงหายใจหนัก")]
        public AudioClip heavyBreathClip;

        [Header("Timing")]
        [Tooltip("ระยะเวลาที่ freeze ตัวละคร (วินาที), 0 = ไม่ freeze")]
        public float freezeDuration = 1.5f;

        [Tooltip("ระยะเวลา desaturate ค้างไว้ (วินาที)")]
        public float desaturateDuration = 3f;

        [Tooltip("Cooldown ก่อน trigger อาการซ้ำได้ (วินาที)")]
        public float symptomCooldown = 10f;

        // --- Private ---
        private float _lastSymptomTime = -999f;
        private bool _isTriggered = false;
        private Transform _playerCamera;
        private Movement _playerMovement;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerMovement = player.GetComponentInChildren<Movement>();
                Camera cam = player.GetComponentInChildren<Camera>();
                if (cam != null) _playerCamera = cam.transform;
            }
            if (_playerCamera == null && Camera.main != null)
                _playerCamera = Camera.main.transform;
        }

        // ─────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────

        /// <summary>
        /// เรียกจาก Script อื่น เพื่อกระตุ้น PTSD Symptom
        /// </summary>
        public void TriggerSymptom()
        {
            if (_isTriggered) return;
            if (Time.time - _lastSymptomTime < symptomCooldown) return;

            _lastSymptomTime = Time.time;
            StartCoroutine(SymptomSequence());
        }

        /// <summary>
        /// เรียกได้โดยตรงถ้าต้องการ bypass cooldown (เช่น เหตุการณ์พิเศษ)
        /// </summary>
        public void ForceSymptom() => StartCoroutine(SymptomSequence());

        // ─────────────────────────────────────────
        //  Sequence
        // ─────────────────────────────────────────

        IEnumerator SymptomSequence()
        {
            _isTriggered = true;
            Debug.Log("😰 [PTSDSymptomManager] อาการกำเริบ!");

            // 1. เสียง heartbeat
            PlaySound(heartbeatClip, 0.9f);

            // 2. เสียง flashback สุ่ม
            yield return new WaitForSeconds(0.3f);
            PlayRandomSound(flashbackSounds, 0.8f);

            // 3. Screen Shake
            if (useScreenShake && _playerCamera != null)
                StartCoroutine(ShakeCamera());

            // 4. Freeze ตัวละคร
            if (freezeDuration > 0f && _playerMovement != null)
                StartCoroutine(FreezePlayer());

            // 5. Vignette แดง (ถ้าใช้ Post Processing)
            if (useVignette)
                StartCoroutine(VignettePulse());

            // 6. Desaturate ขาวดำ
            if (useDesaturate)
                StartCoroutine(DesaturatePulse());

            // 7. รอก่อน flashback visual
            yield return new WaitForSeconds(0.6f);

            // 8. Hallucination flash (ถ้ามี)
            if (hallucinationCutscene != null)
                hallucinationCutscene.TriggerCutscene();
            else if (blinkEffect != null)
                yield return StartCoroutine(blinkEffect.DoBlink(0.1f, 0.05f, 0.2f));

            // 9. เสียงหายใจหนัก
            yield return new WaitForSeconds(0.5f);
            PlaySound(heavyBreathClip, 0.7f);

            yield return new WaitForSeconds(1f);
            _isTriggered = false;
        }

        IEnumerator FreezePlayer()
        {
            if (_playerMovement == null) yield break;
            _playerMovement.enabled = false;
            yield return new WaitForSeconds(freezeDuration);
            _playerMovement.enabled = true;
        }

        IEnumerator ShakeCamera()
        {
            if (_playerCamera == null) yield break;
            Vector3 origin = _playerCamera.localPosition;
            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float fade = 1f - (elapsed / shakeDuration);
                float ox = Random.Range(-shakeIntensity, shakeIntensity) * fade;
                float oy = Random.Range(-shakeIntensity, shakeIntensity) * fade;
                _playerCamera.localPosition = origin + new Vector3(ox, oy, 0f);
                yield return null;
            }
            _playerCamera.localPosition = origin;
        }

        IEnumerator VignettePulse()
        {
            // Placeholder: ถ้าใช้ URP Volume ให้ต่อเชื่อม VolumeProfile ที่นี่
            // ตัวอย่างการใช้ UnityEngine.Rendering.Universal.Vignette
            yield return new WaitForSeconds(desaturateDuration);
        }

        IEnumerator DesaturatePulse()
        {
            // Placeholder: ถ้าใช้ URP Volume ให้ต่อเชื่อม ColorAdjustments ที่นี่
            yield return new WaitForSeconds(desaturateDuration);
        }

        // ─────────────────────────────────────────
        //  Audio helpers
        // ─────────────────────────────────────────

        void PlaySound(AudioClip clip, float vol = 1f)
        {
            if (audioSource == null || clip == null) return;
            audioSource.PlayOneShot(clip, vol);
        }

        void PlayRandomSound(AudioClip[] clips, float vol = 1f)
        {
            if (clips == null || clips.Length == 0) return;
            PlaySound(clips[Random.Range(0, clips.Length)], vol);
        }
    }
}
