using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace JY
{
    /// <summary>
    /// AI 이동 제어 클래스
    /// NavMeshAgent를 사용한 이동과 위치 관리를 담당
    /// </summary>
    public class AIMovement : MonoBehaviour
    {
        [Header("이동 설정")]
        [SerializeField] private float arrivalDistance = 0.5f;
        [SerializeField] private int maxRetries = 3;
        
        // 컴포넌트
        private NavMeshAgent agent;
        private Transform spawnPoint;
        
        // 이동 상태
        private Vector3 currentDestination;
        private bool isMoving = false;
        
        // 속성
        public bool IsMoving => isMoving && agent.pathPending || agent.remainingDistance > arrivalDistance;
        public Vector3 CurrentDestination => currentDestination;
        public float RemainingDistance => agent.remainingDistance;
        
        // 이벤트
        public System.Action OnDestinationReached;
        public System.Action OnMovementFailed;
        
        /// <summary>
        /// 초기화
        /// </summary>
        public void Initialize(Transform spawn)
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                Debug.LogError($"AIMovement: NavMeshAgent를 찾을 수 없습니다. {gameObject.name}");
                return;
            }
            
            spawnPoint = spawn;
            isMoving = false;
        }
        
        /// <summary>
        /// 목적지로 이동
        /// </summary>
        public bool MoveTo(Vector3 destination)
        {
            if (agent == null)
            {
                Debug.LogError("AIMovement: NavMeshAgent가 초기화되지 않았습니다.");
                return false;
            }
            
            // NavMesh 위의 가장 가까운 점 찾기
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                destination = hit.position;
            }
            else
            {
                Debug.LogWarning($"AIMovement: NavMesh에서 유효한 위치를 찾을 수 없습니다. {destination}");
                return false;
            }
            
            currentDestination = destination;
            isMoving = true;
            
            agent.SetDestination(destination);
            
            // 도착 확인 코루틴 시작
            StartCoroutine(CheckArrival());
            
            return true;
        }
        
        /// <summary>
        /// 이동 중지
        /// </summary>
        public void StopMovement()
        {
            if (agent != null)
            {
                agent.ResetPath();
                isMoving = false;
            }
        }
        
        /// <summary>
        /// 스폰 지점으로 복귀
        /// </summary>
        public bool ReturnToSpawn()
        {
            if (spawnPoint == null)
            {
                Debug.LogWarning("AIMovement: 스폰 지점이 설정되지 않았습니다.");
                return false;
            }
            
            return MoveTo(spawnPoint.position);
        }
        
        /// <summary>
        /// 랜덤 위치로 이동 (배회)
        /// </summary>
        public bool WanderAround(Vector3 center, float radius)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                Vector3 randomDirection = Random.insideUnitSphere * radius;
                randomDirection += center;
                randomDirection.y = center.y; // Y축 고정
                
                if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, radius, NavMesh.AllAreas))
                {
                    return MoveTo(hit.position);
                }
            }
            
            Debug.LogWarning($"AIMovement: {maxRetries}번 시도 후 배회 위치를 찾지 못했습니다.");
            return false;
        }
        
        /// <summary>
        /// 도착 확인 코루틴
        /// </summary>
        private IEnumerator CheckArrival()
        {
            while (isMoving)
            {
                if (!agent.pathPending && agent.remainingDistance < arrivalDistance)
                {
                    isMoving = false;
                    OnDestinationReached?.Invoke();
                    yield break;
                }
                
                // 경로 계산 실패 확인
                if (!agent.hasPath && !agent.pathPending)
                {
                    isMoving = false;
                    OnMovementFailed?.Invoke();
                    Debug.LogWarning($"AIMovement: 경로를 찾을 수 없습니다. 목적지: {currentDestination}");
                    yield break;
                }
                
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        /// <summary>
        /// 현재 위치가 목적지 근처인지 확인
        /// </summary>
        public bool IsNearDestination(Vector3 destination, float threshold = -1f)
        {
            if (threshold < 0f) threshold = arrivalDistance;
            return Vector3.Distance(transform.position, destination) <= threshold;
        }
        
        /// <summary>
        /// 에이전트 활성화/비활성화
        /// </summary>
        public void SetAgentEnabled(bool enabled)
        {
            if (agent != null)
            {
                agent.enabled = enabled;
            }
        }
        
        /// <summary>
        /// 현재 이동 정보 반환
        /// </summary>
        public string GetMovementInfo()
        {
            if (agent == null) return "Agent 없음";
            
            return $"이동중: {isMoving}, 목적지: {currentDestination}, 남은거리: {agent.remainingDistance:F1}";
        }
    }
} 