using UnityEngine;

namespace HorrorGame
{
    /// <summary>
    /// วาง Script นี้ไว้ที่ Trigger Collider (isTrigger = true)
    /// เมื่อผู้เล่นเดินเข้ามา จะเรียก HallucinationCutscene ให้เล่นอัตโนมัติ
    /// 
    /// วิธีใช้:
    /// 1. สร้าง GameObject → Add Collider → ติ๊ก Is Trigger
    /// 2. วาง Script นี้ไว้บน GameObject นั้น
    /// 3. ลาก HallucinationCutscene ใส่ช่อง cutscene
    /// 4. วาง Trigger Zone ตรงหน้าเก้าอี้/ศพ
    /// </summary>
    public class HallucinationTriggerZone : MonoBehaviour
    {
        [Tooltip("Cutscene ที่จะเล่นเมื่อผู้เล่นเดินผ่าน")]
        public HallucinationCutscene cutscene;

        [Tooltip("Tag ของผู้เล่น (ปกติคือ Player)")]
        public string playerTag = "Player";

        [Tooltip("แสดง Gizmo สีเหลืองเพื่อให้เห็นในหน้าต่าง Scene")]
        public bool showGizmo = true;

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            if (cutscene == null)
            {
                Debug.LogWarning("⚠️ [HallucinationTriggerZone] ยังไม่ได้ลาก HallucinationCutscene ใส่ช่อง cutscene!");
                return;
            }

            Debug.Log("🚶 [HallucinationTriggerZone] ผู้เล่นเดินเข้า Zone → เรียก Cutscene");
            cutscene.TriggerCutscene();
        }

        void OnDrawGizmos()
        {
            if (!showGizmo) return;
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            Collider col = GetComponent<Collider>();
            if (col != null)
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
