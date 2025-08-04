using UnityEngine;
using System.Collections;

namespace JY
{
    /// <summary>
    /// AI의 대기열 관련 행동을 관리하는 클래스
    /// 대기열 진입, 서비스 대기, 서비스 처리 등을 담당
    /// </summary>
    public class AIQueueBehavior : MonoBehaviour
    {
        [Header("대기열 설정")]
        [SerializeField] private float queueRetryDelay = 2f;
        [SerializeField] private float serviceWaitCheckInterval = 0.1f;

        private bool isInQueue = false;
        private bool isWaitingForService = false;
        private Vector3 targetQueuePosition;
        private Coroutine queueCoroutine;

        public bool IsInQueue => isInQueue;
        public bool IsWaitingForService => isWaitingForService;
        public Vector3 TargetQueuePosition => targetQueuePosition;

        // 의존성
        private CounterManager counterManager;
        private AIMovementController movementController;
        private AITimeHandler timeHandler;
        private string aiName;

        /// <summary>
        /// 초기화
        /// </summary>
        public void Initialize(CounterManager counter, AIMovementController movement, AITimeHandler time, string name)
        {
            counterManager = counter;
            movementController = movement;
            timeHandler = time;
            aiName = name;
        }

        /// <summary>
        /// 대기열 행동 시작
        /// </summary>
        public void StartQueueBehavior(AIAgent agent, AIStateMachine.AIState currentState)
        {
            if (queueCoroutine != null)
            {
                StopCoroutine(queueCoroutine);
            }
            queueCoroutine = StartCoroutine(QueueBehaviorCoroutine(agent, currentState));
        }

        /// <summary>
        /// 대기열 행동 중지
        /// </summary>
        public void StopQueueBehavior()
        {
            if (queueCoroutine != null)
            {
                StopCoroutine(queueCoroutine);
                queueCoroutine = null;
            }
            
            if (isInQueue && counterManager != null)
            {
                counterManager.LeaveQueue(GetComponent<AIAgent>());
            }
            
            isInQueue = false;
            isWaitingForService = false;
        }

        /// <summary>
        /// 대기열 행동 코루틴
        /// </summary>
        private IEnumerator QueueBehaviorCoroutine(AIAgent agent, AIStateMachine.AIState initialState)
        {
            AIDebugLogger.Log(aiName, $"QueueBehavior 시작 - 상태: {initialState}", LogCategory.Queue);

            if (counterManager == null)
            {
                AIDebugLogger.LogWarning(aiName, "CounterManager가 없음", LogCategory.Queue);
                yield break;
            }

            // 대기열 진입 시도
            AIDebugLogger.Log(aiName, "대기열 진입 시도", LogCategory.Queue);
            if (!counterManager.TryJoinQueue(agent))
            {
                AIDebugLogger.Log(aiName, "대기열 진입 실패", LogCategory.Queue);
                yield return HandleQueueJoinFailure(agent, initialState);
                yield break;
            }

            // 대기열 진입 성공
            AIDebugLogger.Log(aiName, "대기열 진입 성공", LogCategory.Queue);
            isInQueue = true;

            // 대기열에서 서비스 대기
            yield return WaitForService(agent, initialState);
        }

        /// <summary>
        /// 대기열 진입 실패 처리
        /// </summary>
        private IEnumerator HandleQueueJoinFailure(AIAgent agent, AIStateMachine.AIState currentState)
        {
            // ReportingRoomQueue 상태인 경우 재시도
            if (currentState == AIStateMachine.AIState.ReportingRoomQueue)
            {
                AIDebugLogger.Log(aiName, "ReportingRoomQueue 상태이므로 재시도", LogCategory.Queue);
                yield return new WaitForSeconds(UnityEngine.Random.Range(queueRetryDelay, queueRetryDelay + 3f));
                StartQueueBehavior(agent, currentState);
                yield break;
            }

            // 다른 행동으로 전환
            yield return new WaitForSeconds(UnityEngine.Random.Range(1f, 3f));
            StartQueueBehavior(agent, currentState);
        }

        /// <summary>
        /// 서비스 대기
        /// </summary>
        private IEnumerator WaitForService(AIAgent agent, AIStateMachine.AIState initialState)
        {
            while (isInQueue)
            {
                // 17시 체크 - 대기열에서도 즉시 디스폰
                var (hour, minute) = timeHandler.GetCurrentTime();
                if (hour == 17 && minute == 0)
                {
                    AIDebugLogger.LogTimeEvent(aiName, "대기열 대기 중 17시 감지, 즉시 강제 디스폰");
                    // 강제 디스폰 이벤트 발생 - AIAgent에서 처리
                    yield break;
                }

                // 서비스 위치 도착 확인
                if (movementController.IsAtDestination)
                {
                    if (counterManager.CanReceiveService(agent))
                    {
                        AIDebugLogger.Log(aiName, "서비스 시작", LogCategory.Queue);
                        counterManager.StartService(agent);
                        isWaitingForService = true;

                        // 서비스 처리 대기
                        yield return WaitForServiceCompletion(agent, initialState);
                        break;
                    }
                }

                yield return new WaitForSeconds(serviceWaitCheckInterval);
            }
        }

        /// <summary>
        /// 서비스 완료 대기
        /// </summary>
        private IEnumerator WaitForServiceCompletion(AIAgent agent, AIStateMachine.AIState initialState)
        {
            while (isWaitingForService)
            {
                // 서비스 대기 중에도 17시 체크
                var (hour, minute) = timeHandler.GetCurrentTime();
                if (hour == 17 && minute == 0)
                {
                    AIDebugLogger.LogTimeEvent(aiName, "서비스 대기 중 17시 감지, 즉시 강제 디스폰");
                    yield break;
                }

                yield return new WaitForSeconds(serviceWaitCheckInterval);
            }

            // 서비스 완료 후 처리는 AIAgent에서 담당
            AIDebugLogger.Log(aiName, "서비스 완료", LogCategory.Queue);
        }

        /// <summary>
        /// 서비스 완료 신호 (CounterManager에서 호출)
        /// </summary>
        public void OnServiceCompleted()
        {
            isWaitingForService = false;
            isInQueue = false;
        }

        /// <summary>
        /// 대기열 위치 업데이트 (CounterManager에서 호출)
        /// </summary>
        public void UpdateQueuePosition(Vector3 position)
        {
            targetQueuePosition = position;
            if (movementController != null)
            {
                movementController.SetDestination(position);
            }
        }

        void OnDestroy()
        {
            StopQueueBehavior();
        }
    }
}