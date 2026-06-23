using UnityEngine;

/*
 * ==========================================
 * สคริปต์สำหรับจัดการอุปสรรค (ฝาท่อที่ถูกน็อตยึดไว้)
 * ==========================================
 * วิธีใช้งาน:
 * 1. แปะสคริปต์นี้ไว้ที่โมเดลของ "ฝาท่อ" (ซึ่งต้องมี Rigidbody ติดอยู่ด้วย และตั้งค่า isKinematic = true)
 * 2. ลากจุดวาร์ปมาใส่ในช่อง Door To Unlock
 * 3. เมื่อผู้เล่นไขน็อต (ด้วยสคริปต์ PipeScrew) มันจะมาเรียก RemoveScrew() ที่นี่
 * 4. เมื่อน็อตครบ ฝาท่อจะหล่นลงมา และมีเสียงดังตอนตกถึงพื้น
 */
[RequireComponent(typeof(Rigidbody))]
public class PipeObstacle : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("จำนวนน็อตทั้งหมดที่ต้องไขออก")]
    public int totalScrews = 4;
    private int removedScrews = 0;

    [Header("References")]
    [Tooltip("จุดวาร์ปที่ต้องการปลดล็อกเมื่อท่อหล่น (ต้องติ๊ก Is Locked ที่ตัว Door ไว้ด้วย)")]
    public Door doorToUnlock;

    [Header("Audio")]
    [Tooltip("เสียงที่จะดังตอนฝาท่อตกลงมากระแทกพื้น")]
    public AudioClip dropSound;
    private AudioSource audioSource;
    private bool hasDropped = false;
    private Rigidbody pipeCoverRigidbody;

    void Start()
    {
        pipeCoverRigidbody = GetComponent<Rigidbody>();
        if (pipeCoverRigidbody != null) pipeCoverRigidbody.isKinematic = true;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
    }

    // ฟังก์ชันนี้ให้สคริปต์อื่น (เช่น PipeScrew) เรียกเมื่อน็อตถูกไขออก 1 ตัว
    public void RemoveScrew()
    {
        removedScrews++;
        Debug.Log($"[PipeObstacle] ถอดน็อตแล้ว {removedScrews}/{totalScrews} ตัว");

        if (removedScrews >= totalScrews && !hasDropped)
        {
            DropPipeAndUnlock();
        }
    }

    private void DropPipeAndUnlock()
    {
        hasDropped = true;

        // 1. ทำให้ฝาท่อหล่นลงมาด้วย Physics
        if (pipeCoverRigidbody != null)
        {
            pipeCoverRigidbody.isKinematic = false; 
            pipeCoverRigidbody.useGravity = true;
            Debug.Log("[PipeObstacle] ฝาท่อหล่นลงมาแล้ว!");
        }

        // 2. ปลดล็อกจุดวาร์ปให้กด E ได้
        if (doorToUnlock != null)
        {
            doorToUnlock.UnlockDoor(); 
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // หากฝาท่อหล่นลงมาแล้ว และกระแทกกับบางสิ่งด้วยความแรงพอประมาณ
        if (hasDropped && dropSound != null)
        {
            if (collision.relativeVelocity.magnitude > 1f)
            {
                audioSource.PlayOneShot(dropSound);
                dropSound = null; // ป้องกันไม่ให้เล่นเสียงกระแทกซ้ำๆ
            }
        }
    }
}
