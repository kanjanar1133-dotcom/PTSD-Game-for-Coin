using UnityEngine;

namespace HorrorGame
{
    /// <summary>
    /// PTSDGun — ปืนวางพื้น เดินไปเก็บได้
    /// เมื่อผู้เล่นเก็บปืน → เรียก PTSDSymptomManager.TriggerSymptom()
    ///
    /// Setup:
    ///   1. วาง Script นี้บน GameObject ปืน
    ///   2. ให้ GameObject ปืนมี Collider (ไม่ใช่ trigger)
    ///   3. ตั้งค่า Audio, PTSDSymptomManager จะจัดการ symptom เอง
    ///   4. (ตัวเลือก) ลาก PlayerHands ใส่ช่อง playerHands เพื่อ trigger animation หยิบ
    ///
    /// การทำงาน:
    ///   - เมื่อผู้เล่น Raycast โดนปืนและกด E → เก็บปืน
    ///   - เรียก PTSD Symptom ทันที
    ///   - (ตัวเลือก) เพิ่มปืนใน Inventory
    /// </summary>
    public class PTSDGun : MonoBehaviour
    {
        [Header("Gun Info")]
        [Tooltip("ชื่อปืนที่แสดงใน Inventory")]
        public string gunName = "Pistol";

        [Tooltip("ID สำหรับ Inventory")]
        public string gunID = "ptsd_gun";

        [Header("Visuals")]
        [Tooltip("แสดงแสงกะพริบรอบปืน (ดึงดูดความสนใจ)")]
        public Light glowLight;

        [Tooltip("ความเร็วการกะพริบของแสง")]
        public float glowPulseSpeed = 2f;

        [Tooltip("ความเข้มแสง min/max")]
        public float glowMinIntensity = 0.2f;
        public float glowMaxIntensity = 1.5f;

        [Header("Hover Rotation (หมุนช้าๆ ขณะวาง)")]
        public bool hoverRotate = true;
        public float rotateSpeed = 45f;   // องศาต่อวินาที
        public float hoverAmplitude = 0.05f;
        public float hoverFrequency = 1f;

        [Header("References")]
        [Tooltip("PlayerHands script — เรียก TriggerPickup animation (ตัวเลือก)")]
        public PlayerHands playerHands;

        // ─── Private ───────────────────────────────
        private float _hoverTimer = 0f;
        private Vector3 _startPos;
        private bool _isPickedUp = false;

        void Start()
        {
            _startPos = transform.position;

            // หา PlayerHands อัตโนมัติ
            if (playerHands == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerHands = player.GetComponentInChildren<PlayerHands>();
            }
        }

        void Update()
        {
            if (_isPickedUp) return;

            // หมุนและลอย
            if (hoverRotate)
            {
                transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
                _hoverTimer += Time.deltaTime;
                float yOffset = Mathf.Sin(_hoverTimer * hoverFrequency * Mathf.PI * 2f) * hoverAmplitude;
                transform.position = _startPos + Vector3.up * yOffset;
            }

            // กะพริบแสง
            if (glowLight != null)
            {
                float pulse = (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f;
                glowLight.intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, pulse);
            }
        }

        /// <summary>
        /// เรียกจาก PlayerInteraction ตอนกด E บนปืน
        /// </summary>
        public void PickUp(PlayerInteraction playerInteraction)
        {
            if (_isPickedUp) return;
            _isPickedUp = true;

            Debug.Log("🔫 [PTSDGun] ผู้เล่นเก็บปืน → อาการกำเริบ!");

            // Trigger PTSD Symptom
            if (PTSDSymptomManager.Instance != null)
                PTSDSymptomManager.Instance.TriggerSymptom();
            else
                Debug.LogWarning("⚠️ [PTSDGun] ไม่พบ PTSDSymptomManager ใน Scene!");

            // เล่น pickup animation
            if (playerHands != null)
                playerHands.TriggerPickup();

            // เพิ่มใน Inventory (ถ้าต้องการ)
            if (playerInteraction != null)
            {
                playerInteraction.inventory.Add(new PlayerInteraction.InventoryItem
                {
                    id   = gunID,
                    name = gunName
                });
                playerInteraction.selectedIndex = playerInteraction.inventory.Count;
                playerInteraction.UpdateHandVisuals();
            }

            // ซ่อน GameObject ปืนในโลก
            gameObject.SetActive(false);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
