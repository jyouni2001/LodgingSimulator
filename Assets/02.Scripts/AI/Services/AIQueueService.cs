using UnityEngine;
using System.Collections;

namespace JY
{
    /// <summary>
    /// AI 대기열 서비스 클래스
    /// 대기열 진입, 서비스 대기, 서비스 완료 로직을 담당
    /// </summary>
    public class AIQueueService : MonoBehaviour
    {
        [Header("대기열 설정")]
        [SerializeField] private float serviceWaitTime = 5f;
        
        // 상태
        private bool isInQueue = false;
        private bool isWaitingForService = false;
        private bool isBeingServed = false;
        private Vector3 targetQueuePosition;
        
        // 의존성
        private CounterManager counterManager;
        private Transform counterPosition;
        private AIMovement aiMovement;
        private AIStateManager stateManager;
        private string agentId;
        
        // 이벤트
        public System.Action OnQueueJoined;
        public System.Action OnQueueLeft;
        public System.Action OnServiceStarted;
        public System.Action OnServiceCompleted;
        
        // 속성
        public bool IsInQueue => isInQueue;
        public bool IsWaitingForService => isWaitingForService;
        public bool IsBeingServed => isBeingServed;
        
        /// <summary>
        /// 초기화
        /// </summary>
        public void Initialize(string id, AIMovement movement, AIStateManager state)
        {
            agentId = id;
            aiMovement = movement;
            stateManager = state;
            
            // CounterManager와 카운터 위치 찾기 (ServiceLocator 사용)
            counterManager = ServiceLocator.CounterManager;
            var counterObj = GameObject.FindGameObjectWithTag("Counter");
            if (counterObj != null)
            {
                counterPosition = counterObj.transform;
            }
            
            if (counterManager == null || counterPosition == null)
            {
                Debug.LogWarning($"AIQueueService {agentId}: CounterManager 또는 Counter를 찾을 수 없습니다.");
            }
        }
        
        /// <summary>
        /// 대기열 진입 시도
        /// </summary>
        public bool TryJoinQueue()
        {
            if (counterManager == null || isInQueue)
            {
                return false;
            }
            
            // CounterManager를 통해 대기열 진입 시도
            var aiAgent = GetComponent<AIAgent>();
            if (aiAgent != null && counterManager.TryJoinQueue(aiAgent))
            {
                isInQueue = true;
                isWaitingForService = true;
                
                stateManager.ChangeState(AIState.WaitingInQueue, "대기열에서 서비스 대기");
                
                OnQueueJoined?.Invoke();
                AIEvents.TriggerQueueJoined(agentId);
                
                Debug.Log($"AI {agentId}: 대기열 진입 성공");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 대기열에서 나가기
        /// </summary>
        public void LeaveQueue()
        {
            if (!isInQueue || counterManager == null) return;
            
            var aiAgent = GetComponent<AIAgent>();
            if (aiAgent != null)
            {
                counterManager.LeaveQueue(aiAgent);
            }
            
            isInQueue = false;
            isWaitingForService = false;
            isBeingServed = false;
            
            OnQueueLeft?.Invoke();
            AIEvents.TriggerQueueLeft(agentId);
            
            Debug.Log($"AI {agentId}: 대기열에서 나감");
        }
        
        /// <summary>
        /// 서비스 시작
        /// </summary>
        public void StartService()
        {
            if (!isInQueue) return;
            
            isWaitingForService = false;
            isBeingServed = true;
            
            stateManager.ChangeState(AIState.WaitingInQueue, "서비스 받는 중");
            
            OnServiceStarted?.Invoke();
            AIEvents.TriggerServiceStarted(agentId);
            
            Debug.Log($"AI {agentId}: 서비스 시작");
            
            // 서비스 완료 대기
            StartCoroutine(WaitForServiceCompletion());
        }
        
        /// <summary>
        /// 서비스 완료 대기 코루틴
        /// </summary>
        private IEnumerator WaitForServiceCompletion()
        {
            yield return new WaitForSeconds(serviceWaitTime);
            CompleteService();
        }
        
        /// <summary>
        /// 서비스 완료
        /// </summary>
        public void CompleteService()
        {
            if (!isBeingServed) return;
            
            isBeingServed = false;
            
            OnServiceCompleted?.Invoke();
            AIEvents.TriggerServiceCompleted(agentId);
            
            Debug.Log($"AI {agentId}: 서비스 완료");
            
            // 대기열에서 나가기
            LeaveQueue();
        }
        
        /// <summary>
        /// 대기열 위치 설정
        /// </summary>
        public void SetQueueDestination(Vector3 position)
        {
            targetQueuePosition = position;
            if (aiMovement != null)
            {
                aiMovement.MoveTo(position);
            }
        }
        
        /// <summary>
        /// 카운터로 이동
        /// </summary>
        public bool MoveToCounter()
        {
            if (counterPosition == null || aiMovement == null)
            {
                return false;
            }
            
            return aiMovement.MoveTo(counterPosition.position);
        }
        
        /// <summary>
        /// 서비스 가능 여부 확인
        /// </summary>
        public bool CanReceiveService()
        {
            if (counterManager == null) return false;
            
            var aiAgent = GetComponent<AIAgent>();
            return aiAgent != null && counterManager.CanReceiveService(aiAgent);
        }
        
        /// <summary>
        /// 대기열 상태 리셋
        /// </summary>
        public void Reset()
        {
            if (isInQueue)
            {
                LeaveQueue();
            }
            
            isInQueue = false;
            isWaitingForService = false;
            isBeingServed = false;
        }
        
        /// <summary>
        /// 현재 대기열 상태 정보 반환
        /// </summary>
        public string GetQueueInfo()
        {
            if (!isInQueue) return "대기열에 없음";
            
            if (isBeingServed) return "서비스 받는 중";
            if (isWaitingForService) return "서비스 대기 중";
            
            return "대기열 상태 불명";
        }
    }
} 