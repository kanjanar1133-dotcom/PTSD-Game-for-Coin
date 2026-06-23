using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame
{
    public class RandomGhostFootsteps : MonoBehaviour
    {
        [Header("Footstep Sounds")]
        [Tooltip("เสียงเดินของผี (สุ่มเลือกมาเล่น)")]
        [SerializeField] private AudioClip[] footstepClips;

        [Header("Distance Settings")]
        [Tooltip("ระยะห่างน้อยสุดที่เสียงจะเกิด")]
        [SerializeField] private float minDistance = 10f;
        [Tooltip("ระยะห่างมากสุดที่เสียงจะเกิด")]
        [SerializeField] private float maxDistance = 30f;

        [Header("Timing Settings")]
        [Tooltip("เวลารอขั้นต่ำก่อนที่จะมีเสียงเดินชุดต่อไป")]
        [SerializeField] private float minWaitBetweenSequences = 10f;
        [Tooltip("เวลารอสูงสุดก่อนที่จะมีเสียงเดินชุดต่อไป")]
        [SerializeField] private float maxWaitBetweenSequences = 25f;

        [Header("Sequence Settings")]
        [Tooltip("จำนวนก้าวเดินน้อยสุดในแต่ละชุด")]
        [SerializeField] private int minStepsPerSequence = 3;
        [Tooltip("จำนวนก้าวเดินมากสุดในแต่ละชุด")]
        [SerializeField] private int maxStepsPerSequence = 7;
        [Tooltip("เวลาห่างระหว่างแต่ละก้าว (วินาที)")]
        [SerializeField] private float timeBetweenSteps = 0.6f;

        [Header("Audio Settings")]
        [Tooltip("ระดับความดังของเสียง")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.8f;

        private Transform playerTransform;

        private void Start()
        {
            // พยายามหา Player (อาจจะปรับแต่ง tag ตามที่ใช้จริง)
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                // ถ้าหาไม่เจอ ใช้ Camera หลักแทน
                if (Camera.main != null)
                {
                    playerTransform = Camera.main.transform;
                }
            }

            if (playerTransform != null && footstepClips != null && footstepClips.Length > 0)
            {
                StartCoroutine(FootstepRoutine());
            }
            else
            {
                Debug.LogWarning("[RandomGhostFootsteps] ไม่พบ Player หรือไม่มี AudioClip สำหรับเสียงเดินผี!");
            }
        }

        private IEnumerator FootstepRoutine()
        {
            while (true)
            {
                // สุ่มเวลารอชุดต่อไป
                float waitTime = Random.Range(minWaitBetweenSequences, maxWaitBetweenSequences);
                yield return new WaitForSeconds(waitTime);

                // เริ่มเล่นเสียง 1 ชุด
                yield return StartCoroutine(PlayFootstepSequence());
            }
        }

        private IEnumerator PlayFootstepSequence()
        {
            // หาจุดเกิดเสียงแบบสุ่มรอบๆ ผู้เล่น
            Vector3 spawnDirection = Random.onUnitSphere;
            spawnDirection.y = 0; // ไม่เอาความสูง เพื่อให้อยู่ระดับเดียวกัน
            spawnDirection.Normalize();

            float distance = Random.Range(minDistance, maxDistance);
            Vector3 spawnPosition = playerTransform.position + (spawnDirection * distance);

            // สร้าง GameObject สำหรับเป็นแหล่งกำเนิดเสียงชั่วคราว
            GameObject audioObj = new GameObject("GhostFootstepAudio");
            audioObj.transform.position = spawnPosition;
            
            AudioSource audioSource = audioObj.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // เป็น 3D Sound 100%
            audioSource.minDistance = 5f;
            audioSource.maxDistance = maxDistance + 10f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.volume = volume;

            // จำนวนก้าวที่จะเล่นในรอบนี้
            int steps = Random.Range(minStepsPerSequence, maxStepsPerSequence + 1);

            for (int i = 0; i < steps; i++)
            {
                if (footstepClips.Length > 0)
                {
                    // สุ่มคลิปเสียง
                    AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
                    audioSource.PlayOneShot(clip);

                    // อัพเดทตำแหน่งให้เหมือนขยับเข้ามานิดหน่อย หรือขยับไปทางอื่น (Optional)
                    // audioObj.transform.position += someVector;
                }

                // รอจนกว่าจะก้าวต่อไป
                yield return new WaitForSeconds(timeBetweenSteps);
            }

            // รอให้เสียงก้าวสุดท้ายเล่นจบก่อนทำลาย
            yield return new WaitForSeconds(2f);
            Destroy(audioObj);
        }

        private void OnDrawGizmosSelected()
        {
            Transform target = playerTransform;
            if (target == null)
            {
                // ใน Editor ถ้ายังไม่ได้รันเกม ให้ลองวาดรอบๆ ตัวที่ใส่ Script (หรือหา Player)
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                }
                else
                {
                    target = transform;
                }
            }

            // วาดวงกลมสีแดง (ระยะใกล้สุด)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(target.position, minDistance);

            // วาดวงกลมสีเหลือง (ระยะไกลสุด)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(target.position, maxDistance);
        }
    }
}
