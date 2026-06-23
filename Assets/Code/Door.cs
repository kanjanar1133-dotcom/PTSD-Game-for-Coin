using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // <- สำคัญ: เอาไว้ดึง New Input System

/*
 * ==========================================
 * สคริปต์ Door.cs ฉบับอัปเดต (New Input System)
 * ==========================================
 * วิธีใช้งาน Door (ประตูสำหรับวาร์ปผู้เล่นข้ามจุด):
 * 1. สร้างจุดทางเข้า เช่น 3D Object > Cube แล้วลากสคริปต์ Door.cs ใส่กล่องนี้
 * 2. ที่ Component "Box Collider" ของกล่อง ต้องติ๊กถูกที่คำว่า "Is Trigger" ด้วย
 * 3. สร้างก้อน Empty GameObject ทิ้งไว้ในฉากตรงจุดที่อยากให้ผู้เล่นตกลงไปโผล่ (ตั้งชื่อเช่น ExitPoint)
 * 4. กลับมาคลิกที่กล่องประตูในหน้าสมมติ แถบ Inspector ให้ลาก ExitPoint มาหย่อนใส่ในช่อง "Destination"
 * 5. ผู้เล่นต้องมี Tag "Player" ถึงจะเข้าประตูนี้ได้ (แก้ใน Inspector ที่ตัวผู้เล่น)
 * 6. ช่อง 'Interact Key Path' ใช้ตั้งว่าต้องการให้กดปุ่มไหนเวลาวาร์ป (ค่าเริ่มต้น: <Keyboard>/e คือกด E)
 */

public class Door : MonoBehaviour
{
    [Header("Door Settings")]
    [Tooltip("ลาก Transform ของจุดหมายปลายทางมาใส่ช่องนี้")]
    public Transform destination;
    
    [Tooltip("ปุ่มที่กดบนคีย์บอร์ด (รูปแบบ New Input System เช่น <Keyboard>/e)")]
    public string interactKeyPath = "<Keyboard>/e";

    [Header("Lock Settings")]
    [Tooltip("หากติ๊กถูก ประตูจะถูกล็อกไว้ ไม่สามารถใช้งานและไม่ขึ้นปุ่มกด (เอาไว้ให้สคริปต์อื่นมาปลดล็อก เช่น ท่อหล่น)")]
    public bool isLocked = false;

    [Header("Scene Transition Settings")]
    [Tooltip("ติ๊กถูกถ้าต้องการให้ประตูนี้วาร์ปข้ามไป Scene ใหม่")]
    public bool isSceneTransition = false;
    [Tooltip("ชื่อ Scene ที่ต้องการโหลด (ถ้าติ๊กถูกที่ Is Scene Transition)")]
    public string sceneToLoad;

    // ปุ่มของระบบใหม่
    private InputAction interactAction;

    private bool isPlayerNear = false;
    private GameObject playerRef;

    private void Awake()
    {
        // สร้างการอ้างอิงให้ปุ่มแบบโค้ดล้วนๆ
        interactAction = new InputAction("Interact", binding: interactKeyPath);
        interactAction.Enable();
    }

    private void OnDestroy()
    {
        // ปิดปุ่มไว้เวลาที่เปลี่ยนหน้าจอป้องกันบัค
        interactAction.Disable();
    }

    private void Update()
    {
        // ใช้ WasPressedThisFrame ป้องกันบั๊กกดไม่ติด
        if (isPlayerNear && interactAction.WasPressedThisFrame() && playerRef != null)
        {
            if (isLocked)
            {
                Debug.Log("[Door Debug] ประตูยังล็อกอยู่ เข้าไม่ได้!");
                return;
            }

            Movement mScript = playerRef.GetComponentInParent<Movement>();
            // ป้องกันตีกัน: ถ้าผู้เล่นขยับไม่ได้ (สคริปต์ปิดอยู่) หรือซ่อนตัวอยู่ ห้ามกดประตู!
            if (mScript != null && (!mScript.enabled || mScript.IsHiding())) return;

            Debug.Log("[Door Debug] ผู้เล่นกดปุ่มเข้าประตูแล้ว กำลังเตรียมความพร้อม...");
            
            if (destination != null || isSceneTransition)
            {
                // ล็อกการเดินและล็อกไม่ให้กดปุ่มซ้ำขณะจอกำลังเฟดมืด
                if (mScript != null) mScript.SetMovementLock(true);
                TeleportWithFade();
            }
            else
            {
                Debug.LogError("[Door Debug Error] ไม่สามารถเข้าประตูได้! ตรวจสอบว่าใส่ Destination แล้วหรือยัง? และตัวผู้เล่นมี Tag 'Player' หรือไม่");
            }
        }
    }

    private void TeleportWithFade()
    {
        // ⚠️ โคตรสำคัญ: ล้างค่าทันทีเมื่อกด E ไปแล้ว เพื่อป้องกันบั๊ก "ประตูล็อกเป้าผู้เล่น" 
        // เนื่องจากเวลาวาร์ป CharacterController จะถูกปิด ทำให้ OnTriggerExit ไม่ทำงาน
        isPlayerNear = false;
        GameObject currentPlayer = playerRef;
        playerRef = null;

        if (isSceneTransition)
        {
            if (string.IsNullOrEmpty(sceneToLoad))
            {
                Debug.LogError("[Door Debug Error] ไม่สามารถโหลด Scene ได้ เพราะไม่ได้ระบุชื่อ Scene ในช่อง Scene To Load");
                return;
            }

            Debug.Log($"[Door Debug] เรียกใช้งาน FadeManager เพื่อโหลด Scene: {sceneToLoad}");
            FadeManager.Instance.FadeAndLoadScene(sceneToLoad);
        }
        else
        {
            Debug.Log("[Door Debug] เรียกใช้งาน FadeManager ให้เริ่มทำจอมืด (วาร์ปในฉาก)...");

            FadeManager.Instance.FadeAndExecute(() =>
            {
                if (currentPlayer == null) return;

                Debug.Log($"[Door Debug] กำลังเคลื่อนย้ายผู้เล่นจาก {currentPlayer.transform.position} ไปยัง {destination.position}");
                
                Movement mScript = currentPlayer.GetComponent<Movement>();
                if (mScript != null)
                {
                    // เรียกใช้ฟังก์ชันใน Movement เพื่อจัดมุมกล้องให้มองตรงเสมอ
                    mScript.TeleportTo(destination.position, destination.rotation);
                }
                else
                {
                    // โค้ดสำรองเผื่อไม่มีสคริปต์ Movement
                    CharacterController cc = currentPlayer.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    
                    currentPlayer.transform.position = destination.position;
                    currentPlayer.transform.rotation = destination.rotation;
                    
                    Physics.SyncTransforms();
                    if (cc != null) cc.enabled = true;
                }

                Debug.Log($"[Door Debug] ย้ายเสร็จสิ้น! ตอนนี้ผู้เล่นอยู่ที่ {currentPlayer.transform.position}");
            });
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Movement mScript = other.GetComponentInParent<Movement>();
        // เช็ค Tag หรือ เช็คหา Component Movement เผื่อลืมตั้ง Tag
        if (other.CompareTag("Player") || mScript != null)
        {
            isPlayerNear = true;
            // สำคัญ: ต้องยึดตัวแม่ที่มีสคริปต์ Movement เป็นหลัก ป้องกันการย้ายแค่ชิ้นส่วนลูก
            playerRef = mScript != null ? mScript.gameObject : other.gameObject;
            Debug.Log("[Door Debug] Player โผล่เข้ามาในระยะทำการของประตูแล้ว! (โชว์ปุ่มให้กด)");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Movement mScript = other.GetComponentInParent<Movement>();
        if (other.CompareTag("Player") || mScript != null)
        {
            GameObject exitedObj = mScript != null ? mScript.gameObject : other.gameObject;
            // เช็คว่าเป็นตัวเดียวกับที่เข้ามาตอนแรกไหม
            if (playerRef == exitedObj)
            {
                isPlayerNear = false;
                playerRef = null;
                Debug.Log("[Door Debug] Player เดินออกนอกระยะประตูไปแล้ว! (ซ่อนปุ่ม)");
            }
        }
    }

    private void OnGUI()
    {
        if (isPlayerNear && !isLocked)
        {
            bool canShow = true;
            if (playerRef != null)
            {
                Movement mScript = playerRef.GetComponentInParent<Movement>();
                if (mScript != null && (!mScript.enabled || mScript.IsHiding()))
                {
                    canShow = false;
                }
            }

            if (canShow)
            {
                // กำหนดรูปแบบตัวหนังสือ
                GUIStyle style = new GUIStyle();
                style.fontSize = 28;
                style.normal.textColor = Color.white;
                style.alignment = TextAnchor.MiddleCenter;
                
                // วาดข้อความไว้ตรงกลางจอ ค่อนลงล่างนิดหน่อย
                GUI.Label(new Rect(0, Screen.height / 2f + 50f, Screen.width, 50f), "Press [E]", style);
            }
        }
    }

    // ฟังก์ชันสำหรับปลดล็อกประตูให้ใช้งานได้ (เช่น เรียกตอนท่อหล่นลงมาแล้ว)
    public void UnlockDoor()
    {
        isLocked = false;
        Debug.Log("[Door Debug] ประตูถูกปลดล็อกแล้ว! ตอนนี้สามารถกดวาร์ปได้");
    }
}
