using UnityEngine;
using UnityEngine.Playables; // จำเป็นต้องใช้สำหรับควบคุม Timeline

public class ItemPickupCutscene : MonoBehaviour
{
    [Header("Settings")]
    public string playerTag = "Player"; // Tag ของตัวละครผู้เล่น
    public PlayableDirector cutsceneTimeline; // ลาก GameObject ที่มี Playable Director มาใส่ที่นี่

    private bool isPlayerInRange = false;

    void Update()
    {
        // ถ้าผู้เล่นอยู่ในระยะ และกดปุ่ม E
        if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
        {
            TriggerPickupAndCutscene();
        }
    }

    void TriggerPickupAndCutscene()
    {
        Debug.Log("เก็บไอเทมแล้ว! กำลังเริ่มคัทซีน...");

        // 1. สั่งให้คัทซีนเล่น
        if (cutsceneTimeline != null)
        {
            cutsceneTimeline.Play();
        }

        // 2. ซ่อนโมเดลไอเทม (แทนการ Destroy ทันที เพื่อไม่ให้สคริปต์พังกลางคัน)
        GetComponent<Collider>().enabled = false; // ปิดการชนกัน

        if (transform.childCount > 0)
        {
            // ถ้ามีโมเดลเป็นลูก ให้สั่งปิดการแสดงผล
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
            }
        }
        else
        {
            // ถ้าไม่มีลูก ให้ปิด MeshRenderer ของตัวเอง
            if (TryGetComponent<MeshRenderer>(out MeshRenderer mesh)) mesh.enabled = false;
        }

        // 3. ทำลายไอเทมทิ้งหลังจากคัทซีนเล่นจบ (ตัวอย่างเช่น ทำลายหลังจากนี้ 5 วินาที หรือตามความยาวคัทซีน)
        // หรือจะลบส่วนนี้ออกแล้วไปทำลายผ่าน Timeline Event ก็ได้
        Destroy(gameObject, (float)cutsceneTimeline.duration);
    }

    // ตรวจจับเมื่อผู้เล่นเดินเข้ามาในระยะ (Trigger)
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            isPlayerInRange = true;
            Debug.Log("กด E เพื่อเก็บไอเทม");
            // ตรงนี้สามารถใส่โค้ดเปิด UI บอกผู้เล่น เช่น "Press E to Interact" ได้
        }
    }

    // ตรวจจับเมื่อผู้เล่นเดินออกจากระยะ
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            isPlayerInRange = false;
            Debug.Log("เดินออกจากระยะไอเทม");
            // ตรงนี้ใส่โค้ดปิด UI บอกผู้เล่น
        }
    }
}
