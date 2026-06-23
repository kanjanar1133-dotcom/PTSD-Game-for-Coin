using UnityEngine;
using System.Collections;

namespace HorrorGame
{
    /// <summary>
    /// HauntedDoorBase — Base class สำหรับประตูหลอนทุกประเภท
    ///
    /// หน้าที่หลัก:
    ///  - Fade in ตอน spawn (ค่อยๆ ปรากฏ)
    ///  - ตรวจจับ Camera FOV — เมื่อผู้เล่นมองมาแล้วหันออก → ประตูหาย (Flicker)
    ///  - Handle Interact() → เขย่าประตู + เล่นเสียงล็อก
    ///  - แจ้ง HauntedDoorSpawner ผ่าน onReadyToVanish callback
    ///
    /// การแก้ปัญหา Spawn ขณะไม่มอง:
    ///  - spawnGracePeriod: ช่วงเวลาหลัง spawn ที่ยังไม่นับ FOV (ป้องกันประตูหายก่อนผู้เล่นเห็น)
    ///  - visibilityRange: ผู้เล่นต้องอยู่ในระยะนี้จึงจะนับว่า "เห็น" (ป้องกันประตูนอก scene)
    /// </summary>
    public abstract class HauntedDoorBase : MonoBehaviour
    {
        // ─── Fade ───────────────────────────────────
        [Header("Fade Settings")]
        [Tooltip("เวลา fade in ตอน spawn (วินาที)")]
        public float fadeInTime = 1.5f;

        [Tooltip("เวลา flicker ก่อนหายสนิท (วินาที)")]
        public float fadeOutTime = 0.6f;

        // ─── FOV / Vanish ────────────────────────────
        [Header("FOV / Vanish Settings")]
        [Tooltip("ผู้เล่นต้องมองมาอย่างน้อยกี่วินาทีจึงนับว่า 'เห็นแล้ว'")]
        public float minLookTime = 0.3f;

        [Tooltip("หน่วงกี่วินาทีหลังประตูออกจอก่อน flicker/destroy")]
        public float vanishDelay = 0.4f;

        [Tooltip("ช่วงเวลาหลัง spawn ที่ไม่นับ FOV เลย\n" +
                 "ตั้งให้ >= fadeInTime เพื่อให้ประตู spawn ขณะผู้เล่นไม่มองได้")]
        public float spawnGracePeriod = 3f;

        [Tooltip("ผู้เล่นต้องอยู่ในระยะนี้จึงจะนับว่า 'เห็น'\n" +
                 "ถ้าประตู spawn ไกลนอก scene ผู้เล่นจะไม่นับว่าเห็นมัน")]
        public float visibilityRange = 18f;

        // ─── Interaction ─────────────────────────────
        [Header("Interaction")]
        [Tooltip("ข้อความตอนผู้เล่นลองเปิดประตู")]
        public string stuckMessage = "มันไม่ยอมเปิด...";

        // ─── Audio ───────────────────────────────────
        [Header("Audio")]
        public AudioSource audioSource;

        [Tooltip("เสียงตอน spawn (เสียงลึกลับ)")]
        public AudioClip spawnSound;

        [Tooltip("เสียงตอนลองเปิด (ประตูสั่น)")]
        public AudioClip lockedSound;

        [Tooltip("เสียงตอนหาย")]
        public AudioClip vanishSound;

        // ─── References ──────────────────────────────
        [Header("References")]
        [Tooltip("Renderers ของประตู (ปล่อยว่างให้หาเอง)")]
        public Renderer[] doorRenderers;

        [Tooltip("Colliders ของประตู (ปล่อยว่างให้หาเอง)")]
        public Collider[] doorColliders;

        // ─── Debug ───────────────────────────────────
        [Header("Debug")]
        [Tooltip("เปิดเพื่อดู FOV state ใน Console ขณะ Play")]
        public bool showDebugLog = false;

        // ─── State ───────────────────────────────────
        protected bool  _isVanishing    = false;
        protected float _currentAlpha   = 0f;
        private   bool  _hasBeenSeen    = false;
        private   bool  _isInView       = false;
        private   float _lookTimer      = 0f;
        private   bool  _vanishTriggered= false;
        private   float _graceTimer     = 0f;   // นับเวลาหลัง spawn

        // ─── References ──────────────────────────────
        protected Camera      _cam;
        protected Transform   _playerTransform;
        private   Material[][] _clonedMats;
        private   bool         _matsInitialized = false;

        // ─── Callback ────────────────────────────────
        /// <summary>เรียกเมื่อประตู destroy ตัวเอง — HauntedDoorSpawner รับ event นี้</summary>
        public System.Action onReadyToVanish;

        // ─────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────

        protected virtual void Start()
        {
            // หา Camera + Player
            _cam = Camera.main;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
                Camera c = player.GetComponentInChildren<Camera>();
                if (c != null) _cam = c;
            }

            // หา Renderers / Colliders อัตโนมัติ
            if (doorRenderers == null || doorRenderers.Length == 0)
                doorRenderers = GetComponentsInChildren<Renderer>();
            if (doorColliders == null || doorColliders.Length == 0)
                doorColliders = GetComponentsInChildren<Collider>();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            // เริ่มต้น transparent
            InitMaterials();
            SetAlpha(0f);
            SetCollidersEnabled(false);

            PlaySound(spawnSound);
            StartCoroutine(FadeInRoutine());

            Debug.Log($"👻 [{GetType().Name}] Spawn ที่ {transform.position}");
        }

        protected virtual void Update()
        {
            if (_isVanishing) return;

            // นับ grace period — ระหว่างนี้ยังไม่ track FOV
            _graceTimer += Time.deltaTime;
            if (_graceTimer < spawnGracePeriod) return;

            UpdateFOVTracking();

            // เมื่อผู้เล่นเห็นแล้วและหันออก → หาย
            if (_hasBeenSeen && !_isInView && !_vanishTriggered && CanVanish())
            {
                _vanishTriggered = true;
                StartCoroutine(VanishRoutine());
            }
        }

        // ─────────────────────────────────────────────
        //  FOV Tracking
        // ─────────────────────────────────────────────

        void UpdateFOVTracking()
        {
            // หากยังไม่มี camera → ลอง find ใหม่
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_playerTransform != null)
                {
                    Camera c = _playerTransform.GetComponentInChildren<Camera>();
                    if (c != null) _cam = c;
                }
                if (_cam == null) return;
            }

            // ตรวจระยะผู้เล่น — ถ้าไกลเกิน visibilityRange ไม่นับ
            if (_playerTransform != null)
            {
                float playerDist = Vector3.Distance(transform.position, _playerTransform.position);
                if (playerDist > visibilityRange)
                {
                    _lookTimer = 0f;
                    if (showDebugLog) Debug.Log($"[{GetType().Name}] ผู้เล่นไกลเกิน ({playerDist:F1}m > {visibilityRange}m) — ไม่นับ");
                    return;
                }
            }

            // ─── WorldToViewportPoint ─────────────────
            bool inView = IsDoorInCameraView();
            _isInView = inView;

            if (inView)
            {
                _lookTimer += Time.deltaTime;
                if (!_hasBeenSeen && _lookTimer >= minLookTime)
                {
                    _hasBeenSeen = true;
                    Debug.Log($"👁️ [{GetType().Name}] ผู้เล่นเห็นประตูแล้ว");
                }

                if (showDebugLog)
                    Debug.Log($"[{GetType().Name}] IN VIEW — timer: {_lookTimer:F2}s | seen: {_hasBeenSeen}");
            }
            else
            {
                _lookTimer = 0f;

                if (showDebugLog && _hasBeenSeen)
                    Debug.Log($"[{GetType().Name}] OUT OF VIEW — CanVanish: {CanVanish()}");
            }
        }

        /// <summary>
        /// ตรวจว่าประตูอยู่ในหน้าจอกล้องหรือไม่
        /// ใช้ WorldToViewportPoint: z>0 = หน้ากล้อง, (0-1,0-1) = ในหน้าจอ
        /// ตรวจ 5 จุด: กึ่งกลาง + 4 มุม
        /// </summary>
        bool IsDoorInCameraView()
        {
            if (_cam == null) return false;

            Bounds bounds = GetDoorBounds();
            Vector3 center = bounds.center;
            float hw = bounds.extents.x;
            float hh = bounds.extents.y;

            Vector3[] checkPoints =
            {
                center,
                center + new Vector3(-hw, -hh, 0f),
                center + new Vector3( hw, -hh, 0f),
                center + new Vector3(-hw,  hh, 0f),
                center + new Vector3( hw,  hh, 0f),
            };

            foreach (Vector3 wp in checkPoints)
            {
                Vector3 vp = _cam.WorldToViewportPoint(wp);
                if (vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)
                    return true;
            }
            return false;
        }

        Bounds GetDoorBounds()
        {
            if (doorRenderers == null || doorRenderers.Length == 0)
                return new Bounds(transform.position + Vector3.up * 1.1f, new Vector3(1f, 2.2f, 0.2f));

            Bounds b = doorRenderers[0].bounds;
            for (int i = 1; i < doorRenderers.Length; i++)
                b.Encapsulate(doorRenderers[i].bounds);
            return b;
        }

        // ─────────────────────────────────────────────
        //  Override Points
        // ─────────────────────────────────────────────

        /// <summary>Subclass override เพื่อบล็อก vanish ชั่วคราว</summary>
        protected virtual bool CanVanish() => true;

        /// <summary>เรียกเมื่อ fade in เสร็จ — Subclass override</summary>
        protected virtual void OnFadeInComplete() { }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>เรียกจาก PlayerInteraction ตอนกด E ที่ประตู</summary>
        public virtual string Interact()
        {
            PlaySound(lockedSound);
            StartCoroutine(ShakeDoorRoutine());
            return stuckMessage;
        }

        // ─────────────────────────────────────────────
        //  Vanish / Fade
        // ─────────────────────────────────────────────

        IEnumerator FadeInRoutine()
        {
            yield return StartCoroutine(FadeAlpha(0f, 1f, fadeInTime));
            SetCollidersEnabled(true);
            OnFadeInComplete();
            Debug.Log($"👻 [{GetType().Name}] Fade in เสร็จ — พร้อมให้ผู้เล่นเห็น");
        }

        IEnumerator VanishRoutine()
        {
            _isVanishing = true;
            Debug.Log($"💨 [{GetType().Name}] ประตูกำลังหาย...");

            yield return new WaitForSeconds(vanishDelay);

            PlaySound(vanishSound);
            SetCollidersEnabled(false);

            // Flicker vanish — ทำงานได้กับทุก shader
            yield return StartCoroutine(FlickerVanish());

            onReadyToVanish?.Invoke();
            Destroy(gameObject);
        }

        /// <summary>ประตู flicker เร็วขึ้นก่อนหาย — ดูเหมือนสิ่งเหนือธรรมชาติ</summary>
        IEnumerator FlickerVanish()
        {
            bool visible = true;
            float elapsed = 0f;

            while (elapsed < fadeOutTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutTime);
                float waitTime = Mathf.Lerp(0.08f, 0.012f, t);
                visible = !visible;
                SetRenderersEnabled(visible);
                yield return new WaitForSeconds(waitTime);
            }

            SetRenderersEnabled(false);
        }

        IEnumerator ShakeDoorRoutine()
        {
            float elapsed  = 0f;
            float duration = 0.45f;
            Quaternion origin = transform.localRotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float shake = Mathf.Sin(elapsed * 55f) * 0.8f * (1f - elapsed / duration);
                transform.localRotation = origin * Quaternion.Euler(0f, shake, 0f);
                yield return null;
            }
            transform.localRotation = origin;
        }

        // ─────────────────────────────────────────────
        //  Fade Alpha Helpers
        // ─────────────────────────────────────────────

        protected IEnumerator FadeAlpha(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                _currentAlpha = a;
                SetAlpha(a);
                yield return null;
            }
            _currentAlpha = to;
            SetAlpha(to);
        }

        void InitMaterials()
        {
            if (_matsInitialized || doorRenderers == null) return;

            _clonedMats = new Material[doorRenderers.Length][];
            for (int i = 0; i < doorRenderers.Length; i++)
            {
                Material[] mats = doorRenderers[i].materials;
                _clonedMats[i] = new Material[mats.Length];
                for (int j = 0; j < mats.Length; j++)
                {
                    _clonedMats[i][j] = new Material(mats[j]);
                    EnableTransparency(_clonedMats[i][j]);
                }
                doorRenderers[i].materials = _clonedMats[i];
            }
            _matsInitialized = true;
        }

        void EnableTransparency(Material mat)
        {
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 2f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            else if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
        }

        void SetAlpha(float alpha)
        {
            if (_clonedMats == null) return;
            foreach (var matArr in _clonedMats)
            {
                if (matArr == null) continue;
                foreach (var mat in matArr)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty("_Color"))
                    {
                        Color c = mat.GetColor("_Color");
                        c.a = alpha;
                        mat.SetColor("_Color", c);
                    }
                    else if (mat.HasProperty("_BaseColor"))
                    {
                        Color c = mat.GetColor("_BaseColor");
                        c.a = alpha;
                        mat.SetColor("_BaseColor", c);
                    }
                }
            }
        }

        protected void SetCollidersEnabled(bool state)
        {
            if (doorColliders == null) return;
            foreach (var col in doorColliders)
                if (col != null) col.enabled = state;
        }

        protected void SetRenderersEnabled(bool state)
        {
            if (doorRenderers == null) return;
            foreach (var r in doorRenderers)
                if (r != null) r.enabled = state;
        }

        // ─────────────────────────────────────────────
        //  Audio
        // ─────────────────────────────────────────────

        protected void PlaySound(AudioClip clip, float vol = 1f)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip, vol);
        }

        // ─────────────────────────────────────────────
        //  Cleanup
        // ─────────────────────────────────────────────

        void OnDestroy()
        {
            if (_clonedMats == null) return;
            foreach (var mats in _clonedMats)
                if (mats != null)
                    foreach (var m in mats)
                        if (m != null) Destroy(m);
        }

        // ─────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1.1f,
                                new Vector3(1f, 2.2f, 0.15f));

            // วงกลม visibilityRange
            Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, visibilityRange);
        }
    }
}
