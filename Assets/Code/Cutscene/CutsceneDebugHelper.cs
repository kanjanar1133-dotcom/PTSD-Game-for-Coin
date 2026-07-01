using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorGame
{
    /// <summary>
    /// สคริปต์ช่วยตรวจสอบรายละเอียดแบบลึกของ Player หลังคัตซีนจบ
    /// </summary>
    public class CutsceneDebugHelper : MonoBehaviour
    {
        public GameObject player;

        void LateUpdate()
        {
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return;
            }

            CharacterController cc = player.GetComponent<CharacterController>();
            Movement movement = player.GetComponent<Movement>();
            MouseLook mouseLook = player.GetComponentInChildren<MouseLook>(true);

            bool ccEnabled = cc != null && cc.enabled;
            bool mvEnabled = movement != null && movement.enabled;
            bool mlEnabled = mouseLook != null && mouseLook.enabled;

            string debugInfo = $"[CutsceneDebug] CC Active: {ccEnabled} | Movement Active: {mvEnabled} | MouseLook Active: {mlEnabled}";

            if (movement != null)
            {
                // ตรวจสอบตัวแปรภายในต่างๆ ของ Movement ด้วย Reflection
                try
                {
                    var isLockedField = typeof(Movement).GetField("isLocked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    bool isLocked = isLockedField != null ? (bool)isLockedField.GetValue(movement) : false;

                    var isHidingField = typeof(Movement).GetField("isHiding", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    bool isHiding = isHidingField != null ? (bool)isHidingField.GetValue(movement) : false;

                    var currentSpeedField = typeof(Movement).GetField("currentSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    float currentSpeed = currentSpeedField != null ? (float)currentSpeedField.GetValue(movement) : 0f;

                    debugInfo += $" | isLocked: {isLocked} | isHiding: {isHiding} | currentSpeed: {currentSpeed}";
                    debugInfo += $" | playerCamera Assigned: {movement.playerCamera != null}";
                    if (movement.playerCamera != null)
                    {
                        debugInfo += $" | Camera LocalRot: {movement.playerCamera.localRotation.eulerAngles}";
                    }
                }
                catch (System.Exception ex)
                {
                    debugInfo += $" | Reflection Error: {ex.Message}";
                }
            }

            // ตรวจสอบระบบ Input ดิบใน Unity
            Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            bool wPressed = Keyboard.current != null && Keyboard.current.wKey.isPressed;
            debugInfo += $" | Input -> MouseDelta: {mouseDelta} | W_Pressed: {wPressed} | LockState: {Cursor.lockState}";

            Debug.LogWarning(debugInfo);

            if (Input.GetKeyDown(KeyCode.F12))
            {
                Debug.LogWarning("🔌 [CutsceneDebug] รันคำสั่งบังคับคืนสภาพแบบเบ็ดเสร็จผ่าน F12...");
                if (movement != null)
                {
                    movement.enabled = true;
                    movement.SetMovementLock(false);
                    movement.SetHiding(false);
                }
                if (cc != null) cc.enabled = true;
                if (mouseLook != null)
                {
                    mouseLook.enabled = true;
                    // ปลุก InputAction Look คืนชีพ
                    try
                    {
                        var lookActField = typeof(MouseLook).GetField("lookAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (lookActField != null)
                        {
                            InputAction lookAct = (InputAction)lookActField.GetValue(mouseLook);
                            if (lookAct != null) lookAct.Enable();
                        }
                    }
                    catch { }
                }
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
