using UnityEngine;

namespace HorrorGame
{
    /// <summary>
    /// HauntedDoor_Type1 — ประตูหลอนแบบปิดสนิท
    ///
    /// Sequence:
    ///   ประตูปรากฏ (fade in) → ผู้เล่นเดินมาเห็น → ลองเปิด (ไม่ได้)
    ///   → หันออก (ประตูออกจาก FOV) → ประตูหายไปเงียบๆ
    ///
    /// Setup Prefab:
    ///   DoorRoot (HauntedDoor_Type1)
    ///     └─ DoorMesh  [MeshRenderer + Collider]
    ///     └─ DoorFrame [MeshRenderer]  (ตัวเลือก)
    ///   + AudioSource บน DoorRoot
    /// </summary>
    public class HauntedDoor_Type1 : HauntedDoorBase
    {
        // Type 1 ไม่มี logic เพิ่มเติม — ทุกอย่างอยู่ใน HauntedDoorBase
        // CanVanish() คืน true เสมอ → หายทันทีที่ออกจาก FOV
    }
}
