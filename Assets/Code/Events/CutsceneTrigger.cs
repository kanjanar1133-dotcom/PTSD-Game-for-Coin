using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Events;

/// <summary>
/// CutsceneTrigger - เมื่อผู้เล่นเข้ามาในกล่อง Trigger จะเล่น Cutscene
/// 
/// วิธีใช้:
/// 1. ลาก Script นี้ไปใส่ที่ GameObject ที่มี BoxCollider (Is Trigger = true)
/// 2. ถ้าใช้ Timeline: ลาก PlayableDirector ไปใส่ช่อง "timelineDirector"
/// 3. ถ้าไม่ใช้ Timeline: ลาก Camera ตัวที่ใช้ cutscene ไปใส่ช่อง "cutsceneCamera"
/// 4. ตั้ง Tag ของ Player เป็น "Player"
/// </summary>
public class CutsceneTrigger : MonoBehaviour
{
    [Header("=== Cutscene Settings ===")]
    [Tooltip("เลือกโหมดการเล่น Cutscene")]
    public CutsceneMode cutsceneMode = CutsceneMode.Timeline;

    [Header("--- Timeline Mode ---")]
    [Tooltip("ลาก PlayableDirector (Timeline) มาใส่ที่นี่")]
    public PlayableDirector timelineDirector;

    [Header("--- Simple Camera Mode ---")]
    [Tooltip("กล้อง Cutscene (จะเปิดใช้ตอนเล่น cutscene)")]
    public Camera cutsceneCamera;

    [Tooltip("กล้องหลักของผู้เล่น (จะถูกปิดตอนเล่น cutscene)")]
    public Camera playerCamera;

    [Tooltip("ระยะเวลา cutscene (วินาที) - ใช้เฉพาะ Simple Camera Mode")]
    public float cutsceneDuration = 5f;

    [Header("=== General Options ===")]
    [Tooltip("ทำงานครั้งเดียวเท่านั้น?")]
    public bool triggerOnce = true;

    [Tooltip("ล็อคการเคลื่อนที่ของผู้เล่นตอน cutscene?")]
    public bool disablePlayerMovement = true;

    [Tooltip("Tag ของผู้เล่น")]
    public string playerTag = "Player";

    [Header("=== UI Overlay (Optional) ===")]
    [Tooltip("Canvas/Panel แถบดำด้านบน-ล่าง สำหรับ cinematic feel")]
    public GameObject cinematicBarsUI;

    [Header("=== Events ===")]
    [Tooltip("เรียกเมื่อ cutscene เริ่ม")]
    public UnityEvent onCutsceneStart;

    [Tooltip("เรียกเมื่อ cutscene จบ")]
    public UnityEvent onCutsceneEnd;

    // === Private ===
    private bool hasTriggered = false;
    private bool isPlayingCutscene = false;
    private GameObject playerObject;
    private MonoBehaviour[] playerScripts;

    public enum CutsceneMode
    {
        Timeline,       // ใช้ Unity Timeline (PlayableDirector)
        SimpleCamera    // สลับกล้องง่ายๆ แล้วรอจบเวลา
    }

    private void Start()
    {
        // ซ่อน UI cinematic bars ตอนเริ่ม
        if (cinematicBarsUI != null)
            cinematicBarsUI.SetActive(false);

        // ปิดกล้อง cutscene ตอนเริ่ม
        if (cutsceneCamera != null)
            cutsceneCamera.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        // ตรวจสอบว่าเป็นผู้เล่นหรือไม่
        if (!other.CompareTag(playerTag))
            return;

        // ถ้าตั้งให้ทำงานครั้งเดียวและเคย trigger แล้ว
        if (triggerOnce && hasTriggered)
            return;

        // ถ้ากำลังเล่น cutscene อยู่แล้ว
        if (isPlayingCutscene)
            return;

        hasTriggered = true;
        playerObject = other.gameObject;

        StartCutscene();
    }

    /// <summary>
    /// เริ่มเล่น Cutscene
    /// </summary>
    public void StartCutscene()
    {
        isPlayingCutscene = true;

        // ล็อคผู้เล่น
        if (disablePlayerMovement && playerObject != null)
        {
            LockPlayer(true);
        }

        // แสดง cinematic bars
        if (cinematicBarsUI != null)
            cinematicBarsUI.SetActive(true);

        // Fire event
        onCutsceneStart?.Invoke();

        // เริ่มเล่นตามโหมดที่เลือก
        switch (cutsceneMode)
        {
            case CutsceneMode.Timeline:
                PlayTimeline();
                break;

            case CutsceneMode.SimpleCamera:
                PlaySimpleCameraCutscene();
                break;
        }
    }

    /// <summary>
    /// เล่น cutscene ผ่าน Timeline (PlayableDirector)
    /// </summary>
    private void PlayTimeline()
    {
        if (timelineDirector == null)
        {
            Debug.LogWarning("[CutsceneTrigger] ไม่ได้กำหนด PlayableDirector! กรุณาลากใส่ช่อง timelineDirector");
            EndCutscene();
            return;
        }

        // ฟัง event เมื่อ Timeline เล่นจบ
        timelineDirector.stopped += OnTimelineStopped;
        timelineDirector.Play();
    }

    /// <summary>
    /// เรียกเมื่อ Timeline เล่นจบ
    /// </summary>
    private void OnTimelineStopped(PlayableDirector director)
    {
        director.stopped -= OnTimelineStopped;
        EndCutscene();
    }

    /// <summary>
    /// เล่น cutscene แบบง่าย - สลับกล้อง แล้วรอหมดเวลา
    /// </summary>
    private void PlaySimpleCameraCutscene()
    {
        if (cutsceneCamera == null)
        {
            Debug.LogWarning("[CutsceneTrigger] ไม่ได้กำหนด cutsceneCamera! กรุณาลากใส่ช่อง cutsceneCamera");
            EndCutscene();
            return;
        }

        // สลับกล้อง
        if (playerCamera != null)
            playerCamera.gameObject.SetActive(false);

        cutsceneCamera.gameObject.SetActive(true);

        // จบ cutscene เมื่อหมดเวลา
        Invoke(nameof(EndCutscene), cutsceneDuration);
    }

    /// <summary>
    /// จบ Cutscene - คืนค่าทุกอย่างกลับสู่ปกติ
    /// </summary>
    public void EndCutscene()
    {
        isPlayingCutscene = false;

        // คืนกล้อง (Simple Camera Mode)
        if (cutsceneMode == CutsceneMode.SimpleCamera)
        {
            if (cutsceneCamera != null)
                cutsceneCamera.gameObject.SetActive(false);

            if (playerCamera != null)
                playerCamera.gameObject.SetActive(true);
        }

        // ซ่อน cinematic bars
        if (cinematicBarsUI != null)
            cinematicBarsUI.SetActive(false);

        // ปลดล็อคผู้เล่น
        if (disablePlayerMovement && playerObject != null)
        {
            LockPlayer(false);
        }

        // Fire event
        onCutsceneEnd?.Invoke();

        Debug.Log("[CutsceneTrigger] Cutscene จบแล้ว!");
    }

    /// <summary>
    /// ล็อค/ปลดล็อค การเคลื่อนที่ของผู้เล่น
    /// </summary>
    private void LockPlayer(bool locked)
    {
        if (playerObject == null) return;

        // ปิด/เปิด CharacterController (ถ้ามี)
        var charController = playerObject.GetComponent<CharacterController>();
        if (charController != null)
            charController.enabled = !locked;

        // ปิด/เปิด Rigidbody movement (ถ้ามี)
        var rb = playerObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = locked;
            if (locked) rb.linearVelocity = Vector3.zero;
        }

        // ปิด/เปิด MonoBehaviour scripts ที่เกี่ยวกับ movement
        // (จะปิดทุก MonoBehaviour บน player ยกเว้น Collider)
        if (locked)
        {
            playerScripts = playerObject.GetComponents<MonoBehaviour>();
            foreach (var script in playerScripts)
            {
                if (script != this)
                    script.enabled = false;
            }
        }
        else if (playerScripts != null)
        {
            foreach (var script in playerScripts)
            {
                if (script != null)
                    script.enabled = true;
            }
        }
    }

    // === Gizmo สำหรับแสดง trigger zone ใน Editor ===
    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // สีส้มโปร่งใส
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.5f); // สีเหลืองทองโปร่งใส
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
