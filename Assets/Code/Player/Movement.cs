using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float crouchSpeed = 1.5f;
    public float mouseSensitivity = 0.1f;
    public float gravity = -20f;

    [Header("Height Settings")]
    public float eyeHeight = 1.6f;
    public float crouchEyeHeight = 0.8f;
    public float standingCCHeight = 2f;
    public float crouchingCCHeight = 1f;

    [Header("References")]
    public Transform playerCamera;

    [Header("Hiding Settings")]
    public float hideCameraHeight = 0.4f; 
    public float peekLimitX = 60f; // ก้มเงยได้กี่องศา
    public float peekLimitY = 60f; // หันซ้ายขวาได้กี่องศา
    
    private CharacterController controller;
    private float xRotation = 0f;
    private float yRotation = 0f;
    private bool isHiding = false;
    private Vector3 velocity;
    private float currentSpeed;

    // ตัวแปรสำหรับล็อคมุมกล้องตอนซ่อน
    private float hideYCenter = 0f;
    
    // ตัวแปรสำหรับล็อคการเดิน/หมุนกล้องตอนกำลังวาร์ป
    private bool isLocked = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        yRotation = transform.eulerAngles.y;
        
        if (playerCamera == null) {
            Camera mainCam = GetComponentInChildren<Camera>();
            if (mainCam != null) playerCamera = mainCam.transform;
        }

        // จัดกล้องให้มองตรงเสมอตอนเริ่มเกม
        xRotation = 0f;
        if (playerCamera != null) {
            playerCamera.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }

    void Update()
    {
        if (playerCamera == null) return;

        if (isLocked) 
        {
            // ถ้าระบบโดนล็อค (เช่นตอนหน้าจอมืด) ให้คงมุมกล้องและทิศทางตัวละครไว้ที่เดิมตลอดเวลา
            // สิ่งนี้ช่วยป้องกันบั๊กที่ระบบอื่น (เช่น Animator) จะดึงกล้องให้เงยขึ้นเมื่อเราหยุดอัปเดตมุมกล้อง
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        if (isHiding)
        {
            // --- ระบบล็อคมุมกล้องตอนซ่อน ---
            playerCamera.localPosition = new Vector3(0, hideCameraHeight, 0);

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -peekLimitX, peekLimitX); // จำกัดก้มเงย

            yRotation += mouseX;
            // จำกัดการหันซ้ายขวาโดยเทียบจากจุดศูนย์กลางหน้าตู้ (hideYCenter)
            float clampedY = Mathf.Clamp(Mathf.DeltaAngle(hideYCenter, yRotation), -peekLimitY, peekLimitY);
            yRotation = hideYCenter + clampedY;

            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        }
        else
        {
            HandleNormalMovement(mouseX, mouseY);
        }
    }

    void HandleNormalMovement(float mouseX, float mouseY)
    {
        // การหันหน้าปกติ
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -85f, 85f);
        yRotation += mouseX;
        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        // ระบบย่อตัว
        if (Keyboard.current.leftCtrlKey.isPressed)
        {
            controller.height = crouchingCCHeight;
            currentSpeed = crouchSpeed;
            playerCamera.localPosition = new Vector3(0, crouchEyeHeight, 0);
        }
        else
        {
            controller.height = standingCCHeight;
            currentSpeed = Keyboard.current.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            playerCamera.localPosition = new Vector3(0, eyeHeight, 0);
        }

        // เดินและแรงโน้มถ่วง
        if (controller.isGrounded && velocity.y < 0) velocity.y = -2f;

        float moveX = 0, moveZ = 0;
        if (Keyboard.current.wKey.isPressed) moveZ = 1;
        if (Keyboard.current.sKey.isPressed) moveZ = -1;
        if (Keyboard.current.aKey.isPressed) moveX = -1;
        if (Keyboard.current.dKey.isPressed) moveX = 1;

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        
        if (controller.enabled) {
            controller.Move(move * currentSpeed * Time.deltaTime);
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);

            // Report noise to NoiseManager
            if (move.magnitude > 0.1f && controller.isGrounded)
            {
                float noiseAmount = 0f;
                if (Keyboard.current.leftCtrlKey.isPressed) noiseAmount = HorrorGame.NoiseManager.Instance.crouchNoise;
                else if (Keyboard.current.leftShiftKey.isPressed) noiseAmount = HorrorGame.NoiseManager.Instance.runNoise;
                else noiseAmount = HorrorGame.NoiseManager.Instance.walkNoise;

                HorrorGame.NoiseManager.Instance.AddNoise(noiseAmount);
            }
        }
    }

    public void SetHiding(bool state)
    {
        isHiding = state;
        if (isHiding)
        {
            hideYCenter = transform.eulerAngles.y; // บันทึกทิศหน้าตู้ไว้เป็นจุดศูนย์กลาง
            yRotation = hideYCenter;
            xRotation = 0;
            velocity = Vector3.zero;
        }
    }

    // ฟังก์ชันสำหรับให้ศัตรูมาเช็คว่าเราซ่อนอยู่ไหม
    public bool IsHiding() => isHiding;

    // ฟังก์ชันสำหรับล็อกตัวละครตอนวาร์ป ป้องกันบั๊กกล้องหมุนเองเวลา Disable สคริปต์
    public void SetMovementLock(bool state)
    {
        isLocked = state;
        if (state && controller != null) 
        {
            velocity = Vector3.zero; // หยุดแรงเฉื่อย
        }
    }

    // ---- CharacterController ชนผี → Game Over ทันที ----
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (isHiding) return;
        HorrorGame.EnemyAI enemy = hit.collider.GetComponent<HorrorGame.EnemyAI>();
        if (enemy == null) enemy = hit.collider.GetComponentInParent<HorrorGame.EnemyAI>();
        if (enemy != null)
        {
            enemy.TriggerCatch();
        }
    }

    // ฟังก์ชันสำหรับวาร์ปผู้เล่น พร้อมจัดหน้าให้มองตรงเสมอ
    public void TeleportTo(Vector3 targetPos, Quaternion targetRot)
    {
        if (controller != null) controller.enabled = false;
        
        transform.position = targetPos;
        
        // บังคับมองตรงไปข้างหน้าเสมอ (ไม่แหงนหรือก้ม)
        xRotation = 0f; 
        yRotation = targetRot.eulerAngles.y; // หันตามทิศของจุดหมาย
        
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        if (playerCamera != null)
        {
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        Physics.SyncTransforms();
        
        if (controller != null) controller.enabled = true;
        isLocked = false; // ปลดล็อกให้กลับมาเดินได้ปกติ
    }
}
