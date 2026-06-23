using UnityEngine;
using System.Collections;

namespace HorrorGame
{
    /// <summary>
    /// HauntedDoor_Type2 — ประตูหลอนที่เปิดแงมไว้ล่วงหน้า (วางไว้ใน Prefab เอง)
    ///
    /// ไม่มีโค้ดหมุน/ปิดประตู — Prefab วางโมเดลและสี่เหลี่ยมปิดด้านหลังไว้เอง
    ///
    /// Sequence:
    ///   ประตูปรากฏ (fade in) — ประตูอยู่ตามที่ Designer วางไว้
    ///   → ผู้เล่นเดินเข้ามาใกล้ (scareDistance)
    ///   → CUTSCENE:
    ///       1. ล็อกการเคลื่อนที่ผู้เล่น
    ///       2. มือโผล่ออกมา + เสียง
    ///       3. กล้องถอยหลัง (pushback) + camera shake
    ///       4. เสียงหายใจแรง (PTSDSymptom) → เพิ่ม Noise → ผีได้ยิน
    ///       5. มือหดกลับ → ปลดล็อกผู้เล่น
    ///   → ผู้เล่นหันออก → ประตูหาย (flicker)
    ///
    /// Setup Prefab:
    ///   DoorRoot (HauntedDoor_Type2)
    ///     ├─ DoorMesh   [MeshRenderer + Collider]  — วางเปิดแงมไว้ตามต้องการ
    ///     ├─ BlackQuad  [MeshRenderer]  — สี่เหลี่ยมดำปิดด้านหลัง
    ///     └─ HandObject [Capsule / mesh มือ]  ← ลาก Transform ใส่ช่อง handObject
    ///   + AudioSource บน DoorRoot
    /// </summary>
    public class HauntedDoor_Type2 : HauntedDoorBase
    {
        // ─── Scare Trigger ───────────────────────────
        [Header("Type 2 — Scare Distance")]
        [Tooltip("ระยะที่ผู้เล่นต้องเข้ามาจึง trigger cutscene (เมตร)")]
        public float scareDistance = 2.5f;

        // ─── Hand ────────────────────────────────────
        [Header("Type 2 — Hand Object")]
        [Tooltip("Transform ของ HandObject (Capsule placeholder / mesh มือ)")]
        public Transform handObject;

        [Tooltip("ตำแหน่ง local ของมือตอนซ่อนอยู่ (หลังบานประตู)")]
        public Vector3 handHiddenLocalPos = new Vector3(0f, 1.1f, -0.1f);

        [Tooltip("ตำแหน่ง local ของมือตอนโผล่ออกมาด้านหน้า")]
        public Vector3 handScareLocalPos  = new Vector3(0f, 1.1f, 0.6f);

        [Tooltip("ความเร็วมือพุ่งออก (สูง = ทำให้ตกใจมากขึ้น)")]
        public float handPopSpeed     = 10f;

        [Tooltip("เวลาที่มือค้างอยู่ก่อนหดกลับ (วินาที)")]
        public float handHoldTime     = 0.35f;

        [Tooltip("ความเร็วมือหดกลับ")]
        public float handRetractSpeed = 4f;

        // ─── Player Reaction ─────────────────────────
        [Header("Type 2 — Player Reaction")]
        [Tooltip("ระยะที่ผู้เล่นถอยออกมาตอนตกใจ (เมตร)")]
        public float pushbackDistance = 1.2f;

        [Tooltip("เวลาที่ใช้ถอย (วินาที)")]
        public float pushbackDuration = 0.4f;

        // ─── Noise Alert ─────────────────────────────
        [Header("Type 2 — Noise Alert")]
        [Tooltip("ค่า Noise ที่เพิ่มเข้า NoiseManager ตอนหายใจแรง (ผีจะได้ยิน)")]
        public float breathingNoiseAmount = 80f;

        // ─── Audio ───────────────────────────────────
        [Header("Type 2 — Audio")]
        [Tooltip("เสียงตอนมือโผล่ (เสียงฉับ / กรีดร้อง)")]
        public AudioClip handScareSound;

        // ─── Private ─────────────────────────────────
        private bool _scareDone   = false;
        private bool _scareActive = false;

        // ─────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────

        protected override void Start()
        {
            base.Start();

            // ซ่อน HandObject ก่อน
            if (handObject != null)
            {
                handObject.localPosition = handHiddenLocalPos;
                handObject.gameObject.SetActive(false);
            }
        }

        protected override void Update()
        {
            base.Update();  // FOV tracking + vanish check

            if (_isVanishing || _scareActive || _scareDone) return;
            if (_playerTransform == null) return;

            // ตรวจระยะผู้เล่น → trigger cutscene
            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist <= scareDistance)
            {
                _scareActive = true;
                _scareDone   = true;
                StartCoroutine(CutsceneSequence());
            }
        }

        // ─────────────────────────────────────────────
        //  CanVanish — รอ cutscene เสร็จก่อน
        // ─────────────────────────────────────────────

        protected override bool CanVanish() => _scareDone;

        // ─────────────────────────────────────────────
        //  Cutscene Sequence
        // ─────────────────────────────────────────────

        IEnumerator CutsceneSequence()
        {
            Debug.Log("🎬 [HauntedDoor_Type2] Cutscene เริ่ม!");

            // ── 1. ล็อกผู้เล่น ───────────────────────
            Movement playerMovement = _playerTransform.GetComponentInChildren<Movement>();
            if (playerMovement != null) playerMovement.enabled = false;

            // ── 2. มือโผล่ออกมา ──────────────────────
            yield return StartCoroutine(HandPopOut());

            // ── 3. ผู้เล่นถอยหลัง + หายใจแรง ────────
            yield return StartCoroutine(PlayerReaction());

            // ── 4. มือหดกลับ + ซ่อน ──────────────────
            yield return StartCoroutine(HandRetract());

            // ── 5. ปลดล็อกผู้เล่น ────────────────────
            if (playerMovement != null) playerMovement.enabled = true;

            _scareActive = false;
            Debug.Log("🚪 [HauntedDoor_Type2] Cutscene จบ — รอผู้เล่นหัน");
        }

        // ─────────────────────────────────────────────
        //  Hand Routines
        // ─────────────────────────────────────────────

        IEnumerator HandPopOut()
        {
            if (handObject == null) yield break;

            handObject.localPosition = handHiddenLocalPos;
            handObject.gameObject.SetActive(true);
            PlaySound(handScareSound);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * handPopSpeed;
                handObject.localPosition = Vector3.Lerp(
                    handHiddenLocalPos,
                    handScareLocalPos,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            handObject.localPosition = handScareLocalPos;

            yield return new WaitForSeconds(handHoldTime);
        }

        IEnumerator HandRetract()
        {
            if (handObject == null) yield break;

            Vector3 retractFrom = handObject.localPosition;
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime * handRetractSpeed;
                handObject.localPosition = Vector3.Lerp(
                    retractFrom, handHiddenLocalPos, Mathf.Clamp01(t));
                yield return null;
            }

            handObject.gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────
        //  Player Reaction
        // ─────────────────────────────────────────────

        IEnumerator PlayerReaction()
        {
            // Pushback — ดัน player ออกจากประตู
            if (_playerTransform != null && pushbackDistance > 0f)
            {
                Vector3 pushDir = (_playerTransform.position - transform.position).normalized;
                pushDir.y = 0f;
                if (pushDir == Vector3.zero) pushDir = -transform.forward;

                CharacterController cc = _playerTransform.GetComponentInChildren<CharacterController>();
                Vector3 startPos  = _playerTransform.position;
                Vector3 targetPos = startPos + pushDir * pushbackDistance;

                float elapsed = 0f;
                while (elapsed < pushbackDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / pushbackDuration);

                    if (cc != null)
                        cc.Move((targetPos - _playerTransform.position) *
                                Time.deltaTime / Mathf.Max(pushbackDuration - elapsed, 0.01f));
                    else
                        _playerTransform.position = Vector3.Lerp(startPos, targetPos, t);

                    yield return null;
                }
            }

            // Camera shake เบาๆ
            if (_cam != null)
            {
                float shakeDur = 0.5f;
                float elapsed  = 0f;
                Vector3 originLocal = _cam.transform.localPosition;

                while (elapsed < shakeDur)
                {
                    elapsed += Time.deltaTime;
                    float intensity = Mathf.Lerp(0.06f, 0f, elapsed / shakeDur);
                    _cam.transform.localPosition = originLocal + Random.insideUnitSphere * intensity;
                    yield return null;
                }
                _cam.transform.localPosition = originLocal;
            }

            // PTSD Symptom (เสียงหายใจแรง + visual effect)
            if (PTSDSymptomManager.Instance != null)
            {
                Debug.Log("😱 [HauntedDoor_Type2] TriggerSymptom!");
                PTSDSymptomManager.Instance.TriggerSymptom();
            }

            // เพิ่ม Noise → ผีได้ยินเสียงหายใจ
            if (NoiseManager.Instance != null)
            {
                Debug.Log($"🔊 [HauntedDoor_Type2] AddNoise {breathingNoiseAmount} → ผีได้ยิน!");
                NoiseManager.Instance.AddNoise(breathingNoiseAmount);
            }

            yield return new WaitForSeconds(0.3f);
        }

        // ─────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // ระยะ trigger cutscene
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, scareDistance);

            // ตำแหน่งมือ
            if (handObject != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.TransformPoint(handScareLocalPos), 0.08f);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.TransformPoint(handHiddenLocalPos), 0.05f);
            }
        }
    }
}
