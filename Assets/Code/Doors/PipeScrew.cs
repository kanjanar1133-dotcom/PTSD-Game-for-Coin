using UnityEngine;

namespace HorrorGame
{
    [RequireComponent(typeof(Rigidbody))]
    public class PipeScrew : MonoBehaviour
    {
        [Tooltip("เสียงตอนไขน็อตออก")]
        public AudioClip unscrewSound;
        private AudioSource audioSource;
        
        [Tooltip("สคริปต์ตัวหลักที่ควบคุมท่อ (ลากจาก Object ตัวแม่มาใส่)")]
        public PipeObstacle mainObstacle;

        void Start()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }

        public void Unscrew()
        {
            if (unscrewSound != null) audioSource.PlayOneShot(unscrewSound);
            
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                // ดันน็อตกระเด็นออกมานิดหน่อย (สมมติว่าน็อตชี้ออกมาทางด้านหน้า)
                rb.AddForce(transform.forward * 2f + transform.up * 1f, ForceMode.Impulse); 
            }
            
            if (mainObstacle != null)
            {
                mainObstacle.RemoveScrew();
            }

            Destroy(this); // ไม่ให้ไขซ้ำได้อีก
            Destroy(gameObject, 5f); // ให้น็อตหายไปหลังจากผ่านไป 5 วินาทีเพื่อไม่ให้รกฉาก
        }
    }
}
