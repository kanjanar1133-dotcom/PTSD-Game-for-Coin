using UnityEngine;

namespace HorrorGame
{
    /// <summary>
    /// ใช้คู่กับ Box Collider (ที่เปิด Is Trigger)
    /// เมื่อผู้เล่นเดินชนเขต ทริกเกอร์นี้จะสั่งเริ่มเล่นคัตซีนใน CutsceneController
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NewCutsceneTrigger : MonoBehaviour
    {
        [Header("Controller Link")]
        [Tooltip("CutsceneController ที่ใช้ควบคุมคัตซีนนี้")]
        public CutsceneController cutsceneController;

        [Header("Trigger Settings")]
        [Tooltip("ต้องการให้เล่นคัตซีนนี้เพียงครั้งเดียวหรือไม่")]
        public bool playOnce = true;

        private bool _hasTriggered = false;

        void Start()
        {
            // ตรวจสอบ Collider เบื้องต้นเพื่อให้แน่ใจว่าเป็น Trigger
            Collider col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning($"⚠️ [NewCutsceneTrigger] Collider บน GameObject '{gameObject.name}' ไม่ได้ตั้งค่าเป็น Is Trigger! ระบบเปิดให้อัตโนมัติแล้ว");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // ป้องกันการทำงานซ้ำหากกดให้ทำครั้งเดียว
            if (_hasTriggered && playOnce) return;

            // ตรวจสอบว่าเป็นผู้เล่น (เช็คทั้ง Tag 'Player' หรือเช็คตัวแปร Player ที่ลากใส่ใน Controller เพื่อความชัวร์สูงสุด)
            bool isPlayer = other.CompareTag("Player");
            if (!isPlayer && cutsceneController != null && cutsceneController.player != null)
            {
                // หากไม่ได้ตั้ง Tag แต่เป็น GameObject เดียวกับที่ลากใส่ช่อง Player
                isPlayer = (other.gameObject == cutsceneController.player);
            }

            if (isPlayer)
            {
                _hasTriggered = true;
                Debug.Log($"🎯 [NewCutsceneTrigger] ผู้เล่นเข้าชน Box Trigger ของ '{gameObject.name}'");

                if (cutsceneController != null)
                {
                    cutsceneController.StartCutscene();
                }
                else
                {
                    Debug.LogError($"❌ [NewCutsceneTrigger] บน '{gameObject.name}' ไม่มีการใส่ Reference ของ CutsceneController!");
                }

                // หากตั้งค่าให้เล่นครั้งเดียว ให้ทำการปิดการทำงานสคริปต์และตัวชนเพื่อประสิทธิภาพที่ดี
                if (playOnce)
                {
                    Collider col = GetComponent<Collider>();
                    if (col != null)
                    {
                        col.enabled = false;
                    }
                    enabled = false;
                }
            }
        }
    }
}
