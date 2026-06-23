using UnityEngine;

namespace HorrorGame
{
    /// <summary>
    /// BathtubRoomTrigger — One-shot event น้ำในอ่าง (Doorway trigger)
    ///
    /// วาง Trigger บางๆ ที่ขอบประตูห้องน้ำ
    /// ทุกครั้งที่ผู้เล่นผ่านประตู → Enter + Exit จะ fire ทั้งคู่
    ///
    ///   ผ่านเข้าห้อง (crossing 1): Exit ครั้งที่ 1 → ยังไม่ทำอะไร
    ///   ผ่านออกห้อง (crossing 2): Exit ครั้งที่ 2 → น้ำหาย จบ event
    ///
    /// Setup ใน Unity Editor:
    ///   1. สร้าง Empty GameObject วางที่ขอบประตูห้องน้ำ
    ///   2. Add Component → Box Collider → ติ๊ก "Is Trigger"
    ///      Size: กว้างเท่าช่องประตู, บางๆ (Z ประมาณ 0.2)
    ///   3. Add Component → BathtubRoomTrigger (script นี้)
    ///   4. ลาก GameObject ผิวน้ำ ใส่ช่อง "waterObject"
    ///      → เปิด (SetActive true) ไว้ใน Inspector
    ///   5. ผู้เล่นต้องมี Tag "Player"
    /// </summary>
    public class BathtubRoomTrigger : MonoBehaviour
    {
        [Header("Water")]
        [Tooltip("GameObject ผิวน้ำในอ่าง\nต้องเปิด (SetActive = true) ไว้ใน Inspector")]
        public GameObject waterObject;

        // ─── Private ───────────────────────────────────────────
        private int  _exitCount = 0;   // นับจำนวนครั้งที่ผ่านขอบประตู
        private bool _done      = false;

        // ──────────────────────────────────────────────────────
        void Start()
        {
            if (waterObject == null)
                Debug.LogWarning("⚠️ [BathtubRoomTrigger] ยังไม่ได้ลาก waterObject ใส่ใน Inspector");

            Collider col = GetComponent<Collider>();
            if (col == null || !col.isTrigger)
                Debug.LogWarning("⚠️ [BathtubRoomTrigger] โปรดเพิ่ม Collider และติ๊ก Is Trigger");
        }

        void OnTriggerExit(Collider other)
        {
            if (_done) return;
            if (!other.CompareTag("Player")) return;

            _exitCount++;
            Debug.Log("🚪 [BathtubRoomTrigger] Exit ครั้งที่ " + _exitCount);

            if (_exitCount == 1)
            {
                // ผ่านเข้าห้อง — น้ำยังอยู่
                Debug.Log("🛁 [BathtubRoomTrigger] ผู้เล่นเข้าห้อง — น้ำยังอยู่");
            }
            else if (_exitCount == 2)
            {
                // ผ่านออกห้อง — น้ำหาย
                if (waterObject != null)
                    waterObject.SetActive(false);

                _done = true;
                Debug.Log("🛁 [BathtubRoomTrigger] ผู้เล่นออกห้อง — น้ำหาย (จบ event)");
            }
        }

        // ─── Gizmos ────────────────────────────────────────────
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.5f, 1f, 0.15f);
            Collider col = GetComponent<Collider>();
            if (col != null) Gizmos.DrawCube(col.bounds.center, col.bounds.size);

            Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.6f);
            if (col != null) Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
