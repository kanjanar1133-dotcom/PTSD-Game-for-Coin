using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace HorrorGame
{
    /// <summary>
    /// DeadRoom — ห้องที่เข้าไปแล้วมีแต่คนตาย
    ///
    /// เมื่อผู้เล่นเข้าห้อง:
    ///  1. ไฟกะพริบ
    ///  2. ร่างคนตาย spawn / activate พร้อมกัน
    ///  3. เสียง ambient น่าขนลุก
    ///  4. ถ้าอยู่นานเกินไป → เรียก PTSD Symptom + Hallucination
    ///
    /// Setup:
    ///   1. วาง Script นี้บน Empty GameObject
    ///   2. เพิ่ม Box Collider → ติ๊ก Is Trigger (ขนาดเท่าห้อง)
    ///   3. ลาก Corpse GameObjects ใส่ช่อง corpses[] (ปิดไว้ก่อน)
    ///   4. ลาก Light ของห้องใส่ช่อง roomLight
    ///   5. ลาก AudioSource ใส่ช่อง audioSource
    ///   6. (ตัวเลือก) ลาก HallucinationCutscene
    /// </summary>
    public class DeadRoom : MonoBehaviour
    {
        [Header("Corpses")]
        [Tooltip("List ของร่างคนตายใน Scene (เริ่มต้นปิดไว้)")]
        public List<GameObject> corpses = new List<GameObject>();

        [Tooltip("delay ระหว่าง corpse แต่ละตัวที่ activate (วินาที)")]
        public float corpseRevealDelay = 0.15f;

        [Header("Lighting")]
        [Tooltip("Light ของห้อง (จะกะพริบตอนผู้เล่นเข้ามา)")]
        public Light roomLight;

        [Tooltip("จำนวนครั้งกะพริบก่อนไฟติดเต็ม")]
        public int flickerCount = 5;

        [Tooltip("ความเข้มไฟปกติ")]
        public float normalIntensity = 1f;

        [Tooltip("เปิดไฟค้างหลังจาก flicker")]
        public bool keepLightOn = true;

        [Header("Ambient Audio")]
        public AudioSource audioSource;

        [Tooltip("เสียง ambient ห้อง (เล่น loop ตอนอยู่ในห้อง)")]
        public AudioClip ambientClip;

        [Tooltip("เสียงตอน corpse reveal")]
        public AudioClip revealSound;

        [Header("PTSD Trigger")]
        [Tooltip("เรียก PTSD Symptom เมื่อผู้เล่นอยู่ในห้องนานเกินกี่วินาที (0 = ทันที)")]
        public float ptsdTriggerDelay = 5f;

        [Tooltip("HallucinationCutscene ที่จะเรียก (ตัวเลือก)")]
        public HallucinationCutscene hallucination;

        [Header("Settings")]
        [Tooltip("trigger ได้กี่ครั้ง")]
        public int maxEnterCount = 1;

        // ─── Private ───────────────────────────────
        private int  _enterCount = 0;
        private bool _playerInside = false;
        private bool _revealed = false;
        private Coroutine _ptsdCoroutine;

        void Start()
        {
            // ซ่อนร่างทั้งหมดตอนเริ่ม
            foreach (var c in corpses)
                if (c != null) c.SetActive(false);

            if (roomLight != null) roomLight.intensity = 0f;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (_enterCount >= maxEnterCount && maxEnterCount >= 0) return;

            _enterCount++;
            _playerInside = true;

            Debug.Log("💀 [DeadRoom] ผู้เล่นเข้าห้องคนตาย");

            if (!_revealed) StartCoroutine(RevealSequence());

            // เริ่ม PTSD timer
            _ptsdCoroutine = StartCoroutine(PTSDTimer());
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            _playerInside = false;

            // ยกเลิก PTSD timer ถ้าออกจากห้อง
            if (_ptsdCoroutine != null)
            {
                StopCoroutine(_ptsdCoroutine);
                _ptsdCoroutine = null;
            }

            // หยุดเสียง ambient
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        // ──────────────────────────────────────────
        //  Reveal Sequence
        // ──────────────────────────────────────────
        IEnumerator RevealSequence()
        {
            _revealed = true;

            // 1. ไฟกะพริบ
            if (roomLight != null)
                yield return StartCoroutine(FlickerLight());

            // 2. เสียง reveal
            if (audioSource != null && revealSound != null)
                audioSource.PlayOneShot(revealSound);

            // 3. activate corpse ทีละตัว
            foreach (var corpse in corpses)
            {
                if (corpse != null)
                {
                    corpse.SetActive(true);

                    // ถ้า corpse มี GhostAppearance ให้ fade in
                    GhostAppearance ga = corpse.GetComponent<GhostAppearance>();
                    if (ga != null) StartCoroutine(ga.FadeIn(0.3f));
                }
                yield return new WaitForSeconds(corpseRevealDelay);
            }

            // 4. เล่นเสียง ambient loop
            if (audioSource != null && ambientClip != null)
            {
                audioSource.clip = ambientClip;
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        IEnumerator FlickerLight()
        {
            if (roomLight == null) yield break;

            for (int i = 0; i < flickerCount; i++)
            {
                roomLight.intensity = normalIntensity;
                yield return new WaitForSeconds(Random.Range(0.05f, 0.12f));
                roomLight.intensity = 0f;
                yield return new WaitForSeconds(Random.Range(0.05f, 0.18f));
            }

            if (keepLightOn) roomLight.intensity = normalIntensity;
        }

        // ──────────────────────────────────────────
        //  PTSD Timer
        // ──────────────────────────────────────────
        IEnumerator PTSDTimer()
        {
            yield return new WaitForSeconds(ptsdTriggerDelay);

            if (!_playerInside) yield break;

            Debug.Log("💀 [DeadRoom] ผู้เล่นอยู่ในห้องนานเกินไป → PTSD อาการกำเริบ");

            if (PTSDSymptomManager.Instance != null)
                PTSDSymptomManager.Instance.TriggerSymptom();

            if (hallucination != null)
            {
                yield return new WaitForSeconds(0.5f);
                hallucination.TriggerCutscene();
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0f, 0f, 0.2f);
            Collider col = GetComponent<Collider>();
            if (col != null)
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
