using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace JY
{
    /// <summary>
    /// AI 이동 제어를 담당하는 클래스
    /// NavMeshAgent를 사용한 이동, 배회, 경로 찾기 등을 처리
    /// </summary>
    public class AIMovementController : MonoBehaviour
    {
        [Header("이동 설정")]
        [SerializeField] private float arrivalDistance = 0.5f;
        [SerializeField] private float wanderRadius = 10f;
        [SerializeField] private float wanderTimer = 5f;

        private NavMeshAgent agent;
        private Coroutine currentMovementCoroutine;
        private float timer;

        public NavMeshAgent Agent => agent;
        public bool IsAtDestination => !agent.pathPending && agent.remainingDistance < arrivalDistance;
        public bool IsOnNavMesh => agent.isOnNavMesh;

        void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                Debug.LogError($"AIMovementController: NavMeshAgent가 없습니다 - {gameObject.name}");
            }
        }

        /// <summary>
        /// 목적지 설정
        /// </summary>
        public bool SetDestination(Vector3 destination)
        {
            if (agent == null || !agent.isOnNavMesh) return false;

            return agent.SetDestination(destination);
        }

        /// <summary>
        /// 배회 시작
        /// </summary>
        public void StartWandering()
        {
            if (currentMovementCoroutine != null)
            {
                StopCoroutine(currentMovementCoroutine);
            }
            currentMovementCoroutine = StartCoroutine(WanderingBehavior());
        }

        /// <summary>
        /// 배회 중지
        /// </summary>
        public void StopWandering()
        {
            if (currentMovementCoroutine != null)
            {
                StopCoroutine(currentMovementCoroutine);
                currentMovementCoroutine = null;
            }
        }

        /// <summary>
        /// 이동 중지
        /// </summary>
        public void StopMovement()
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
            StopWandering();
        }

        /// <summary>
        /// 배회 행동 코루틴
        /// </summary>
        private IEnumerator WanderingBehavior()
        {
            while (true)
            {
                timer += Time.deltaTime;

                if (timer >= wanderTimer)
                {
                    Vector3 newPos = RandomNavSphere(transform.position, wanderRadius, -1);
                    if (newPos != Vector3.zero)
                    {
                        agent.SetDestination(newPos);
                    }
                    timer = 0;
                }

                yield return new WaitForSeconds(0.1f);
            }
        }

        /// <summary>
        /// 랜덤한 NavMesh 위치 찾기
        /// </summary>
        private Vector3 RandomNavSphere(Vector3 origin, float distance, int layermask)
        {
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * distance;
            randomDirection += origin;

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit navHit, distance, layermask))
            {
                return navHit.position;
            }

            return Vector3.zero;
        }

        /// <summary>
        /// 특정 범위 내에서 랜덤 위치 찾기
        /// </summary>
        public Vector3 GetRandomPositionInBounds(Bounds bounds, int maxRetries = 10)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                Vector3 randomPoint = new Vector3(
                    UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                    bounds.center.y,
                    UnityEngine.Random.Range(bounds.min.z, bounds.max.z)
                );

                if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }

            return bounds.center;
        }

        void OnDestroy()
        {
            StopWandering();
        }
    }
}