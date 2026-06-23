using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections; // เพิ่มเข้ามาเพื่อให้ใช้งาน Coroutine ได้

namespace HorrorGame
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance;

        public enum SoundtrackState
        {
            Normal,         // ตอนปกติ
            GhostSpotted,   // ตอนโดนผีเจอ
            GhostChasing    // ตอนโดนผีไล่
        }

        [Header("Audio Sources")]
        [Tooltip("แหล่งกำเนิดเสียงสำหรับเพลงพื้นหลัง (BGM)")]
        [SerializeField] private AudioSource bgmSource;
        [Tooltip("แหล่งกำเนิดเสียงสำหรับ Effect (SFX) ทั่วไป")]
        [SerializeField] private AudioSource sfxSource;

        [Header("Soundtracks")]
        [Tooltip("เพลงตอนปกติ")]
        [SerializeField] private AudioClip normalBGM;
        [Tooltip("เพลงตอนโดนผีเจอ")]
        [SerializeField] private AudioClip ghostSpottedBGM;
        [Tooltip("เพลงตอนโดนผีไล่")]
        [SerializeField] private AudioClip ghostChasingBGM;

        [Header("Settings")]
        [Tooltip("เวลาในการ Fade เปลี่ยนเพลง (วินาที)")]
        [SerializeField] private float fadeDuration = 1f;
        [Tooltip("ความดังสูงสุดของ BGM")]
        [SerializeField] private float maxBGMVolume = 1f;

        private SoundtrackState currentState = SoundtrackState.Normal;
        private Coroutine fadeCoroutine;

        private void Awake()
        {
            // ทำตัวเป็น Singleton
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // ทำให้ไม่โดนทำลายตอนเปลี่ยน Scene
                Debug.Log("[SoundManager] โหลดระบบเสียงสำเร็จ");
            }
            else
            {
                Destroy(gameObject); // ถ้ามีอยู่แล้วให้ทำลายตัวใหม่ทิ้ง ป้องกันซ้อนกัน
            }
        }

        private void Start()
        {
            // เริ่มต้นด้วยเพลงตอนปกติ
            ChangeSoundtrackState(SoundtrackState.Normal);
        }

        /// <summary>
        /// เปลี่ยน State ของเพลงพื้นหลัง (Normal, GhostSpotted, GhostChasing) พร้อม Fade เสียง
        /// </summary>
        public void ChangeSoundtrackState(SoundtrackState newState)
        {
            // ถ้าเป็น State เดิม และกำลังเล่นอยู่ ไม่ต้องเปลี่ยน
            if (currentState == newState && bgmSource.isPlaying && fadeCoroutine == null) return;
            
            currentState = newState;
            AudioClip nextClip = null;

            switch (newState)
            {
                case SoundtrackState.Normal:
                    nextClip = normalBGM;
                    break;
                case SoundtrackState.GhostSpotted:
                    nextClip = ghostSpottedBGM;
                    break;
                case SoundtrackState.GhostChasing:
                    nextClip = ghostChasingBGM;
                    break;
            }

            if (nextClip != null)
            {
                if (fadeCoroutine != null)
                {
                    StopCoroutine(fadeCoroutine);
                }
                fadeCoroutine = StartCoroutine(FadeToNextClip(nextClip));
            }
        }

        private IEnumerator FadeToNextClip(AudioClip nextClip)
        {
            // Fade Out เพลงเดิมก่อน
            if (bgmSource.isPlaying)
            {
                while (bgmSource.volume > 0)
                {
                    bgmSource.volume -= maxBGMVolume * Time.deltaTime / (fadeDuration / 2);
                    yield return null;
                }
                bgmSource.Stop();
            }

            // เปลี่ยนเพลงและ Fade In เพลงใหม่
            bgmSource.clip = nextClip;
            bgmSource.loop = true; // เปิดให้เพลงลูปไปเรื่อยๆ
            bgmSource.volume = 0f;
            bgmSource.Play();

            while (bgmSource.volume < maxBGMVolume)
            {
                bgmSource.volume += maxBGMVolume * Time.deltaTime / (fadeDuration / 2);
                yield return null;
            }

            bgmSource.volume = maxBGMVolume;
            fadeCoroutine = null;
        }

        /// <summary>
        /// เล่นเพลงพื้นหลัง (BGM) แบบกำหนดเอง
        /// </summary>
        public void PlayCustomBGM(AudioClip clip, bool loop = true)
        {
            if (bgmSource == null || clip == null) return;

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.volume = maxBGMVolume;
            bgmSource.Play();
        }

        /// <summary>
        /// หยุดเล่นเพลงพื้นหลัง
        /// </summary>
        public void StopBGM()
        {
            if (bgmSource != null)
            {
                if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
                bgmSource.Stop();
            }
        }

        /// <summary>
        /// เล่นเสียง Effects (SFX)
        /// </summary>
        public void PlaySFX(AudioClip clip)
        {
            if (sfxSource == null || clip == null) return;

            sfxSource.PlayOneShot(clip);
        }
    }
}
