using UnityEngine;
using UnityEngine.Playables;

namespace HorrorGame
{
    public class GhostRunEvent : MonoBehaviour
    {
        [Header("Timeline Settings")]
        [Tooltip("ลาก PlayableDirector (Timeline) ที่สร้างไว้สำหรับฉากผีวิ่งมาใส่ที่นี่")]
        public PlayableDirector timelineDirector;

        [Header("Event Conditions")]
        [Tooltip("ต้องมีไฟฉายก่อนถึงจะเกิดเหตุการณ์นี้หรือไม่")]
        public bool requireFlashlight = true;
        
        [Tooltip("Trigger ที่ผู้เล่นต้องเดินชน (ตอนเดินออกจากห้อง)")]
        public Collider triggerZone;

        private bool hasTriggered = false;
        private PlayerFlashlight playerFlashlightRef;

        void Update()
        {
            // พยายามหา PlayerFlashlight ถ้ายังไม่มี
            if (requireFlashlight && playerFlashlightRef == null)
            {
                playerFlashlightRef = FindObjectOfType<PlayerFlashlight>();
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (hasTriggered) return;

            // ตรวจสอบว่าเป็นผู้เล่นหรือไม่
            bool isPlayer = other.CompareTag("Player") 
                         || other.GetComponentInParent<Collider>()?.CompareTag("Player") == true
                         || other.GetComponentInParent<Movement>() != null
                         || other.name.ToLower().Contains("player");

            if (isPlayer)
            {
                // ตรวจสอบเงื่อนไขไฟฉาย
                if (requireFlashlight)
                {
                    if (playerFlashlightRef != null && playerFlashlightRef.hasFlashlight)
                    {
                        TriggerTimeline();
                    }
                }
                else
                {
                    TriggerTimeline();
                }
            }
        }

        private void TriggerTimeline()
        {
            hasTriggered = true;
            Debug.Log("👻 [GhostRunEvent] เงื่อนไขครบ! เริ่มเล่น Timeline ผีวิ่งตัดหน้า");

            if (timelineDirector != null)
            {
                timelineDirector.Play();
            }
            else
            {
                Debug.LogWarning("[GhostRunEvent] ไม่ได้ใส่ PlayableDirector ไว้!");
            }

            // ปิด Trigger เพื่อไม่ให้ทำงานซ้ำ
            if (triggerZone != null)
            {
                triggerZone.enabled = false;
            }
            else
            {
                Collider col = GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }

            // ปิดสคริปต์นี้ไปเลยเพราะสั่งเล่น Timeline แล้ว
            this.enabled = false;
        }
    }
}
