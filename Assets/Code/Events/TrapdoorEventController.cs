using UnityEngine;

namespace HorrorGame
{
    public class TrapdoorEventController : MonoBehaviour
    {
        [Header("Trapdoor Settings")]
        [Tooltip("เกมออบเจ็กต์ของ Trapdoor ที่ต้องการให้หายไปตอนแรก")]
        public GameObject trapdoorObject;
        
        [Tooltip("แสงไฟที่อยู่ตรง Trapdoor")]
        public Light trapdoorLight;

        [Header("Room Settings")]
        [Tooltip("ไฟทุกดวงที่ต้องการให้ดับหลังจากเก็บไฟฉาย")]
        public Light[] roomLights;
        
        [Tooltip("จุด Trigger เมื่อเดินออกจากห้อง")]
        public Collider exitRoomTrigger;

        private bool hasFlashlight = false;
        private bool sequenceCompleted = false;
        private PlayerFlashlight playerFlashlightRef;

        void Start()
        {
            // เริ่มเกมมา - Trapdoor หาย ไม่สามารถกดได้
            // ปิดไฟและตั้งค่าเริ่มต้น
            if (trapdoorObject != null) trapdoorObject.SetActive(false);
            if (trapdoorLight != null) trapdoorLight.enabled = false;

            // ไม่ต้องปิด Collider Trigger เพื่อป้องกันปัญหาผู้เล่นยืนทับอยู่ตอนเก็บไฟฉาย
        }

        void Update()
        {
            // ถ้ายังหา PlayerFlashlight ไม่เจอ ให้พยายามหาใหม่
            if (playerFlashlightRef == null)
            {
                playerFlashlightRef = FindObjectOfType<PlayerFlashlight>();
            }

            // ตรวจสอบอัตโนมัติว่าผู้เล่นเก็บไฟฉายไปหรือยัง
            if (!hasFlashlight && playerFlashlightRef != null && playerFlashlightRef.hasFlashlight)
            {
                OnFlashlightPickedUp();
            }
        }

        // นำไปโยงกับ UnityEvent OnPickup ในสคริปต์ FlashlightItem
        public void OnFlashlightPickedUp()
        {
            if (hasFlashlight) return; // ป้องกันการเรียกซ้ำ

            hasFlashlight = true;
            Debug.Log("💡 [TrapdoorEventController] เก็บไฟฉายแล้ว ดับไฟทั้งหมดในห้อง...");

            // ดับไฟทุกดวง
            foreach (Light l in roomLights)
            {
                if (l != null) l.enabled = false;
            }
        }

        // ใช้ OnTriggerStay แทน เผื่อผู้เล่นยืนอยู่ใน Trigger ตอนเก็บไฟฉาย
        private void OnTriggerStay(Collider other)
        {
            if (!sequenceCompleted && hasFlashlight)
            {
                bool isPlayer = other.CompareTag("Player") 
                             || other.GetComponentInParent<Collider>()?.CompareTag("Player") == true
                             || other.GetComponentInParent<Movement>() != null
                             || other.name.ToLower().Contains("player");

                if (isPlayer)
                {
                    Debug.Log($"[TrapdoorEventController] ผู้เล่นอยู่ใน Trigger: {other.name}");
                    ShowTrapdoor();
                }
            }
        }

        // ออกจากห้องไฟฉายมา - ทำให้ Trapdoor โผล่มา พร้อมแสงไฟ
        public void ShowTrapdoor()
        {
            sequenceCompleted = true;
            Debug.Log("🚪 [TrapdoorEventController] เดินออกจากห้องแล้ว! Trapdoor ปรากฏขึ้น");

            if (trapdoorObject != null)
                trapdoorObject.SetActive(true);

            if (trapdoorLight != null)
                trapdoorLight.enabled = true;

            // ปิดตัวเอง
            if (exitRoomTrigger != null)
                exitRoomTrigger.enabled = false;
        }
    }
}
