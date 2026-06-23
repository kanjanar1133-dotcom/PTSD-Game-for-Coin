using UnityEngine;

namespace HorrorGame
{
    /// <summary>
    /// แนบ script นี้ไว้ที่ Root GameObject ของสิ่งของกุญแจ
    /// 
    /// ✅ Checklist สำหรับตั้งค่าใน Inspector:
    ///   1. Root GameObject ต้องมี Collider (BoxCollider / MeshCollider) ที่ isTrigger = FALSE
    ///   2. ถ้าโมเดล 3D อยู่ใน Child ให้แน่ใจว่า MeshRenderer enabled = true
    ///   3. Layer ของ object ต้องไม่ถูก ignoreLayer ของ PlayerInteraction กรอง
    ///   4. keyID ต้องตรงกับ LockedDoor.requiredKeyID
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class KeyItem : MonoBehaviour
    {
        [Tooltip("ต้องตรงกับ LockedDoor.requiredKeyID เสมอ (case-sensitive)")]
        public string keyID = "Room1";

        [Header("Glow Settings")]
        [Tooltip("เพิ่มแสงสว่างให้กุญแจเพื่อให้หาเจอได้ง่ายขึ้น")]
        public bool enableGlow = true;
        public Color glowColor = Color.yellow;
        public float glowIntensity = 1.5f;
        public float glowRange = 2f;
        
        private Light glowLight;

        void Awake()
        {
            ValidateSetup();
            if (enableGlow) SetupGlowLight();
        }

        void SetupGlowLight()
        {
            glowLight = GetComponentInChildren<Light>();
            if (glowLight == null)
            {
                GameObject lightObj = new GameObject("KeyGlowLight");
                lightObj.transform.SetParent(transform);
                lightObj.transform.localPosition = Vector3.up * 0.1f;
                glowLight = lightObj.AddComponent<Light>();
                glowLight.type = LightType.Point;
            }
            glowLight.color = glowColor;
            glowLight.intensity = glowIntensity;
            glowLight.range = glowRange;
        }

        void ValidateSetup()
        {
            // เช็ค Collider
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                Debug.LogError($"❌ [KeyItem] '{name}' ไม่มี Collider! เพิ่ม BoxCollider หรือ MeshCollider ที่ isTrigger=false");
                return;
            }
            if (col.isTrigger)
            {
                Debug.LogWarning($"⚠️ [KeyItem] '{name}' Collider เป็น isTrigger=true — Raycast จะโดนไม่ได้! ให้ปิด isTrigger");
            }

            // เช็ค Renderer (รวม child)
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend == null)
            {
                Debug.LogWarning($"⚠️ [KeyItem] '{name}' ไม่พบ Renderer ใน GameObject หรือ Child — โมเดลอาจไม่แสดงผล");
            }
            else if (!rend.enabled)
            {
                Debug.LogWarning($"⚠️ [KeyItem] '{name}' Renderer ถูกปิดอยู่ (enabled=false)");
            }

            // เช็ค keyID ว่าไม่ว่าง
            if (string.IsNullOrEmpty(keyID))
            {
                Debug.LogError($"❌ [KeyItem] '{name}' keyID ว่างอยู่! ต้องตั้งค่าให้ตรงกับ LockedDoor");
            }
            else
            {
                Debug.Log($"🔑 [KeyItem] '{name}' พร้อมใช้งาน (keyID = '{keyID}')");
            }
        }

#if UNITY_EDITOR
        // แสดง Gizmo ตำแหน่ง key ใน Scene view
        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.2f);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"🔑 {keyID}");
        }
#endif
    }
}
