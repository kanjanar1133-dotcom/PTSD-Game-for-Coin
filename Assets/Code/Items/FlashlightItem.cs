using UnityEngine;

namespace HorrorGame
{
    /// <summary>
    /// ไอเทมไฟฉาย/ตะเกียง ที่สามารถเก็บได้
    /// นำสคริปต์นี้ไปแปะที่ Root GameObject ของไอเทมไฟฉายในฉาก
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FlashlightItem : MonoBehaviour
    {
        [Header("Glow Settings")]
        public bool enableGlow = true;
        public Color glowColor = Color.white;
        public float glowIntensity = 1.0f;
        public float glowRange = 3f;
        
        private Light glowLight;

        [Header("Events")]
        public UnityEngine.Events.UnityEvent onPickup; // Event เมื่อถูกเก็บ

        public void OnPickup()
        {
            onPickup?.Invoke();
        }

        void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null && col.isTrigger)
            {
                Debug.LogWarning($"⚠️ [FlashlightItem] '{name}' Collider เป็น isTrigger=true — Raycast จะโดนไม่ได้! ให้ปิด isTrigger");
            }

            if (enableGlow) SetupGlowLight();
        }

        void SetupGlowLight()
        {
            glowLight = GetComponentInChildren<Light>();
            if (glowLight == null)
            {
                GameObject lightObj = new GameObject("FlashlightGlowLight");
                lightObj.transform.SetParent(transform);
                lightObj.transform.localPosition = Vector3.up * 0.1f;
                glowLight = lightObj.AddComponent<Light>();
                glowLight.type = LightType.Point;
            }
            glowLight.color = glowColor;
            glowLight.intensity = glowIntensity;
            glowLight.range = glowRange;
        }
    }
}
