using UnityEngine;
using System.Collections;

namespace HorrorGame
{
    /// <summary>
    /// VanishingDoor — ประตู Hallucination ที่หายไปแล้วกลับมา
    ///
    /// ตัวละครเดินเข้าใกล้/จ้องมอง → ประตู fade out หายไป (ผู้เล่นคิดว่าเห็นผิด)
    /// หลังจาก delay → ประตูกลับมาใหม่
    ///
    /// Setup:
    ///   1. วาง Script นี้บน Door GameObject (เดียวกับ BaseDoor หรือแยกต่างหาก)
    ///   2. Collider ของประตูจะถูก disable พร้อมกับ mesh
    ///   3. ตั้ง triggerDistance และ duration ตามต้องการ
    ///   4. (ตัวเลือก) ลาก GhostAppearance เพื่อใช้ fade material
    ///      หรือปล่อยให้ Script หา Renderer เอง
    /// </summary>
    public class VanishingDoor : MonoBehaviour
    {
        [Header("Vanish Settings")]
        [Tooltip("ระยะที่ผู้เล่นต้องเข้ามาก่อนประตูจะเริ่มหาย (เมตร)")]
        public float triggerDistance = 4f;

        [Tooltip("เวลา fade out (วินาที)")]
        public float fadeOutTime = 0.6f;

        [Tooltip("เวลาที่ประตูหายอยู่ก่อนกลับมา (วินาที)")]
        public float invisibleDuration = 4f;

        [Tooltip("เวลา fade กลับมา (วินาที)")]
        public float fadeInTime = 1.5f;

        [Tooltip("trigger ได้กี่ครั้ง (-1 = ไม่จำกัด)")]
        public int maxVanishCount = -1;

        [Header("Audio")]
        public AudioSource audioSource;

        [Tooltip("เสียงตอนประตูหาย (เสียงหอน/วิ้ว)")]
        public AudioClip vanishSound;

        [Tooltip("เสียงตอนประตูกลับมา")]
        public AudioClip reappearSound;

        [Header("References")]
        [Tooltip("Renderer ของประตู (ปล่อยว่างเพื่อให้หาเอง)")]
        public Renderer[] doorRenderers;

        [Tooltip("Collider ของประตู (ปล่อยว่างเพื่อให้หาเอง)")]
        public Collider[] doorColliders;

        // ─── Private ───────────────────────────────
        private int     _vanishCount = 0;
        private bool    _isVanishing = false;
        private bool    _isVisible   = true;
        private Transform _player;
        private Material[][] _clonedMats;
        private bool _matsInitialized = false;

        void Start()
        {
            // หา Player
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;

            // หา Renderers / Colliders อัตโนมัติ
            if (doorRenderers == null || doorRenderers.Length == 0)
                doorRenderers = GetComponentsInChildren<Renderer>();
            if (doorColliders == null || doorColliders.Length == 0)
                doorColliders = GetComponentsInChildren<Collider>();

            InitMaterials();
        }

        void InitMaterials()
        {
            if (_matsInitialized) return;
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
            }
        }

        void Update()
        {
            if (_isVanishing || !_isVisible) return;
            if (_player == null) return;
            if (maxVanishCount >= 0 && _vanishCount >= maxVanishCount) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist < triggerDistance)
            {
                _vanishCount++;
                StartCoroutine(VanishSequence());
            }
        }

        IEnumerator VanishSequence()
        {
            _isVanishing = true;

            Debug.Log("🚪 [VanishingDoor] ประตูกำลังหาย...");
            PlaySound(vanishSound);

            // Fade Out
            yield return StartCoroutine(FadeAlpha(1f, 0f, fadeOutTime));

            // Disable Colliders
            SetCollidersEnabled(false);
            _isVisible = false;

            // รอ
            yield return new WaitForSeconds(invisibleDuration);

            // Fade กลับมา
            _isVisible = true;
            SetCollidersEnabled(false); // ยังปิด collider ไว้ตอน fade กลับ

            PlaySound(reappearSound);
            yield return StartCoroutine(FadeAlpha(0f, 1f, fadeInTime));

            // คืน Colliders
            SetCollidersEnabled(true);

            _isVanishing = false;
            Debug.Log("🚪 [VanishingDoor] ประตูกลับมาแล้ว");
        }

        IEnumerator FadeAlpha(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }
            SetAlpha(to);
        }

        void SetAlpha(float alpha)
        {
            if (_clonedMats == null) return;
            for (int i = 0; i < _clonedMats.Length; i++)
            {
                if (_clonedMats[i] == null) continue;
                foreach (var mat in _clonedMats[i])
                {
                    if (mat == null) continue;
                    if (mat.HasProperty("_Color"))
                    {
                        Color c = mat.GetColor("_Color"); c.a = alpha;
                        mat.SetColor("_Color", c);
                    }
                    else if (mat.HasProperty("_BaseColor"))
                    {
                        Color c = mat.GetColor("_BaseColor"); c.a = alpha;
                        mat.SetColor("_BaseColor", c);
                    }
                }
            }
        }

        void SetCollidersEnabled(bool state)
        {
            foreach (var col in doorColliders)
                if (col != null) col.enabled = state;
        }

        void OnDestroy()
        {
            if (_clonedMats == null) return;
            foreach (var mats in _clonedMats)
                if (mats != null)
                    foreach (var m in mats)
                        if (m != null) Destroy(m);
        }

        void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, triggerDistance);
        }
    }
}
