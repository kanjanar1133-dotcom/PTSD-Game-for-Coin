using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorGame
{
    public class PlayerFlashlight : MonoBehaviour
    {
        [Header("Flashlight Settings")]
        [Tooltip("โมเดลไฟฉายที่จะแสดงในมือผู้เล่น")]
        public GameObject flashlightModel;
        
        [Tooltip("ดวงไฟ (Light Component) ของไฟฉาย")]
        public Light flashlightLight;

        [Tooltip("ผู้เล่นมีไฟฉายอยู่กับตัวแล้วหรือไม่")]
        public bool hasFlashlight = false;
        
        private bool isEquipped = false;
        private InputAction toggleAction;

        void Awake()
        {
            toggleAction = new InputAction("ToggleFlashlight", binding: "<Keyboard>/f");
            toggleAction.Enable();

            // ปิดโมเดลและแสงไฟฉายตอนเริ่มเกม
            if (flashlightModel != null) flashlightModel.SetActive(false);
            if (flashlightLight != null) flashlightLight.enabled = false;
        }

        void Update()
        {
            if (hasFlashlight && toggleAction.WasPressedThisFrame())
            {
                isEquipped = !isEquipped;
                if (flashlightModel != null) flashlightModel.SetActive(isEquipped);
                if (flashlightLight != null) flashlightLight.enabled = isEquipped;
                Debug.Log($"💡 [PlayerFlashlight] หยิบไฟฉาย: {(isEquipped ? "เปิด" : "ปิด")}");
            }
        }
    }
}
