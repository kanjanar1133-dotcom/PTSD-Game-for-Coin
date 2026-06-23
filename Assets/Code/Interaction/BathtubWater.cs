using UnityEngine;
using System.Collections;

namespace HorrorGame
{
    /// <summary>
    /// BathtubWater — ระบบน้ำในอ่างที่ค่อยๆ เพิ่มระดับขึ้น
    ///
    /// Setup ใน Unity Inspector:
    ///   1. วาง Script นี้บน GameObject ของอ่างน้ำ (เช่น Bathtub)
    ///   2. สร้าง Child GameObject ชื่อ "Water" → ใส่ Plane หรือ Quad
    ///      แล้วลากใส่ช่อง waterMesh
    ///   3. ตั้ง Y ของ waterMesh ให้ต่ำกว่าขอบอ่าง (ตำแหน่งเริ่มต้น)
    ///   4. กำหนด maxWaterHeight = Y ที่น้ำเต็มอ่าง (local space)
    ///   5. ลาก AudioSource ใส่ช่อง audioSource
    ///   6. ลาก AudioClip เสียงน้ำไหลใส่ช่อง fillSound
    ///   7. (ตัวเลือก) ลาก AudioClip เสียง splash ใส่ช่อง interactSound
    ///   8. เพิ่ม Collider บน GameObject (เป็น trigger หรือ solid)
    ///      แล้วติ๊กเป็น Trigger ถ้าต้องการตรวจจับผู้เล่น
    ///
    /// วิธีใช้ใน PlayerInteraction:
    ///   - CheckInteraction() และ PerformInteraction() จะเรียก
    ///     GetComponentInParent<BathtubWater>() โดยอัตโนมัติ
    ///   - กด [E] ที่อ่าง → เปิด/ปิดน้ำ (toggle)
    ///   - ถ้าต้องการเรียกจาก Script อื่น: bathtubWater.ToggleFill()
    /// </summary>
    public class BathtubWater : MonoBehaviour
    {
        // ─── Water Visual ──────────────────────────────────────
        [Header("Water Mesh")]
        [Tooltip("GameObject ที่แสดงผิวน้ำ (Plane / Quad child ของอ่าง)")]
        public GameObject waterMesh;

        [Tooltip("ระดับ Y ต่ำสุด (local) ที่น้ำจะเริ่มต้น (อ่างว่าง)")]
        public float minWaterHeight = -0.3f;

        [Tooltip("ระดับ Y สูงสุด (local) ที่น้ำจะหยุด (อ่างเต็ม)")]
        public float maxWaterHeight = 0.05f;

        [Tooltip("ความเร็วที่น้ำค่อยๆ เพิ่มขึ้น (หน่วย Unity ต่อวินาที)")]
        public float fillSpeed = 0.04f;

        // ─── Material / Color ──────────────────────────────────
        [Header("Water Material")]
        [Tooltip("Material ที่ใช้กับ waterMesh (ถ้าเว้นว่างจะใช้ material เดิม)")]
        public Material waterMaterial;

        [Tooltip("สี Albedo ของน้ำ (alpha = ความโปร่งใส)")]
        public Color waterColor = new Color(0.1f, 0.4f, 0.8f, 0.75f);

        // ─── Audio ─────────────────────────────────────────────
        [Header("Audio")]
        [Tooltip("AudioSource สำหรับเสียงน้ำ")]
        public AudioSource audioSource;

        [Tooltip("เสียงน้ำไหล (loop ขณะกำลังเติม)")]
        public AudioClip fillSound;

        [Tooltip("เสียง splash ตอนกด E เปิด/ปิด")]
        public AudioClip interactSound;

        [Tooltip("เสียงน้ำเต็มอ่าง (เล่นครั้งเดียว)")]
        public AudioClip fullSound;

        // ─── Settings ──────────────────────────────────────────
        [Header("Settings")]
        [Tooltip("เริ่มเปิดน้ำทันทีตอน Start หรือไม่")]
        public bool autoFillOnStart = false;

        [Tooltip("กด E ได้ไหม (ถ้า false จะควบคุมจาก Script อื่น)")]
        public bool allowPlayerInteract = true;

        // ─── Private ───────────────────────────────────────────
        private bool  _isFilling   = false;
        private bool  _isFull      = false;
        private bool  _isDraining  = false;
        private bool  _fullSoundPlayed = false;
        private Renderer _waterRenderer;
        private MaterialPropertyBlock _propBlock;

        // ──────────────────────────────────────────────────────
        #region Unity Lifecycle

        void Start()
        {
            InitWater();
            if (autoFillOnStart) StartFill();
        }

        void Update()
        {
            if (_isFilling && !_isFull)
                RiseWater();

            if (_isDraining)
                DrainStep();
        }

        #endregion

        // ──────────────────────────────────────────────────────
        #region Init

        void InitWater()
        {
            if (waterMesh == null)
            {
                Debug.LogWarning("⚠️ [BathtubWater] ไม่พบ waterMesh — โปรดลาก GameObject ผิวน้ำมาใส่ใน Inspector");
                return;
            }

            // ตั้งตำแหน่งเริ่มต้น (local Y)
            Vector3 pos = waterMesh.transform.localPosition;
            pos.y = minWaterHeight;
            waterMesh.transform.localPosition = pos;

            // ตั้งค่า Material
            _waterRenderer = waterMesh.GetComponent<Renderer>();
            if (_waterRenderer != null)
            {
                _propBlock = new MaterialPropertyBlock();

                if (waterMaterial != null)
                    _waterRenderer.sharedMaterial = waterMaterial;

                // ใส่สีและความโปร่งใสผ่าน PropertyBlock (ไม่เปลี่ยน shared material)
                _waterRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_BaseColor",  waterColor); // URP
                _propBlock.SetColor("_Color",      waterColor); // Built-in
                _waterRenderer.SetPropertyBlock(_propBlock);
            }

            // ซ่อนน้ำ (อ่างว่าง)
            waterMesh.SetActive(false);

            Debug.Log("🛁 [BathtubWater] ระบบน้ำพร้อมใช้งาน (อ่างว่าง)");
        }

        #endregion

        // ──────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// เรียกจาก PlayerInteraction เมื่อกด E บนอ่าง
        /// </summary>
        public void Interact()
        {
            if (!allowPlayerInteract) return;
            ToggleFill();
        }

        /// <summary>
        /// เปิด/ปิดน้ำ (toggle)
        /// </summary>
        public void ToggleFill()
        {
            if (_isFull)
            {
                Debug.Log("🛁 [BathtubWater] อ่างเต็มแล้ว — ไม่สามารถเปิดน้ำได้อีก");
                return;
            }

            if (_isFilling) StopFill();
            else            StartFill();
        }

        /// <summary>
        /// เปิดน้ำ (เริ่มเติม)
        /// </summary>
        public void StartFill()
        {
            if (_isFull) return;
            _isFilling = true;

            if (waterMesh != null) waterMesh.SetActive(true);

            // เสียงน้ำไหล
            if (audioSource != null && fillSound != null)
            {
                audioSource.clip = fillSound;
                audioSource.loop = true;
                if (!audioSource.isPlaying) audioSource.Play();
            }

            // เสียง interact
            if (audioSource != null && interactSound != null)
                audioSource.PlayOneShot(interactSound);

            Debug.Log("🚿 [BathtubWater] เริ่มเติมน้ำ...");
        }

        /// <summary>
        /// ปิดน้ำ (หยุดเติม แต่น้ำยังอยู่)
        /// </summary>
        public void StopFill()
        {
            _isFilling = false;

            if (audioSource != null) audioSource.Stop();

            // เสียง interact
            if (audioSource != null && interactSound != null)
                audioSource.PlayOneShot(interactSound);

            Debug.Log("🛁 [BathtubWater] หยุดเติมน้ำ");
        }

        /// <summary>
        /// คืนค่าเปอร์เซ็นต์น้ำ (0.0 – 1.0)
        /// </summary>
        public float GetFillPercent()
        {
            if (waterMesh == null) return 0f;
            float current = waterMesh.transform.localPosition.y;
            return Mathf.InverseLerp(minWaterHeight, maxWaterHeight, current);
        }

        /// <summary>
        /// คืน true ถ้าน้ำเต็มอ่างแล้ว
        /// </summary>
        public bool IsFull => _isFull;

        /// <summary>
        /// คืน true ถ้ากำลังเติมน้ำอยู่
        /// </summary>
        public bool IsFilling => _isFilling;

        /// <summary>
        /// แสดงน้ำเต็มอ่างทันที (ไม่ค่อยๆ ไหล) — เรียกจาก BathtubRoomTrigger
        /// </summary>
        public void ShowInstant()
        {
            if (waterMesh == null) return;

            // หยุด drain ถ้ากำลังระบาย
            _isDraining = false;
            _isFilling  = false;

            // ตั้งระดับน้ำเต็ม
            Vector3 pos = waterMesh.transform.localPosition;
            pos.y = maxWaterHeight;
            waterMesh.transform.localPosition = pos;

            // ตั้งสีเต็ม opacity
            if (_waterRenderer != null && _propBlock != null)
            {
                _propBlock.SetColor("_BaseColor", waterColor);
                _propBlock.SetColor("_Color",     waterColor);
                _waterRenderer.SetPropertyBlock(_propBlock);
            }

            waterMesh.SetActive(true);
            _isFull = true;
            _fullSoundPlayed = false;

            // เสียงน้ำ ambient
            if (audioSource != null && fillSound != null)
            {
                audioSource.clip = fillSound;
                audioSource.loop = false;
                audioSource.Play();
            }

            Debug.Log("🛁 [BathtubWater] แสดงน้ำเต็มอ่างทันที");
        }

        /// <summary>
        /// ระบายน้ำออกช้าๆ แล้วซ่อน — เรียกจาก BathtubRoomTrigger
        /// </summary>
        /// <param name="drainSpeed">ความเร็วระบาย (หน่วยต่อวินาที, 0 = ทันที)</param>
        public void DrainAndHide(float drainSpeed = 0.08f)
        {
            if (waterMesh == null || !waterMesh.activeSelf) return;

            _isFilling = false;
            _isFull    = false;

            if (audioSource != null) audioSource.Stop();

            if (drainSpeed <= 0f)
            {
                // ซ่อนทันที
                waterMesh.SetActive(false);
                ResetWaterPosition();
                Debug.Log("🛁 [BathtubWater] น้ำหายทันที");
            }
            else
            {
                _drainSpeed = drainSpeed;
                _isDraining = true;
                Debug.Log("🛁 [BathtubWater] เริ่มระบายน้ำ...");
            }
        }

        private float _drainSpeed = 0.08f;

        #endregion

        // ──────────────────────────────────────────────────────
        #region Internal

        void RiseWater()
        {
            if (waterMesh == null) return;

            Vector3 pos = waterMesh.transform.localPosition;

            // เพิ่มระดับน้ำ
            pos.y = Mathf.MoveTowards(pos.y, maxWaterHeight, fillSpeed * Time.deltaTime);
            waterMesh.transform.localPosition = pos;

            // อัปเดตสี (opacity เพิ่มตาม fill percent)
            if (_waterRenderer != null && _propBlock != null)
            {
                float t = GetFillPercent();
                Color c = waterColor;
                c.a = Mathf.Lerp(0.3f, waterColor.a, t);
                _propBlock.SetColor("_BaseColor", c);
                _propBlock.SetColor("_Color",     c);
                _waterRenderer.SetPropertyBlock(_propBlock);
            }

            // ตรวจสอบเต็มอ่าง
            if (pos.y >= maxWaterHeight)
            {
                _isFull    = true;
                _isFilling = false;

                if (audioSource != null) audioSource.Stop();

                if (!_fullSoundPlayed && audioSource != null && fullSound != null)
                {
                    _fullSoundPlayed = true;
                    audioSource.PlayOneShot(fullSound);
                }

                Debug.Log("🛁 [BathtubWater] น้ำเต็มอ่างแล้ว!");
            }
        }

        /// <summary>ระบายน้ำทีละ frame จนหมด แล้วซ่อน waterMesh</summary>
        void DrainStep()
        {
            if (waterMesh == null) { _isDraining = false; return; }

            Vector3 pos = waterMesh.transform.localPosition;
            pos.y = Mathf.MoveTowards(pos.y, minWaterHeight, _drainSpeed * Time.deltaTime);
            waterMesh.transform.localPosition = pos;

            // fade alpha ลดลงตามระดับน้ำ
            if (_waterRenderer != null && _propBlock != null)
            {
                float t = Mathf.InverseLerp(minWaterHeight, maxWaterHeight, pos.y);
                Color c = waterColor;
                c.a = Mathf.Lerp(0f, waterColor.a, t);
                _propBlock.SetColor("_BaseColor", c);
                _propBlock.SetColor("_Color",     c);
                _waterRenderer.SetPropertyBlock(_propBlock);
            }

            if (pos.y <= minWaterHeight)
            {
                _isDraining = false;
                waterMesh.SetActive(false);
                ResetWaterPosition();
                Debug.Log("🛁 [BathtubWater] น้ำระบายหมดแล้ว");
            }
        }

        /// <summary>รีเซ็ตตำแหน่ง Y น้ำกลับจุดเริ่มต้น</summary>
        void ResetWaterPosition()
        {
            if (waterMesh == null) return;
            Vector3 pos = waterMesh.transform.localPosition;
            pos.y = minWaterHeight;
            waterMesh.transform.localPosition = pos;
        }

        #endregion

        // ──────────────────────────────────────────────────────
        #region Gizmos

        void OnDrawGizmosSelected()
        {
            // แสดงระดับน้ำ min/max ใน Scene view
            Gizmos.color = new Color(0.1f, 0.5f, 1f, 0.4f);
            Vector3 wMin = transform.TransformPoint(new Vector3(0, minWaterHeight, 0));
            Vector3 wMax = transform.TransformPoint(new Vector3(0, maxWaterHeight, 0));
            Gizmos.DrawWireCube(wMin, new Vector3(0.8f, 0.01f, 0.4f));

            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(wMax, new Vector3(0.8f, 0.01f, 0.4f));
        }

        #endregion
    }
}
