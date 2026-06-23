using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace HorrorGame
{
    /// <summary>
    /// ติดกับ GameObject ของผี/ศพ เพื่อควบคุม Fade In/Out, Flicker, Wander และ Pose
    ///
    /// รองรับ:
    /// - SkinnedMeshRenderer (โมเดล 3D พร้อม Bones)
    /// - MeshRenderer ธรรมดา
    /// - Ghost Wander: ผีเคลื่อนที่ลอยๆ ระหว่างที่โผล่
    /// - Multiple Poses: สุ่ม child GameObject ที่ active เพื่อเปลี่ยน pose
    /// - Sound Emitter: เล่นเสียงผีขณะโผล่
    ///
    /// Shader ต้องรองรับ Alpha (Standard Transparent, URP Lit + Alpha, Fade)
    /// </summary>
    public class GhostAppearance : MonoBehaviour
    {
        [Header("Material Settings")]
        [Tooltip("ถ้าเปิด จะ clone material ไม่ให้กระทบ Asset ต้นฉบับ")]
        public bool cloneMaterial = true;

        [Tooltip("ชื่อ property ของ Alpha ใน Shader (Standard = _Color, URP = _BaseColor)")]
        public string alphaPropertyName = "_Color";

        [Header("Flicker Settings")]
        [Tooltip("ความน่าจะเป็นที่จะ flicker แต่ละ frame (0-1)")]
        [Range(0f, 1f)]
        public float flickerChance = 0.08f;

        [Tooltip("ค่า alpha ต่ำสุดตอน flicker")]
        [Range(0f, 1f)]
        public float flickerMinAlpha = 0.3f;

        [Tooltip("ค่า alpha สูงสุดตอน flicker")]
        [Range(0f, 1f)]
        public float flickerMaxAlpha = 1f;

        [Header("Ghost Tint")]
        [Tooltip("สีหลักของผี (ขาว = ไม่เปลี่ยนสี)")]
        public Color ghostTint = new Color(0.7f, 0.85f, 1f, 1f); // ฟ้าอ่อนแสงวิญญาณ

        // ─── Ghost Wander ──────────────────────────
        [Header("Ghost Wander (เคลื่อนที่ขณะโผล่)")]
        [Tooltip("เปิด/ปิดการเคลื่อนที่ของผี")]
        public bool enableWander = true;

        [Tooltip("ความเร็วลอย (เมตร/วินาที)")]
        public float wanderSpeed = 0.3f;

        [Tooltip("ขนาดการแกว่งซ้ายขวา")]
        public float wanderAmplitudeX = 0.15f;

        [Tooltip("ขนาดการลอยขึ้นลง")]
        public float wanderAmplitudeY = 0.08f;

        [Tooltip("ความถี่การแกว่ง")]
        public float wanderFrequency = 0.5f;

        // ─── Pose Variants ────────────────────────
        [Header("Pose Variants (child GameObjects)")]
        [Tooltip("List ของ child GameObject ที่เป็น pose ต่างๆ (นั่ง/ยืน/กลับหัว) — ถ้าว่างจะใช้ mesh เดิม")]
        public List<GameObject> poseVariants = new List<GameObject>();

        [Tooltip("สุ่ม pose ใหม่ทุกครั้งที่ผีโผล่")]
        public bool randomPoseOnAppear = true;

        // ─── Ghost Audio ──────────────────────────
        [Header("Ghost Audio")]
        [Tooltip("AudioSource สำหรับเสียงผี (ถ้าไม่ใส่จะสร้างอัตโนมัติ)")]
        public AudioSource ghostAudioSource;

        [Tooltip("เสียงที่ผีเปล่งออกมาระหว่างโผล่ (ครวญ/หายใจ)")]
        public AudioClip[] ghostAmbientSounds;

        [Tooltip("ความดังเสียงผี")]
        [Range(0f, 1f)]
        public float ghostSoundVolume = 0.6f;

        [Tooltip("ระยะเวลา min/max ระหว่างเล่นเสียง ambient (วินาที)")]
        public float soundIntervalMin = 1.5f;
        public float soundIntervalMax = 4f;

        // ─── Private ─────────────────────────────
        private Renderer[] _renderers;
        private Material[][] _materials;
        private float _currentAlpha = 0f;
        private bool _initialized = false;

        // Wander
        private Vector3 _wanderOrigin;
        private float   _wanderTimer = 0f;
        private bool    _isWandering = false;

        // Sound
        private float _nextSoundTime = 0f;
        private Coroutine _soundCoroutine;

        void Awake()
        {
            InitRenderers();

            // สร้าง AudioSource อัตโนมัติถ้าไม่มี
            if (ghostAudioSource == null)
            {
                ghostAudioSource = gameObject.AddComponent<AudioSource>();
                ghostAudioSource.spatialBlend = 1f;
                ghostAudioSource.maxDistance  = 15f;
                ghostAudioSource.rolloffMode  = AudioRolloffMode.Linear;
                ghostAudioSource.playOnAwake  = false;
            }

            _wanderOrigin = transform.localPosition;
        }

        void Update()
        {
            if (!_isWandering) return;

            // Ghost wander drift
            if (enableWander)
            {
                _wanderTimer += Time.deltaTime;
                float ox = Mathf.Sin(_wanderTimer * wanderFrequency * Mathf.PI * 2f) * wanderAmplitudeX;
                float oy = Mathf.Sin(_wanderTimer * wanderFrequency * 0.7f * Mathf.PI * 2f) * wanderAmplitudeY;
                transform.localPosition = Vector3.Lerp(
                    transform.localPosition,
                    _wanderOrigin + new Vector3(ox, oy, 0f),
                    Time.deltaTime * wanderSpeed
                );
            }

            // Ambient ghost sounds
            if (ghostAmbientSounds != null && ghostAmbientSounds.Length > 0 &&
                Time.time >= _nextSoundTime)
            {
                PlayRandomAmbient();
                _nextSoundTime = Time.time + Random.Range(soundIntervalMin, soundIntervalMax);
            }
        }

        void InitRenderers()
        {
            if (_initialized) return;

            _renderers = GetComponentsInChildren<Renderer>(true);
            _materials = new Material[_renderers.Length][];

            for (int i = 0; i < _renderers.Length; i++)
            {
                Material[] mats = _renderers[i].materials;
                _materials[i] = new Material[mats.Length];

                for (int j = 0; j < mats.Length; j++)
                {
                    if (cloneMaterial)
                    {
                        _materials[i][j] = new Material(mats[j]);
                        // ตั้ง Shader Mode เป็น Transparent/Fade ถ้าเป็น Standard Shader
                        EnableTransparency(_materials[i][j]);
                    }
                    else
                    {
                        _materials[i][j] = mats[j];
                    }
                }

                _renderers[i].materials = _materials[i];
            }

            _initialized = true;
            SetAlpha(0f);
        }

        /// <summary>
        /// ตั้งค่าให้ Standard Shader รองรับ Transparency
        /// </summary>
        void EnableTransparency(Material mat)
        {
            // รองรับ Standard Shader
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 2f); // Fade mode
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            // รองรับ URP Lit Shader
            else if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
        }

        /// <summary>
        /// ตั้งค่า Alpha ทุก Renderer/Material พร้อมกัน
        /// </summary>
        public void SetAlpha(float alpha)
        {
            if (!_initialized) InitRenderers();

            _currentAlpha = Mathf.Clamp01(alpha);

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                for (int j = 0; j < _materials[i].Length; j++)
                {
                    Material mat = _materials[i][j];
                    if (mat == null) continue;

                    if (mat.HasProperty(alphaPropertyName))
                    {
                        Color c = mat.GetColor(alphaPropertyName);
                        // ผสม Tint สี
                        c.r = ghostTint.r;
                        c.g = ghostTint.g;
                        c.b = ghostTint.b;
                        c.a = _currentAlpha;
                        mat.SetColor(alphaPropertyName, c);
                    }
                    else if (mat.HasProperty("_BaseColor"))
                    {
                        // URP fallback
                        Color c = mat.GetColor("_BaseColor");
                        c.r = ghostTint.r;
                        c.g = ghostTint.g;
                        c.b = ghostTint.b;
                        c.a = _currentAlpha;
                        mat.SetColor("_BaseColor", c);
                    }
                }
            }
        }

        /// <summary>
        /// Coroutine Fade In ผีเข้ามา พร้อม wander + สุ่ม pose
        /// </summary>
        public IEnumerator FadeIn(float duration)
        {
            if (randomPoseOnAppear) RandomizePose();
            StartWander();

            float elapsed = 0f;
            float startAlpha = _currentAlpha;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetAlpha(Mathf.Lerp(startAlpha, 1f, t * t));
                yield return null;
            }

            SetAlpha(1f);
        }

        /// <summary>
        /// Coroutine Fade Out ผีออกไป พร้อมหยุด wander
        /// </summary>
        public IEnumerator FadeOut(float duration)
        {
            float elapsed = 0f;
            float startAlpha = _currentAlpha;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetAlpha(Mathf.Lerp(startAlpha, 0f, t));
                yield return null;
            }

            SetAlpha(0f);
            StopWander();
        }

        /// <summary>
        /// กระตุก Alpha แบบสุ่ม (เรียกทุก frame ระหว่างที่ผีโผล่อยู่)
        /// </summary>
        public void Flicker()
        {
            if (Random.value < flickerChance)
            {
                float flickerAlpha = Random.Range(flickerMinAlpha, flickerMaxAlpha);
                SetAlpha(flickerAlpha);
            }
        }

        // ─── Ghost Wander Control ─────────────────

        /// <summary>
        /// เริ่มให้ผีเคลื่อนที่ (เรียกตอน FadeIn)
        /// </summary>
        public void StartWander()
        {
            _wanderOrigin = transform.localPosition;
            _wanderTimer  = Random.Range(0f, 10f); // offset ต่างกันแต่ละครั้ง
            _isWandering  = true;
            _nextSoundTime = Time.time + Random.Range(0.5f, soundIntervalMin);
        }

        /// <summary>
        /// หยุดผีเคลื่อนที่และรีเซ็ตตำแหน่ง
        /// </summary>
        public void StopWander()
        {
            _isWandering = false;
            transform.localPosition = _wanderOrigin;
        }

        // ─── Pose Variants ────────────────────────

        /// <summary>
        /// สุ่ม pose จาก poseVariants list
        /// </summary>
        public void RandomizePose()
        {
            if (poseVariants == null || poseVariants.Count == 0) return;

            // ปิดทั้งหมดก่อน
            foreach (var p in poseVariants)
                if (p != null) p.SetActive(false);

            // เปิด 1 pose สุ่ม
            int idx = Random.Range(0, poseVariants.Count);
            if (poseVariants[idx] != null)
                poseVariants[idx].SetActive(true);
        }

        // ─── Audio ────────────────────────────────

        void PlayRandomAmbient()
        {
            if (ghostAudioSource == null || ghostAmbientSounds == null ||
                ghostAmbientSounds.Length == 0) return;

            AudioClip clip = ghostAmbientSounds[Random.Range(0, ghostAmbientSounds.Length)];
            if (clip != null)
                ghostAudioSource.PlayOneShot(clip, ghostSoundVolume);
        }



        void OnDestroy()
        {
            // ล้าง cloned materials เพื่อป้องกัน memory leak
            if (cloneMaterial && _materials != null)
            {
                foreach (var mats in _materials)
                    if (mats != null)
                        foreach (var mat in mats)
                            if (mat != null) Destroy(mat);
            }
        }
    }
}
