using UnityEngine;
using System.Collections;

namespace HorrorGame
{
    /// <summary>
    /// คัตซีนหลอนประสาท: ผีโผล่ที่เก้าอี้/ศพแวบหนึ่ง
    /// แล้วตัวละครกระพริบตา (Blink) แล้วผีก็หายไป
    /// 
    /// วิธีใช้:
    /// 1. วาง Script นี้ไว้ที่ GameObject ใดก็ได้ในฉาก
    /// 2. ลาก Ghost GameObject (ผีหรือศพ) ไส่ช่อง ghostObject
    /// 3. ลาก BlinkEffect ที่อยู่ใน Canvas ใส่ช่อง blinkEffect
    /// 4. (ตัวเลือก) ลาก AudioSource และ AudioClip เสียงหลอน
    /// 5. เรียก TriggerCutscene() จาก Trigger Zone หรือ Script อื่น
    /// </summary>
    public class HallucinationCutscene : MonoBehaviour
    {
        [Header("Ghost / Corpse Reference")]
        [Tooltip("GameObject ของผีหรือศพที่จะโผล่ออกมาแวบหนึ่ง")]
        public GameObject ghostObject;

        [Tooltip("ตำแหน่งที่ผีจะโผล่ (ถ้าไม่ได้ตั้ง จะใช้ตำแหน่งปัจจุบันของ ghostObject)")]
        public Transform ghostSpawnPoint;

        [Header("Blink Effect")]
        [Tooltip("Script BlinkEffect ที่อยู่ใน Canvas UI")]
        public BlinkEffect blinkEffect;

        [Header("Timing")]
        [Tooltip("หน่วงเวลาหลังจาก Trigger ก่อนผีจะโผล่ (วินาที)")]
        public float delayBeforeGhost = 0.3f;

        [Tooltip("เวลาที่ผีโผล่อยู่บนหน้าจอ (วินาที)")]
        public float ghostVisibleDuration = 1.2f;

        [Tooltip("ความเร็วที่ผี Fade In (วินาที)")]
        public float ghostFadeInTime = 0.08f;

        [Tooltip("ความเร็วที่ Blink ปิดตา (วินาที)")]
        public float blinkCloseTime = 0.12f;

        [Tooltip("เวลาที่ตาปิดค้างไว้ (วินาที)")]
        public float blinkHoldTime = 0.08f;

        [Tooltip("ความเร็วที่ Blink เปิดตา (วินาที)")]
        public float blinkOpenTime = 0.18f;

        [Header("Camera Shake")]
        [Tooltip("เปิด/ปิด Camera Shake ตอนผีโผล่")]
        public bool useCameraShake = true;

        [Tooltip("ความแรงของ Camera Shake")]
        public float shakeIntensity = 0.04f;

        [Tooltip("ระยะเวลา Camera Shake (วินาที)")]
        public float shakeDuration = 0.25f;

        [Header("Audio")]
        [Tooltip("AudioSource สำหรับเสียงหลอน (ถ้ามี)")]
        public AudioSource audioSource;

        [Tooltip("เสียงที่ดังตอนผีโผล่ (สุ่มจาก list นี้)")]
        public AudioClip[] ghostAppearSounds;

        [Tooltip("เสียงที่ดังตอนกระพริบตา")]
        public AudioClip blinkSound;

        [Header("State")]
        [Tooltip("กี่ครั้งที่จะให้ผีโผล่ (-1 = ไม่จำกัด)")]
        public int maxTriggerCount = 1;

        // --- Private ---
        private int _triggerCount = 0;
        private bool _isPlaying = false;
        private Transform _playerCamera;
        private GhostAppearance _ghostAppearance;

        void Start()
        {
            // ซ่อนผีตั้งแต่เริ่มต้น
            if (ghostObject != null)
            {
                ghostObject.SetActive(false);
                _ghostAppearance = ghostObject.GetComponent<GhostAppearance>();
            }

            // หา Camera ของผู้เล่น
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Camera cam = player.GetComponentInChildren<Camera>();
                if (cam != null) _playerCamera = cam.transform;
            }

            // fallback หา Main Camera
            if (_playerCamera == null && Camera.main != null)
                _playerCamera = Camera.main.transform;
        }

        /// <summary>
        /// เรียกฟังก์ชันนี้เพื่อเริ่มคัตซีนผีหลอน
        /// </summary>
        public void TriggerCutscene()
        {
            if (_isPlaying) return;
            if (maxTriggerCount >= 0 && _triggerCount >= maxTriggerCount) return;

            _triggerCount++;
            StartCoroutine(PlayCutscene());
        }

        IEnumerator PlayCutscene()
        {
            _isPlaying = true;

            // --- 1. หน่วงเล็กน้อย ---
            yield return new WaitForSeconds(delayBeforeGhost);

            // --- 2. เซ็ตตำแหน่งผี ---
            if (ghostObject != null && ghostSpawnPoint != null)
            {
                ghostObject.transform.position = ghostSpawnPoint.position;
                ghostObject.transform.rotation = ghostSpawnPoint.rotation;
            }

            // --- 3. เล่นเสียงผีโผล่ ---
            PlayRandomSound(ghostAppearSounds);

            // --- 4. Camera Shake ---
            if (useCameraShake && _playerCamera != null)
                StartCoroutine(ShakeCamera());

            // --- 5. Fade In ผี ---
            ghostObject?.SetActive(true);
            if (_ghostAppearance != null)
                yield return StartCoroutine(_ghostAppearance.FadeIn(ghostFadeInTime));

            // --- 6. ผีอยู่บนจอสักพัก ---
            float elapsed = 0f;
            while (elapsed < ghostVisibleDuration)
            {
                elapsed += Time.deltaTime;

                // (ตัวเลือก) ทำให้ผีกระตุก/flicker ระหว่างที่โผล่
                if (_ghostAppearance != null)
                    _ghostAppearance.Flicker();

                yield return null;
            }

            // --- 7. กระพริบตา! ---
            if (blinkEffect != null)
            {
                PlaySound(blinkSound);
                yield return StartCoroutine(blinkEffect.DoBlink(blinkCloseTime, blinkHoldTime, blinkOpenTime));
            }
            else
            {
                // ถ้าไม่มี BlinkEffect ให้ fade out ธรรมดา
                if (_ghostAppearance != null)
                    yield return StartCoroutine(_ghostAppearance.FadeOut(0.1f));
                else
                    ghostObject?.SetActive(false);
            }

            // --- 8. ซ่อนผีตอนตาหลับ (จะ SetActive false ก่อนเปิดตา) ---
            if (_ghostAppearance != null)
                _ghostAppearance.SetAlpha(0f);
            ghostObject?.SetActive(false);

            _isPlaying = false;

            Debug.Log("👻 [HallucinationCutscene] คัตซีนสิ้นสุดแล้ว ผีหายไป");
        }

        IEnumerator ShakeCamera()
        {
            if (_playerCamera == null) yield break;

            Vector3 originalLocalPos = _playerCamera.localPosition;
            float elapsed = 0f;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float fade = 1f - (elapsed / shakeDuration);
                float offsetX = Random.Range(-shakeIntensity, shakeIntensity) * fade;
                float offsetY = Random.Range(-shakeIntensity, shakeIntensity) * fade;
                _playerCamera.localPosition = originalLocalPos + new Vector3(offsetX, offsetY, 0f);
                yield return null;
            }

            _playerCamera.localPosition = originalLocalPos;
        }

        void PlayRandomSound(AudioClip[] clips)
        {
            if (audioSource == null || clips == null || clips.Length == 0) return;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        void PlaySound(AudioClip clip)
        {
            if (audioSource == null || clip == null) return;
            audioSource.PlayOneShot(clip);
        }

        /// <summary>
        /// รีเซ็ตตัวนับให้โผล่ได้ใหม่ (เรียกจาก Script อื่น ถ้าต้องการ)
        /// </summary>
        public void ResetTriggerCount() => _triggerCount = 0;

        /// <summary>
        /// ตรวจสอบว่า Cutscene กำลังเล่นอยู่ไหม
        /// </summary>
        public bool IsPlaying => _isPlaying;
    }
}
