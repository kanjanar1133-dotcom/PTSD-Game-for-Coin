using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace HorrorGame
{
    /// <summary>
    /// HauntedDoorSpawner — Manager สุ่ม spawn ประตูหลอนตาม spawn points
    ///
    /// Setup:
    ///   1. วาง Script นี้ใน Scene (Singleton)
    ///   2. สร้าง Prefab ของ HauntedDoor_Type1 และ HauntedDoor_Type2
    ///      → ลาก Prefab ใส่ doorType1Prefab / doorType2Prefab
    ///   3. สร้าง Empty GameObjects ในตำแหน่งที่ต้องการ (หน้าผนัง/ประตู)
    ///      → ชี้ forward (+Z) ออกมาจากผนัง
    ///      → ลาก Transforms เข้า spawnPoints[]
    ///
    /// ContextMenu "Force Spawn Door Now" ใช้ Debug spawn ใน Play Mode ได้
    /// </summary>
    public class HauntedDoorSpawner : MonoBehaviour
    {
        public static HauntedDoorSpawner Instance { get; private set; }

        // ─── Prefabs ────────────────────────────────
        [Header("Prefabs")]
        [Tooltip("Prefab ของ HauntedDoor_Type1 (ประตูปิดสนิท)")]
        public GameObject doorType1Prefab;

        [Tooltip("Prefab ของ HauntedDoor_Type2 (ประตูเปิดนิดหน่อย + มือโผล่)")]
        public GameObject doorType2Prefab;

        // ─── Spawn Points ───────────────────────────
        [Header("Spawn Points")]
        [Tooltip("Empty GameObjects ที่ Designer กำหนดตำแหน่ง spawn ใน Scene\n" +
                 "แนะนำ: ชี้ forward (+Z) ออกมาจากผนัง")]
        public Transform[] spawnPoints;

        // ─── Timing ─────────────────────────────────
        [Header("Spawn Timing")]
        [Tooltip("รอกี่วินาทีขั้นต่ำก่อน spawn ครั้งต่อไป")]
        public float minSpawnInterval = 30f;

        [Tooltip("รอกี่วินาทีสูงสุดก่อน spawn ครั้งต่อไป")]
        public float maxSpawnInterval = 90f;

        [Tooltip("รอกี่วินาทีก่อนเริ่ม spawn ครั้งแรก (ให้ผู้เล่นตั้งหลักก่อน)")]
        public float initialDelay = 15f;

        // ─── Rules ──────────────────────────────────
        [Header("Spawn Rules")]
        [Tooltip("จำนวนประตูหลอนสูงสุดที่ active พร้อมกัน")]
        public int maxActiveDoors = 1;

        [Tooltip("ไม่ spawn ถ้าผู้เล่นอยู่ใกล้กว่าระยะนี้ (อาจเห็น spawn)")]
        public float minPlayerDistance = 6f;

        [Tooltip("ไม่ spawn ถ้าผู้เล่นอยู่ไกลกว่าระยะนี้ (เดินไปถึงไม่ทัน)")]
        public float maxPlayerDistance = 25f;

        // ─── Type Weights ───────────────────────────
        [Header("Type Weights")]
        [Range(0f, 1f)]
        [Tooltip("โอกาส spawn Type 2 (มือโผล่) — ที่เหลือเป็น Type 1")]
        public float type2Chance = 0.4f;

        // ─── Debug ────────────────────────────────────
        [Header("Debug")]
        public bool verboseLog = false;

        // ─── Startup Spawn ───────────────────────────
        [Header("Startup Spawn (เกิดทันทีเมื่อเริ่มเกม)")]
        [Tooltip("ประตูที่ต้องเกิดทันทีเมื่อเริ่มเกม (ไม่ขึ้นกับ Random Spawn Loop)")]
        public StartupSpawn[] startupSpawns;

        [System.Serializable]
        public class StartupSpawn
        {
            [Tooltip("ดัชนีของ Spawn Point ใน Array spawnPoints[] ที่ต้องการใช้")]
            public int spawnPointIndex = 0;

            [Tooltip("ถ้าเปิด = เกิดประตู Type 2 (มือโผล่), ปิด = Type 1 (ประตูปิดสนิท)")]
            public bool useType2 = false;

            [Tooltip("หน่วงกี่วินาทีหลังเริ่มเกมก่อนเกิด (0 = ทันที)")]
            public float delay = 0f;
        }


        // ─── Private ────────────────────────────────
        private readonly List<HauntedDoorBase> _activeDoors = new List<HauntedDoorBase>();
        private Transform _player;

        // ─────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            // หา Player
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
            else Debug.LogWarning("⚠️ [HauntedDoorSpawner] ไม่พบ Player (ต้องการ Tag 'Player')");

            // ตรวจ spawn points
            if (spawnPoints == null || spawnPoints.Length == 0)
                Debug.LogWarning("⚠️ [HauntedDoorSpawner] ไม่มี Spawn Points!");

            // Startup Spawns — เกิดทันทีตามที่กำหนด
            if (startupSpawns != null && startupSpawns.Length > 0)
                StartCoroutine(SpawnStartupDoors());

            StartCoroutine(SpawnLoop());
        }

        // ─────────────────────────────────────────────
        //  Startup Spawn
        // ─────────────────────────────────────────────

        IEnumerator SpawnStartupDoors()
        {
            foreach (StartupSpawn entry in startupSpawns)
            {
                if (entry.delay > 0f)
                    yield return new WaitForSeconds(entry.delay);

                // ตรวจ index ถูกต้อง
                if (spawnPoints == null || entry.spawnPointIndex >= spawnPoints.Length)
                {
                    Debug.LogWarning($"⚠️ [HauntedDoorSpawner] Startup Spawn Index {entry.spawnPointIndex} เกิน spawnPoints array!");
                    continue;
                }

                Transform point = spawnPoints[entry.spawnPointIndex];
                if (point == null) continue;

                GameObject prefab = entry.useType2 ? doorType2Prefab : doorType1Prefab;
                if (prefab == null)
                {
                    string typeName = entry.useType2 ? "Type2" : "Type1";
                    Debug.LogWarning($"⚠️ [HauntedDoorSpawner] Startup Spawn: Prefab {typeName} ยังไม่ได้ตั้งค่า!");
                    continue;
                }

                GameObject doorObj = Instantiate(prefab, point.position, point.rotation);
                HauntedDoorBase door = doorObj.GetComponent<HauntedDoorBase>();
                if (door != null)
                {
                    door.onReadyToVanish += () => _activeDoors.Remove(door);
                    _activeDoors.Add(door);
                }

                Debug.Log($"👻 [HauntedDoorSpawner] Startup Spawn Type{(entry.useType2 ? 2 : 1)} ที่ spawnPoint[{entry.spawnPointIndex}]");
            }
        }


        // ─────────────────────────────────────────────
        //  Spawn Loop
        // ─────────────────────────────────────────────

        IEnumerator SpawnLoop()
        {
            yield return new WaitForSeconds(initialDelay);

            while (true)
            {
                float waitTime = Random.Range(minSpawnInterval, maxSpawnInterval);
                if (verboseLog) Debug.Log($"🕐 [HauntedDoorSpawner] รอ {waitTime:F1}s ก่อน spawn ครั้งต่อไป");

                yield return new WaitForSeconds(waitTime);
                TrySpawnDoor();
            }
        }

        // ─────────────────────────────────────────────
        //  Spawn Logic
        // ─────────────────────────────────────────────

        void TrySpawnDoor(bool ignoreDistanceCheck = false)
        {
            // ลบ reference ที่ถูก destroy ไปแล้ว
            _activeDoors.RemoveAll(d => d == null);

            if (_activeDoors.Count >= maxActiveDoors)
            {
                Debug.Log("🚪 [HauntedDoorSpawner] SKIP: มีประตูหลอน active อยู่แล้ว " +
                          $"({_activeDoors.Count}/{maxActiveDoors})");
                return;
            }

            if (_player == null)
            {
                Debug.LogWarning("⚠️ [HauntedDoorSpawner] SKIP: ไม่พบ _player!");
                return;
            }

            // เลือก spawn point ที่เหมาะสม
            Transform spawnPoint = GetValidSpawnPoint(ignoreDistanceCheck);
            if (spawnPoint == null)
            {
                Debug.LogWarning("⚠️ [HauntedDoorSpawner] SKIP: ไม่มี spawn point ที่ผ่านเงื่อนไข\n" +
                                 "→ ตรวจว่า spawnPoints[] ถูกลากใส่ใน Inspector แล้ว\n" +
                                 "→ ตรวจระยะ min/maxPlayerDistance vs ระยะจริงของ spawn point");
                return;
            }

            // เลือก type
            bool       useType2 = doorType2Prefab != null && Random.value < type2Chance;
            GameObject prefab   = useType2 ? doorType2Prefab : doorType1Prefab;

            if (prefab == null)
            {
                Debug.LogWarning($"⚠️ [HauntedDoorSpawner] SKIP: Prefab Type {(useType2 ? 2 : 1)} เป็น null!\n" +
                                 "→ ลาก Prefab ใส่ doorType1Prefab หรือ doorType2Prefab ใน Inspector");
                return;
            }

            // Instantiate
            GameObject      doorObj = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            HauntedDoorBase door    = doorObj.GetComponent<HauntedDoorBase>();

            if (door == null)
            {
                Debug.LogWarning("⚠️ [HauntedDoorSpawner] SKIP: Prefab ไม่มี HauntedDoorBase component!");
                Destroy(doorObj);
                return;
            }

            // Subscribe callback
            door.onReadyToVanish += () =>
            {
                _activeDoors.Remove(door);
                Debug.Log("💨 [HauntedDoorSpawner] ประตูหลอนหายไปแล้ว");
            };

            _activeDoors.Add(door);
            Debug.Log($"🚪 [HauntedDoorSpawner] ✅ Spawn Type {(useType2 ? 2 : 1)} ที่ '{spawnPoint.name}'");
        }

        // ─────────────────────────────────────────────
        //  Spawn Point Selection
        // ─────────────────────────────────────────────

        Transform GetValidSpawnPoint(bool ignoreDistanceCheck = false)
        {
            if (spawnPoints == null || spawnPoints.Length == 0 || _player == null)
                return null;

            // สับรายการก่อนเพื่อสุ่มแบบ fair
            List<Transform> candidates = new List<Transform>(spawnPoints);
            Shuffle(candidates);

            foreach (Transform sp in candidates)
            {
                if (sp == null) continue;

                if (!ignoreDistanceCheck)
                {
                    float dist = Vector3.Distance(sp.position, _player.position);
                    if (dist < minPlayerDistance)
                    {
                        if (verboseLog)
                            Debug.Log($"  ↳ '{sp.name}' ใกล้เกินไป ({dist:F1}m < {minPlayerDistance}m)");
                        continue;
                    }
                    if (dist > maxPlayerDistance)
                    {
                        if (verboseLog)
                            Debug.Log($"  ↳ '{sp.name}' ไกลเกินไป ({dist:F1}m > {maxPlayerDistance}m)");
                        continue;
                    }
                }

                // ไม่ spawn ทับกับประตูที่มีอยู่แล้ว
                if (IsNearActiveDoor(sp.position, 1.5f))
                    continue;

                return sp;
            }
            return null;
        }

        bool IsNearActiveDoor(Vector3 pos, float threshold)
        {
            foreach (var door in _activeDoors)
            {
                if (door == null) continue;
                if (Vector3.Distance(door.transform.position, pos) < threshold)
                    return true;
            }
            return false;
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ─────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Force spawn ทันที โดยข้าม distance check
        /// ใช้สำหรับ Debug ใน Play Mode (Right-click component → Force Spawn)
        /// </summary>
        [UnityEngine.ContextMenu("Force Spawn Door Now (Bypass Distance)")]
        public void ForceSpawn()
        {
            Debug.Log("🔧 [HauntedDoorSpawner] Force Spawn เริ่ม (bypass distance check)...");
            TrySpawnDoor(ignoreDistanceCheck: true);
        }

        /// <summary>
        /// Force spawn Type 1 (ปิดสนิท) ที่ spawn point แรก
        /// </summary>
        [UnityEngine.ContextMenu("Force Spawn Type 1")]
        public void ForceSpawnType1() => ForceSpawnAt(0, false);

        /// <summary>
        /// Force spawn Type 2 (มือโผล่) ที่ spawn point แรก
        /// </summary>
        [UnityEngine.ContextMenu("Force Spawn Type 2 (Hand)")]
        public void ForceSpawnType2() => ForceSpawnAt(0, true);

        /// <summary>
        /// แสดง diagnostic state ใน Console
        /// </summary>
        [UnityEngine.ContextMenu("Print Diagnostic Info")]
        public void PrintDiagnostic()
        {
            _activeDoors.RemoveAll(d => d == null);
            Debug.Log("═══ HauntedDoorSpawner Diagnostic ═══");
            Debug.Log($"  Player found   : {(_player != null ? _player.name : "NULL ⚠️")}");
            Debug.Log($"  Spawn points   : {(spawnPoints != null ? spawnPoints.Length : 0)}");
            Debug.Log($"  Active doors   : {_activeDoors.Count} / {maxActiveDoors}");
            Debug.Log($"  Type1 Prefab   : {(doorType1Prefab != null ? doorType1Prefab.name : "NULL ⚠️")}");
            Debug.Log($"  Type2 Prefab   : {(doorType2Prefab != null ? doorType2Prefab.name : "NULL ⚠️")}");

            if (spawnPoints != null && _player != null)
            {
                foreach (var sp in spawnPoints)
                {
                    if (sp == null) continue;
                    float dist = Vector3.Distance(sp.position, _player.position);
                    bool ok = dist >= minPlayerDistance && dist <= maxPlayerDistance;
                    Debug.Log($"  SpawnPoint '{sp.name}' — dist {dist:F1}m  [{(ok ? "✅ OK" : $"❌ ต้องอยู่ระหว่าง {minPlayerDistance}-{maxPlayerDistance}m")}]");
                }
            }
            Debug.Log("══════════════════════════════════════");
        }

        /// <summary>
        /// Force spawn ประเภทที่กำหนด ที่ spawn point index ที่กำหนด (bypass distance)
        /// </summary>
        public void ForceSpawnAt(int spawnIndex, bool type2)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning("⚠️ [HauntedDoorSpawner] ไม่มี spawnPoints!");
                return;
            }
            if (spawnIndex >= spawnPoints.Length)
            {
                Debug.LogWarning($"⚠️ [HauntedDoorSpawner] spawnIndex {spawnIndex} เกิน array!");
                return;
            }

            _activeDoors.RemoveAll(d => d == null);

            GameObject prefab = type2 ? doorType2Prefab : doorType1Prefab;
            if (prefab == null)
            {
                Debug.LogWarning($"⚠️ [HauntedDoorSpawner] Prefab Type {(type2 ? 2 : 1)} เป็น null!");
                return;
            }

            Transform  sp   = spawnPoints[spawnIndex];
            GameObject obj  = Instantiate(prefab, sp.position, sp.rotation);
            HauntedDoorBase door = obj.GetComponent<HauntedDoorBase>();

            if (door == null) { Destroy(obj); return; }

            door.onReadyToVanish += () => _activeDoors.Remove(door);
            _activeDoors.Add(door);
            Debug.Log($"🚪 [HauntedDoorSpawner] ✅ Force spawn Type {(type2 ? 2 : 1)} ที่ '{sp.name}'");
        }

        // ─────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────

        void OnDrawGizmosSelected()
        {
            if (spawnPoints == null) return;

            foreach (Transform sp in spawnPoints)
            {
                if (sp == null) continue;

                // วาดกรอบประตู
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
                Gizmos.matrix = Matrix4x4.TRS(sp.position, sp.rotation, Vector3.one);
                Gizmos.DrawWireCube(new Vector3(0f, 1.1f, 0f), new Vector3(0.9f, 2.2f, 0.1f));
                Gizmos.matrix = Matrix4x4.identity;

                // วาดลูกศรทิศ forward
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(sp.position, sp.position + sp.forward * 0.6f);
                Gizmos.DrawWireSphere(sp.position + sp.forward * 0.6f, 0.05f);
            }

            // วาดระยะ min/max ของ Player
            if (Application.isPlaying && _player != null)
            {
                Gizmos.color = new Color(0f, 1f, 0f, 0.08f);
                Gizmos.DrawWireSphere(_player.position, minPlayerDistance);
                Gizmos.color = new Color(1f, 1f, 0f, 0.05f);
                Gizmos.DrawWireSphere(_player.position, maxPlayerDistance);
            }
        }
    }
}
