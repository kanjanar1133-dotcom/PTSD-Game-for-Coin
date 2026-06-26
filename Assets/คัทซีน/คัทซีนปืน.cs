using UnityEngine;
using UnityEngine.Playables; // เรียกใช้งาน Timeline

public class ItemTrigger : MonoBehaviour
{
    [Header("Cutscene Timeline")]
    public PlayableDirector cutsceneDirector; // ลาก Timeline Cutscene มาใส่ตรงนี้

    private void OnTriggerEnter(Collider other)
    {
        // เช็คว่าตัวละคร (Tag ชื่อ Player) เดินมาเก็บไอเทมหรือไม่
        if (other.CompareTag("Player"))
        {
            // 1. เริ่มเล่น Cutscene
            if (cutsceneDirector != null)
            {
                cutsceneDirector.Play();
            }

            // 2. ซ่อนหรือทำลายไอเทมชิ้นนี้ทันที
            Destroy(gameObject);
        }
    }
}
