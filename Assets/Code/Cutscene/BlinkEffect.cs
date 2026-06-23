using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace HorrorGame
{
    /// <summary>
    /// เอฟเฟกต์กระพริบตา (Blink) ใช้ Image สีดำบน Canvas
    /// 
    /// วิธีตั้งค่าใน Unity:
    /// 1. สร้าง Canvas (Screen Space - Overlay)
    /// 2. สร้าง Image ลูกของ Canvas: สีดำ, Raycast Target = false
    ///    ขนาดเต็มหน้าจอ (Anchor Presets: Stretch Stretch)
    /// 3. ตั้งชื่อ เช่น "BlinkPanel"
    /// 4. วาง Script นี้ไว้ที่ GameObject นั้น
    /// 5. ลาก BlinkPanel ใส่ช่อง blinkPanel
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class BlinkEffect : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Image สีดำที่คลุมทั้งหน้าจอ (ใช้ CanvasGroup บน GameObject นี้)")]
        public Image blinkPanel;

        [Header("Blink Curve")]
        [Tooltip("Curve ควบคุมความเร็วของการปิด/เปิดตา (ปกติ EaseIn ตอนปิด EaseOut ตอนเปิด)")]
        public AnimationCurve blinkCloseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve blinkOpenCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("Color")]
        [Tooltip("สีของหน้าจอตอนหลับตา")]
        public Color blinkColor = Color.black;

        private CanvasGroup _canvasGroup;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            if (blinkPanel != null)
                blinkPanel.color = blinkColor;
        }

        /// <summary>
        /// เล่น animation กระพริบตาสมบูรณ์ 1 ครั้ง
        /// </summary>
        public IEnumerator DoBlink(float closeTime, float holdTime, float openTime)
        {
            // ปิดตา (fade ดำขึ้น)
            yield return StartCoroutine(FadeAlpha(0f, 1f, closeTime, blinkCloseCurve));

            // ตาปิดค้าง
            yield return new WaitForSeconds(holdTime);

            // เปิดตา (fade จางลง)
            yield return StartCoroutine(FadeAlpha(1f, 0f, openTime, blinkOpenCurve));
        }

        /// <summary>
        /// ค่อยๆ ปิดตา (ไม่เปิด) — ใช้สำหรับ fade to black ทั่วไป
        /// </summary>
        public IEnumerator FadeToBlack(float duration)
        {
            yield return StartCoroutine(FadeAlpha(0f, 1f, duration, blinkCloseCurve));
        }

        /// <summary>
        /// ค่อยๆ เปิดตาจากดำ
        /// </summary>
        public IEnumerator FadeFromBlack(float duration)
        {
            yield return StartCoroutine(FadeAlpha(1f, 0f, duration, blinkOpenCurve));
        }

        IEnumerator FadeAlpha(float from, float to, float duration, AnimationCurve curve)
        {
            float elapsed = 0f;
            _canvasGroup.alpha = from;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _canvasGroup.alpha = Mathf.Lerp(from, to, curve.Evaluate(t));
                yield return null;
            }

            _canvasGroup.alpha = to;
        }

        /// <summary>
        /// ตั้งค่า alpha โดยตรง (ใช้ใน HallucinationCutscene)
        /// </summary>
        public void SetAlpha(float alpha) => _canvasGroup.alpha = Mathf.Clamp01(alpha);

        /// <summary>
        /// alpha ปัจจุบัน
        /// </summary>
        public float CurrentAlpha => _canvasGroup.alpha;
    }
}
