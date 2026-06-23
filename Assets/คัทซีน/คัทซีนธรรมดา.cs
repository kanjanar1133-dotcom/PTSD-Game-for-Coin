using UnityEngine;
using UnityEngine.Playables;

public class SimpleTimelineTrigger : MonoBehaviour
{
    [Header("Timeline Setup")]
    public PlayableDirector timeline;

    void Start()
    {
        if (timeline != null)
        {
            timeline.playOnAwake = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // ตัดการเช็ค Tag Player ออก เพื่อให้ทำงานง่ายขึ้นชัวร์ๆ
        if (timeline != null)
        {
            timeline.Play(); // สั่ง Timeline เล่นทันที
        }

        // สั่งลบตัวบล็อกดักนี้ทิ้งทันทีหลังชน เพื่อให้บล็อกหายไปจากหน้าต่าง Hierarchy เลย
        Destroy(gameObject);
    }
}
