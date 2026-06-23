using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

namespace HorrorGame
{
    public class EnemyAI : MonoBehaviour
    {
        [Header("References")]
        public Transform player;
        public List<Transform> patrolWaypoints;
        public List<RoomArea> rooms;
        private NavMeshAgent agent;
        private Movement playerMovement;

        [Header("Footstep Audio")]
        [Tooltip("AudioClip เสียงฝีเท้าของผี (ใส่หลายเสียงสลับกันได้)")]
        public AudioClip[] footstepClips;
        [Tooltip("ระยะที่ได้ยินเสียงเต็ม (เมตร)")]
        public float footstepMinDistance = 5f;
        [Tooltip("ระยะที่ไม่ได้ยินเสียงเลย (เมตร)")]
        public float footstepMaxDistance = 20f;
        [Tooltip("ความดังสูงสุด (0-1)")]
        public float footstepMaxVolume = 0.85f;
        [Tooltip("ช่วงเวลาระหว่างก้าว (วินาที) ตอน patrol")]
        public float footstepIntervalPatrol = 0.55f;
        [Tooltip("ช่วงเวลาระหว่างก้าว (วินาที) ตอน chase")]
        public float footstepIntervalChase  = 0.32f;

        private AudioSource _footstepSource;
        private float _footstepTimer = 0f;
        private int   _footstepIndex = 0;

        [Header("Detection Settings")]
        public float viewDistance = 15f;
        public float viewAngle = 360f;
        public float catchDistance = 2f;
        public LayerMask obstacleMask;

        [Header("Detection Meter")]
        [Tooltip("เวลา (วินาที) ที่ใช้เติม meter จนเต็มตอนเห็นผู้เล่นต่อเนื่อง")]
        public float detectionFillTime   = 2.5f;
        [Tooltip("เวลา (วินาที) ที่ meter ลดจนหมดตอนไม่เห็น")]
        public float detectionDrainTime  = 4f;
        [Tooltip("% ที่ meter ถือว่าเข้าสู่ Suspicious (0-1)")]
        public float suspiciousThreshold = 0.3f;

        [Header("Sound Detection")]
        public float noiseDetectionRange = 15f;
        public float noiseSensitivity = 15f;
        private Vector3? lastNoisePosition = null;

        [Header("Movement Settings")]
        public float patrolSpeed = 2f;
        public float chaseSpeed = 4.5f;
        [Tooltip("ความเร็ววิ่งไปจุดเสียง")]
        public float investigateRunSpeed = 3.5f;

        [Header("Room Wander Settings")]
        [Tooltip("จำนวนจุดที่สุ่มเดินในห้อง (ก่อนออก)")]
        public int minWanderPoints = 2;
        public int maxWanderPoints = 4;
        [Tooltip("เวลาหยุดรอ (วินาที) ที่แต่ละจุดในห้อง")]
        public float wanderWaitMin = 0.8f;
        public float wanderWaitMax = 2.5f;

        [Header("Noise Investigation Settings")]
        [Tooltip("เวลา (วินาที) ที่ผีใช้หันก่อนวิ่งไปจุดเสียง")]
        public float turnBeforeRunTime = 0.6f;

        [Header("Door Settings")]
        [Tooltip("ระยะ (เมตร) ที่ผีต้องอยู่ใกล้ประตูก่อนถึงจะเปิดได้")]
        public float doorInteractDistance = 1.8f;

        // ---- Detection Meter State ----
        private float _detectionMeter  = 0f;   // 0 = ไม่รู้ตัว, 1 = จับได้
        private bool  _isSuspicious    = false;
        private bool  isChasing = false;
        private bool  isInteractingWithDoor = false;
        private bool  isGameOver = false;
        private BaseDoor[] allDoors;

        public float DetectionMeter => _detectionMeter;
        public bool  IsSuspicious   => _isSuspicious;

        [System.Serializable]
        public class RoomArea
        {
            public string roomName;
            public BaseDoor door;
            public Transform entrancePoint;
            public List<Transform> insidePoints;
        }

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            if (player == null)
            {
                GameObject pObj = GameObject.FindGameObjectWithTag("Player");
                if (pObj != null) player = pObj.transform;
            }
            if (player != null) playerMovement = player.GetComponentInChildren<Movement>();

            allDoors = FindObjectsByType<BaseDoor>(FindObjectsSortMode.None);

            // ---- ตั้งค่า AudioSource สำหรับเสียงฝีเท้า ----
            _footstepSource = gameObject.AddComponent<AudioSource>();
            _footstepSource.spatialBlend  = 1f;   // 3D audio เต็ม
            _footstepSource.rolloffMode   = AudioRolloffMode.Custom;
            _footstepSource.minDistance   = footstepMinDistance;
            _footstepSource.maxDistance   = footstepMaxDistance;
            _footstepSource.playOnAwake   = false;
            _footstepSource.loop          = false;
            _footstepSource.volume        = footstepMaxVolume;

            Debug.Log("👻 [EnemyAI] เริ่มระบบลาดตระเวนแล้ว (พบประตู " + allDoors.Length + " บาน)");
            StartCoroutine(PatrolRoutine());
        }

        void Update()
        {
            if (player == null || playerMovement == null || isGameOver) return;

            bool canSeePlayer = CheckLineOfSight();
            bool playerHiding = playerMovement.IsHiding();

            // ---- ชนผี = จบทันที (ใช้ OverlapSphere ไม่ขึ้นกับ Collider ของผี) ----
            Collider[] hits = Physics.OverlapSphere(transform.position, catchDistance);
            foreach (Collider col in hits)
            {
                bool isPlayer = col.CompareTag("Player")
                             || col.GetComponentInParent<Movement>() != null
                             || col.GetComponent<Movement>() != null;
                if (isPlayer && !playerHiding && !isGameOver)
                {
                    Debug.Log($"💀 [EnemyAI] OverlapSphere จับผู้เล่นได้! col={col.name} Game Over!");
                    StartCoroutine(GameOverSequence());
                    return;
                }
            }

            // ---- fallback: flat XZ distance เผื่อความแม่นยำ ----
            Vector3 ghostFlat  = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 playerFlat = new Vector3(player.position.x,    0f, player.position.z);
            float distToPlayer = Vector3.Distance(ghostFlat, playerFlat);
            if (distToPlayer <= catchDistance && !playerHiding && !isGameOver)
            {
                Debug.Log($"💀 [EnemyAI] flatDist จับผู้เล่น (dist={distToPlayer:F2}) Game Over!");
                StartCoroutine(GameOverSequence());
                return;
            }

            // ---- Debug: แสดงระยะทุก 90 เฟรม เพื่อตรวจสอบใน Console ----
            if (Time.frameCount % 90 == 0)
                Debug.Log($"👻 [EnemyAI] ระยะถึงผู้เล่น: {distToPlayer:F2}m (ต้องน้อยกว่า {catchDistance}m ถึงจะตาย)");

            UpdateDetectionMeter(canSeePlayer, playerHiding);

            // เมื่อ meter เต็ม → Game Over
            if (_detectionMeter >= 1f && !isGameOver)
            {
                Debug.Log("👁️ [EnemyAI] Detection เต็ม! Game Over!");
                StartCoroutine(GameOverSequence());
                return;
            }

            if (isChasing)
            {
                if (!_isSuspicious || playerHiding)
                {
                    Debug.Log("❓ [EnemyAI] คลาดสายตา กลับสู่โหมดลาดตระเวน");
                    isChasing = false;
                }
            }

            // เข้า Chase ตอน suspicious เพื่อไล่ตาม
            if (_isSuspicious && canSeePlayer && !playerHiding && !isInteractingWithDoor)
                isChasing = true;

            if (isChasing) ChasePlayer();
            else CheckForNoise();

            UpdateFootsteps();
        }

        void UpdateFootsteps()
        {
            if (footstepClips == null || footstepClips.Length == 0) return;
            if (_footstepSource == null) return;

            // เล่นเสียงเฉพาะตอนผีกำลังเดิน (velocity > threshold)
            bool isMoving = agent.velocity.magnitude > 0.3f && !agent.isStopped;
            if (!isMoving) { _footstepTimer = 0f; return; }

            float interval = isChasing ? footstepIntervalChase : footstepIntervalPatrol;
            _footstepTimer -= Time.deltaTime;

            if (_footstepTimer <= 0f)
            {
                _footstepTimer = interval;

                // คำนวณ volume ตามระยะ (linear inverse)
                float dist = Vector3.Distance(transform.position, player.position);
                float t = Mathf.InverseLerp(footstepMinDistance, footstepMaxDistance, dist);
                float vol = Mathf.Lerp(footstepMaxVolume, 0f, t);

                if (vol > 0.01f)
                {
                    // สลับ clip แต่ละก้าว
                    AudioClip clip = footstepClips[_footstepIndex % footstepClips.Length];
                    _footstepIndex++;
                    _footstepSource.PlayOneShot(clip, vol);
                }
            }
        }

        void UpdateDetectionMeter(bool canSee, bool hiding)
        {
            bool shouldFill = canSee && !hiding && !isInteractingWithDoor;

            if (shouldFill)
            {
                // เติม meter ตามเวลา
                _detectionMeter += Time.deltaTime / detectionFillTime;
            }
            else
            {
                // ลด meter ตามเวลา
                _detectionMeter -= Time.deltaTime / detectionDrainTime;
            }

            _detectionMeter = Mathf.Clamp01(_detectionMeter);
            _isSuspicious   = _detectionMeter >= suspiciousThreshold;

            // ส่งค่าให้ UI
            if (DetectionIndicatorUI.Instance != null)
                DetectionIndicatorUI.Instance.SetDetectionLevel(_detectionMeter);
        }

        void CheckForNoise()
        {
            if (NoiseManager.Instance == null || playerMovement.IsHiding() || isGameOver) return;

            float noise = NoiseManager.Instance.currentNoise;
            float dist = Vector3.Distance(transform.position, player.position);

            if (noise > noiseSensitivity && dist < noiseDetectionRange)
            {
                lastNoisePosition = player.position;
            }
        }

        bool IsNoiseSignificant(Vector3 currentDestination)
        {
            if (!lastNoisePosition.HasValue) return false;
            return Vector3.Distance(currentDestination, lastNoisePosition.Value) > 2f;
        }

        // ---- Shuffle a list in place ----
        void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        IEnumerator PatrolRoutine()
        {
            while (!isGameOver)
            {
                if (isChasing) { yield return new WaitUntil(() => !isChasing || isGameOver); }
                if (isGameOver) yield break;

                float decision = Random.value;
                if (lastNoisePosition.HasValue)
                {
                    Vector3 target = lastNoisePosition.Value;
                    lastNoisePosition = null;
                    Debug.Log("🔍 [EnemyAI] ได้ยินเสียง! กำลังหันและวิ่งไปตรวจสอบ...");
                    yield return StartCoroutine(InvestigateNoise(target));
                }
                else if (decision < 0.6f && rooms.Count > 0)
                {
                    RoomArea targetRoom = rooms[Random.Range(0, rooms.Count)];
                    Debug.Log("🏠 [EnemyAI] กำลังมุ่งหน้าไปสำรวจห้อง: " + targetRoom.roomName);
                    yield return StartCoroutine(ExploreRoom(targetRoom));
                }
                else if (patrolWaypoints.Count > 0)
                {
                    Transform wp = patrolWaypoints[Random.Range(0, patrolWaypoints.Count)];
                    int retry = 0;
                    while (Vector3.Distance(transform.position, wp.position) < 3f && retry < 5 && patrolWaypoints.Count > 1)
                    {
                        wp = patrolWaypoints[Random.Range(0, patrolWaypoints.Count)];
                        retry++;
                    }
                    Debug.Log("🛤️ [EnemyAI] กำลังเดินไปจุดลาดตระเวน: " + wp.name);
                    yield return StartCoroutine(MoveToDestination(wp.position));
                    yield return StartCoroutine(LookAround());
                }

                yield return null;
            }
        }

        // ---- หันไปทางเสียงก่อน แล้ววิ่งไปตรวจสอบ ----
        IEnumerator InvestigateNoise(Vector3 target)
        {
            if (isChasing || isGameOver) yield break;

            // หยุดเดิน แล้วหันไปทางเสียงก่อน
            agent.isStopped = true;
            Debug.Log("👂 [EnemyAI] หันไปทางเสียง...");
            yield return StartCoroutine(TurnTowards(target, turnBeforeRunTime));

            if (isChasing || isGameOver) yield break;

            // วิ่งไปจุดเสียงด้วยความเร็วพิเศษ
            Debug.Log("🏃 [EnemyAI] วิ่งไปตรวจสอบจุดที่เกิดเสียง!");
            yield return StartCoroutine(MoveToDestination(target, 0.7f, investigateRunSpeed));

            // ถึงแล้ว → มองรอบๆ
            if (!isChasing && !isGameOver)
            {
                yield return StartCoroutine(LookAround());
            }
        }

        // ---- หันไปทางเป้าหมายแบบ smooth ----
        IEnumerator TurnTowards(Vector3 target, float duration)
        {
            Vector3 dir = (target - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) yield break;

            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            float elapsed = 0f;
            Quaternion startRot = transform.rotation;

            while (elapsed < duration)
            {
                if (isChasing || isGameOver) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            transform.rotation = targetRot;
        }

        IEnumerator LookAround()
        {
            Debug.Log("👀 [EnemyAI] กำลังกวาดสายตามองรอบๆ...");
            agent.isStopped = true;

            float startY = transform.eulerAngles.y;
            float[] angles = { 45, -45, 0 };

            foreach (float angle in angles)
            {
                float targetY = startY + angle;
                Quaternion targetRot = Quaternion.Euler(0, targetY, 0);
                float t = 0;
                while (t < 1f)
                {
                    if (isChasing) yield break;
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
                    t += Time.deltaTime * 2f;
                    yield return null;
                }
                yield return new WaitForSeconds(0.8f);
            }

            agent.isStopped = false;
        }

        IEnumerator ExploreRoom(RoomArea room)
        {
            if (room.door == null || room.entrancePoint == null) yield break;

            // ใช้ helper เดียวกันกับ HandleDoorSequence
            if (!IsGhostAccessibleDoor(room.door))
            {
                Debug.Log("🔒 [EnemyAI] ห้อง " + room.roomName + " เปิดไม่ได้ ข้ามไปจุดอื่น");
                yield break;
            }

            yield return StartCoroutine(MoveToDestination(room.entrancePoint.position));
            yield return StartCoroutine(MoveToDestination(room.door.transform.position, 1.2f));

            // เช็คล็อคอีกครั้งตอนถึงประตูจริงๆ (กรณีผู้เล่นล็อคระหว่างทาง)
            if (!IsGhostAccessibleDoor(room.door))
            {
                Debug.Log("🔒 [EnemyAI] ประตู " + room.roomName + " ถูกล็อคตอนผีเดินมาถึง");
                yield return StartCoroutine(MoveToDestination(room.entrancePoint.position));
                yield break;
            }

            if (!room.door.open) yield return StartCoroutine(HandleDoorSequence(room.door));

            // ---- สุ่มลำดับ insidePoints แล้วเลือกเดิน N จุด ----
            if (room.insidePoints != null && room.insidePoints.Count > 0)
            {
                List<Transform> shuffled = new List<Transform>(room.insidePoints);
                ShuffleList(shuffled);

                int count = Mathf.Clamp(Random.Range(minWanderPoints, maxWanderPoints + 1), 0, shuffled.Count);
                Debug.Log($"🚶 [EnemyAI] สำรวจห้อง '{room.roomName}' → จะเดิน {count} จุดสุ่ม");

                for (int i = 0; i < count; i++)
                {
                    if (isChasing || isGameOver) break;
                    Transform pt = shuffled[i];
                    yield return StartCoroutine(MoveToDestination(pt.position));
                    if (isChasing || isGameOver) break;
                    float waitTime = Random.Range(wanderWaitMin, wanderWaitMax);
                    yield return new WaitForSeconds(waitTime);
                    // มีโอกาส 40% กวาดสายตาที่จุดนั้น
                    if (!isChasing && !isGameOver && Random.value < 0.4f)
                        yield return StartCoroutine(LookAround());
                }
            }

            if (!isChasing && !isGameOver)
            {
                yield return StartCoroutine(MoveToDestination(room.door.transform.position, 1.2f));
                if (room.door.open) yield return StartCoroutine(HandleDoorSequence(room.door));
                yield return StartCoroutine(MoveToDestination(room.entrancePoint.position));
            }
        }

        // ---- MoveToDestination รองรับ override speed ----
        IEnumerator MoveToDestination(Vector3 target, float stopDistance = 0.7f, float overrideSpeed = -1f)
        {
            if (isChasing || isGameOver) yield break;

            agent.isStopped = false;
            agent.speed = overrideSpeed > 0f ? overrideSpeed : patrolSpeed;
            agent.stoppingDistance = stopDistance;
            agent.SetDestination(target);

            yield return new WaitUntil(() => !agent.pathPending || isGameOver);
            if (isGameOver) yield break;

            float timeout = 12f;
            while (agent.remainingDistance > stopDistance + 0.1f && !isChasing && !isGameOver && timeout > 0)
            {
                if (IsNoiseSignificant(target)) yield break;
                yield return StartCoroutine(CheckForDoorsInWay());
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        // ---- เช็คว่าผีสามารถเปิดประตูนี้ได้ไหม (ไม่ล็อค + ไม่ใช่ประเภทที่เปิดไม่ได้) ----
        bool IsGhostAccessibleDoor(BaseDoor door)
        {
            if (door == null) return false;
            if (door is UnopenableDoor) return false;
            if (door is LockedDoor ld && ld.isLocked) return false;
            return true;
        }

        IEnumerator CheckForDoorsInWay()
        {
            if (isInteractingWithDoor) yield break;

            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out hit, 1.5f))
            {
                BaseDoor door = hit.collider.GetComponent<BaseDoor>();
                if (door == null) door = hit.collider.GetComponentInParent<BaseDoor>();

                if (door != null && !door.open)
                {
                    // ตรวจสอบว่าประตูไม่ล็อค และผีอยู่ใกล้พอ
                    float distToDoor = Vector3.Distance(transform.position, door.transform.position);
                    if (!IsGhostAccessibleDoor(door))
                    {
                        Debug.Log($"🔒 [EnemyAI] ประตู '{door.name}' ล็อคอยู่ ผีเปิดไม่ได้");
                        // หยุด agent ไม่ให้ทะลุผ่าน
                        agent.isStopped = true;
                        yield return new WaitForSeconds(0.5f);
                        agent.isStopped = false;
                        yield break;
                    }
                    if (distToDoor <= doorInteractDistance)
                        yield return StartCoroutine(HandleDoorSequence(door));
                }
            }
        }

        IEnumerator HandleDoorSequence(BaseDoor door)
        {
            // ป้องกันเปิดประตูล็อคหรือประตูที่ผีเปิดไม่ได้
            if (!IsGhostAccessibleDoor(door))
            {
                Debug.Log($"🔒 [EnemyAI] HandleDoorSequence: ประตู '{door.name}' ผีเปิดไม่ได้ ยกเลิก");
                isInteractingWithDoor = false;
                yield break;
            }

            // ตรวจสอบระยะอีกครั้งก่อนเปิด
            float distToDoor = Vector3.Distance(transform.position, door.transform.position);
            if (distToDoor > doorInteractDistance * 1.5f)
            {
                Debug.Log($"⚠️ [EnemyAI] ผีอยู่ไกลประตูเกินไป ({distToDoor:F1}m) ยังไม่เปิด");
                isInteractingWithDoor = false;
                yield break;
            }

            isInteractingWithDoor = true;

            if (!door.open)
            {
                Debug.Log("🚪 [EnemyAI] กำลังเปิดประตู...");
                agent.isStopped = true;
                Vector3 dirToDoor = (door.transform.position - transform.position).normalized;
                transform.rotation = Quaternion.LookRotation(new Vector3(dirToDoor.x, 0, dirToDoor.z));
                yield return new WaitForSeconds(0.3f);
                door.Interact(true);
                yield return new WaitForSeconds(1.0f);
                agent.isStopped = false;
            }

            Debug.Log("🚶 [EnemyAI] กำลังเดินผ่านประตู...");
            Vector3 passPoint = transform.position + transform.forward * 2.5f;
            agent.SetDestination(passPoint);

            float timer = 0f;
            while (Vector3.Distance(transform.position, passPoint) > 0.5f && timer < 2f)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            Debug.Log("🚪 [EnemyAI] หันกลับมาปิดประตู...");
            agent.isStopped = true;
            Vector3 dirBackToDoor = (door.transform.position - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(new Vector3(dirBackToDoor.x, 0, dirBackToDoor.z));
            yield return new WaitForSeconds(0.5f);

            if (door.open) door.Interact(true);
            yield return new WaitForSeconds(1.0f);

            agent.isStopped = false;
            isInteractingWithDoor = false;
        }

        bool CheckLineOfSight()
        {
            if (player == null || playerMovement.IsHiding() || isGameOver || isInteractingWithDoor) return false;

            Vector3 eyePos = transform.position + Vector3.up * 1.6f;
            Vector3 targetPos = player.position + Vector3.up * 0.8f;
            float distToPlayer = Vector3.Distance(eyePos, targetPos);
            Vector3 dirToPlayer = (targetPos - eyePos).normalized;

            if (distToPlayer > viewDistance) return false;

            float angleToPlayer = Vector3.Angle(transform.forward, dirToPlayer);
            if (angleToPlayer > viewAngle / 2f) return false;

            Ray eyeRay = new Ray(eyePos, dirToPlayer);

            // ขั้นที่ 1: เช็คประตูปิดทุกบาน
            foreach (BaseDoor door in allDoors)
            {
                if (door == null || door.open) continue;

                Collider[] cols = door.GetComponentsInChildren<Collider>();
                bool blocked = false;

                foreach (Collider col in cols)
                {
                    if (col.isTrigger) continue;
                    float enter;
                    if (col.bounds.IntersectRay(eyeRay, out enter) && enter >= 0f && enter < distToPlayer)
                    {
                        blocked = true;
                        Debug.DrawRay(eyePos, dirToPlayer * enter, Color.cyan);
                        break;
                    }
                }

                // Fallback: ถ้าไม่มี Collider ใช้ geometry perpDist
                if (!blocked && cols.Length == 0)
                {
                    Vector3 doorPos = door.transform.position + Vector3.up * 1.0f;
                    float doorDist = Vector3.Distance(eyePos, doorPos);
                    if (doorDist < distToPlayer)
                    {
                        float perpDist = Vector3.Cross(dirToPlayer, doorPos - eyePos).magnitude;
                        if (perpDist < 1.5f) blocked = true;
                    }
                }

                if (blocked) return false;
            }

            // ขั้นที่ 2: เช็คกำแพงใน obstacleMask
            RaycastHit wallHit;
            if (Physics.Raycast(eyePos, dirToPlayer, out wallHit, distToPlayer, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (wallHit.collider.GetComponentInParent<EnemyAI>() == null &&
                    wallHit.collider.GetComponentInParent<Movement>() == null)
                {
                    Debug.DrawRay(eyePos, dirToPlayer * wallHit.distance, Color.gray);
                    return false;
                }
            }

            Debug.DrawRay(eyePos, dirToPlayer * distToPlayer, Color.red);
            return true;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0, 0, 1, 0.2f);
            Gizmos.DrawSphere(transform.position, 0.2f);

            Vector3 leftRayDirection = Quaternion.AngleAxis(-viewAngle / 2, Vector3.up) * transform.forward;
            Vector3 rightRayDirection = Quaternion.AngleAxis(viewAngle / 2, Vector3.up) * transform.forward;

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position + Vector3.up, leftRayDirection * viewDistance);
            Gizmos.DrawRay(transform.position + Vector3.up, rightRayDirection * viewDistance);

            Gizmos.color = new Color(1, 0, 1, 0.3f);
            Gizmos.DrawWireSphere(transform.position, noiseDetectionRange);
        }

        void ChasePlayer()
        {
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.stoppingDistance = 0.5f;
            agent.SetDestination(player.position);
        }

        void CatchPlayer()
        {
            if (isGameOver) return;
            StartCoroutine(GameOverSequence());
        }

        // ---- เรียกจาก Movement.OnControllerColliderHit หรือ trigger ----
        public void TriggerCatch()
        {
            if (isGameOver || (playerMovement != null && playerMovement.IsHiding())) return;
            Debug.Log("💀 [EnemyAI] TriggerCatch: ผู้เล่นชนผีโดยตรง!");
            StartCoroutine(GameOverSequence());
        }

        // ---- Trigger Collider บนผี (ต้องมี Collider ที่ติ๊ก Is Trigger ไว้บน Ghost) ----
        void OnTriggerEnter(Collider other)
        {
            if (isGameOver) return;
            if (other.CompareTag("Player") || other.GetComponentInParent<Movement>() != null)
            {
                if (playerMovement != null && playerMovement.IsHiding()) return;
                Debug.Log("💀 [EnemyAI] OnTriggerEnter: ผู้เล่นเข้าใกล้ผี!");
                StartCoroutine(GameOverSequence());
            }
        }

        IEnumerator GameOverSequence()
        {
            isGameOver = true;
            agent.isStopped = true;

            // ล็อค meter ที่ 1 และล็อค UI ไว้
            _detectionMeter = 1f;
            if (DetectionIndicatorUI.Instance != null)
                DetectionIndicatorUI.Instance.SetDetectionLevel(1f);

            if (playerMovement != null) playerMovement.enabled = false;

            Transform cam = playerMovement.playerCamera;
            Vector3 targetPos = transform.position + Vector3.up * 1.5f;

            float duration = 1.0f;
            float elapsed = 0f;
            Quaternion startRot = cam.rotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Vector3 dir = (targetPos - cam.position).normalized;
                Quaternion targetRot = Quaternion.LookRotation(dir);
                cam.rotation = Quaternion.Slerp(startRot, targetRot, elapsed / duration);
                yield return null;
            }

            yield return new WaitForSeconds(1.5f);

            Debug.Log("💀 [EnemyAI] โหลดฉากใหม่...");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
