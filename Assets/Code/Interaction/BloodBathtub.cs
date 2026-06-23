using UnityEngine;
using System.Collections;

namespace HorrorGame
{
    /// <summary>
    /// BloodBathtub — อ่างน้ำเลือด
    ///
    /// เมื่อผู้เล่นเข้าใกล้และกด E จะเกิด PTSD hallucination
    ///
    /// Setup:
    ///   1. วาง Script นี้บน GameObject อ่างอาบน้ำ
    ///   2. เพิ่ม Collider เป็น trigger (หรือ non-trigger ก็ได้)
    ///   3. ลาก HallucinationCutscene ใส่ช่อง hallucination (ถ้ามีผี)
    ///   4. ลาก AudioSource + clip เสียงหยดเลือด
    ///   5. (ตัวเลือก) ลาก Particle System หยดเลือด
    ///
    /// Script นี้ implement IInteractable ผ่านการตรวจสอบใน PlayerInteraction
    /// โดยการ Add Component นี้บนวัตถุที่มี Collider
    /// </summary>
    public class BloodBathtub : MonoBehaviour
    {
        [Header("Interaction")]
        [Tooltip("ระยะสูงสุดที่ผู้เล่นจะเห็น prompt")]
        public float interactDistance = 3f;

        [Header("Hallucination")]
        [Tooltip("HallucinationCutscene ที่เรียกตอน interact (ถ้าไม่ใส่จะใช้ PTSD Symptom แทน)")]
        public HallucinationCutscene hallucination;

        [Tooltip("ใช้ PTSD Symptom ด้วย (นอกเหนือจาก hallucination)")]
        public bool triggerPTSDSymptom = true;

        [Tooltip("trigger ได้กี่ครั้ง (-1 = ไม่จำกัด)")]
        public int maxTriggerCount = 2;

        [Header("Ambient Blood Drip Audio")]
        [Tooltip("AudioSource สำหรับเสียง ambient หยดเลือด (3D)")]
        public AudioSource ambientAudioSource;

        [Tooltip("เสียงหยดเลือดที่เล่นตลอดเวลา (loop)")]
        public AudioClip bloodDripLoop;

        [Tooltip("เสียงพิเศษตอน interact")]
        public AudioClip interactSound;

        [Header("Particles")]
        [Tooltip("Particle System หยดเลือด (ตัวเลือก)")]
        public ParticleSystem bloodParticles;

        [Header("Light Flicker")]
        [Tooltip("Light ที่จะกะพริบตอนผู้เล่นอยู่ใกล้ (ตัวเลือก)")]
        public Light flickerLight;
        public float flickerMinIntensity = 0.2f;
        public float flickerMaxIntensity = 2f;
        public float flickerSpeed = 12f;

        // ─── Private ───────────────────────────────
        private int   _triggerCount = 0;
        private bool  _isTriggering = false;
        private bool  _playerNearby = false;

        void Start()
        {
            // เล่นเสียง ambient ถ้ามี
            if (ambientAudioSource != null && bloodDripLoop != null)
            {
                ambientAudioSource.clip   = bloodDripLoop;
                ambientAudioSource.loop   = true;
                ambientAudioSource.Play();
            }

            // เริ่ม particle
            if (bloodParticles != null) bloodParticles.Play();
        }

        void Update()
        {
            // กะพริบไฟตอนผู้เล่นอยู่ใกล้
            if (flickerLight != null && _playerNearby)
            {
                float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f);
                flickerLight.intensity = Mathf.Lerp(flickerMinIntensity, flickerMaxIntensity, noise);
            }
        }

        /// <summary>
        /// เรียกจาก PlayerInteraction เมื่อกด E บนอ่าง
        /// </summary>
        public void Interact()
        {
            if (_isTriggering) return;
            if (maxTriggerCount >= 0 && _triggerCount >= maxTriggerCount) return;

            _triggerCount++;
            StartCoroutine(InteractSequence());
        }

        IEnumerator InteractSequence()
        {
            _isTriggering = true;
            Debug.Log("🛁 [BloodBathtub] ผู้เล่นมองลงในอ่างเลือด...");

            // เสียง interact
            if (ambientAudioSource != null && interactSound != null)
                ambientAudioSource.PlayOneShot(interactSound);

            yield return new WaitForSeconds(0.3f);

            // PTSD Symptom
            if (triggerPTSDSymptom && PTSDSymptomManager.Instance != null)
                PTSDSymptomManager.Instance.TriggerSymptom();

            // Hallucination
            if (hallucination != null)
            {
                yield return new WaitForSeconds(0.5f);
                hallucination.TriggerCutscene();
            }

            yield return new WaitForSeconds(2f);
            _isTriggering = false;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
                _playerNearby = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
                _playerNearby = false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, interactDistance);
        }
    }
}
