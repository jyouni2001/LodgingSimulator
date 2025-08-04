using UnityEngine;
using System.Collections;
using JY.AI.Interfaces;
using JY.AI.Services;

namespace JY
{
    /// <summary>
    /// 의존성이 분리된 최종 리팩토링된 AI 에이전트
    /// 인터페이스를 통해 외부 시스템과 느슨하게 결합
    /// </summary>
    public class AIAgentRefactored : MonoBehaviour
    {
        [Header("AI 설정")]
        [SerializeField] private AIDebugLogger.DebugSettings debugSettings = new AIDebugLogger.DebugSettings();
        
        // 인터페이스를 통한 의존성 (느슨한 결합)
        private ITimeProvider timeProvider;
        private IQueueManager queueManager;
        private IRoomProvider roomProvider;
        private IPaymentProcessor paymentProcessor;
        private IObjectPoolManager poolManager;
        
        // 컴포넌트들
        private AIStateMachine stateMachine;
        private AIMovementController movementController;
        private AITimeHandler timeHandler;
        private AIQueueBehavior queueBehavior;
        
        // 방 정보
        private RoomAssignment currentRoom;
        
        // 코루틴 관리
        private Coroutine roomUseCoroutine;
        
        public string AIName => gameObject.name;
        public AIStateMachine.AIState CurrentState => stateMachine?.CurrentState ?? AIStateMachine.AIState.Wandering;
        public bool HasAssignedRoom => currentRoom != null;

        #region Unity 생명주기
        void Start()
        {
            if (!InitializeComponents())
            {
                AIDebugLogger.LogError(AIName, "컴포넌트 초기화 실패");
                ReturnToPool();
                return;
            }
            
            if (!InitializeServices())
            {
                AIDebugLogger.LogError(AIName, "서비스 초기화 실패");
                ReturnToPool();
                return;
            }
            
            InitializeSystems();
            DetermineInitialBehavior();
        }

        void Update()
        {
            if (!movementController.IsOnNavMesh)
            {
                AIDebugLogger.LogWarning(AIName, "NavMesh를 벗어남, 디스폰");
                ReturnToPool();
                return;
            }

            // 시간 기반 업데이트
            UpdateTimeBasedBehavior();
            
            // 상태별 업데이트
            UpdateCurrentState();
        }

        void OnDestroy()
        {
            CleanupCoroutines();
            UnsubscribeFromEvents();
        }
        #endregion

        #region 초기화
        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private bool InitializeComponents()
        {
            // 필수 컴포넌트들 확인/추가
            if (!movementController) movementController = GetComponent<AIMovementController>();
            if (!movementController) movementController = gameObject.AddComponent<AIMovementController>();
            
            if (!queueBehavior) queueBehavior = GetComponent<AIQueueBehavior>();
            if (!queueBehavior) queueBehavior = gameObject.AddComponent<AIQueueBehavior>();

            return movementController != null && queueBehavior != null;
        }

        /// <summary>
        /// 서비스 초기화 (의존성 주입)
        /// </summary>
        private bool InitializeServices()
        {
            // ServiceLocator를 통해 의존성 주입
            timeProvider = AIServiceLocator.GetTimeProvider();
            queueManager = AIServiceLocator.GetQueueManager();
            roomProvider = AIServiceLocator.GetRoomProvider();
            paymentProcessor = AIServiceLocator.GetPaymentProcessor();
            poolManager = AIServiceLocator.GetPoolManager();

            // 필수 서비스 확인
            if (timeProvider == null)
            {
                AIDebugLogger.LogWarning(AIName, "TimeProvider를 찾을 수 없음");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 시스템 초기화
        /// </summary>
        private void InitializeSystems()
        {
            // 각 시스템 초기화
            stateMachine = new AIStateMachine();
            timeHandler = new AITimeHandler();
            
            // 의존성 주입으로 시스템들 초기화
            timeHandler.Initialize(timeProvider);
            
            // 디버그 설정 적용
            AIDebugLogger.UpdateGlobalSettings(debugSettings);
            
            // 이벤트 구독
            SubscribeToEvents();
            
            AIDebugLogger.Log(AIName, "리팩토링된 AI 시스템 초기화 완료", LogCategory.General, true);
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            if (stateMachine != null)
                stateMachine.OnStateChanged += OnStateChanged;
                
            if (timeHandler != null)
            {
                timeHandler.OnForcedDespawnTime += Handle17OClockForcedDespawn;
                timeHandler.OnCheckoutTime += HandleCheckoutDespawn;
                timeHandler.OnHourlyBehaviorUpdate += OnHourlyBehaviorUpdate;
            }
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (stateMachine != null)
                stateMachine.OnStateChanged -= OnStateChanged;
                
            if (timeHandler != null)
            {
                timeHandler.OnForcedDespawnTime -= Handle17OClockForcedDespawn;
                timeHandler.OnCheckoutTime -= HandleCheckoutDespawn;
                timeHandler.OnHourlyBehaviorUpdate -= OnHourlyBehaviorUpdate;
            }
        }
        #endregion

        #region 시간 기반 행동
        /// <summary>
        /// 시간 기반 업데이트
        /// </summary>
        private void UpdateTimeBasedBehavior()
        {
            if (timeProvider == null) return;

            // 강제 디스폰 시간 체크
            if (timeProvider.IsForcedDespawnTime())
            {
                Handle17OClockForcedDespawn();
                return;
            }

            // 체크아웃 시간 체크
            if (timeProvider.IsCheckoutTime())
            {
                HandleCheckoutDespawn();
                return;
            }

            // 매시간 행동 업데이트 (timeHandler에서 처리)
            timeHandler?.UpdateTimeBasedBehavior();
        }

        /// <summary>
        /// 초기 행동 결정
        /// </summary>
        private void DetermineInitialBehavior()
        {
            if (timeProvider == null)
            {
                ExecuteFallbackBehavior();
                return;
            }

            // 시간 기반 행동 결정
            if (timeProvider.IsBusinessHours())
            {
                // 영업시간 중 확률적 행동 결정
                float randomValue = UnityEngine.Random.value;
                int hour = timeProvider.CurrentHour;
                
                float queueProbability = hour switch
                {
                    11 => 0.7f,
                    12 => 0.8f,
                    13 => 0.6f,
                    14 => 0.5f,
                    15 => 0.4f,
                    16 => 0.3f,
                    _ => 0.5f
                };

                if (randomValue < queueProbability)
                {
                    TransitionToQueue();
                }
                else
                {
                    TransitionToWandering();
                }
            }
            else
            {
                TransitionToDespawn();
            }
        }

        /// <summary>
        /// 기본 행동 실행
        /// </summary>
        private void ExecuteFallbackBehavior()
        {
            if (queueManager == null)
            {
                float randomValue = UnityEngine.Random.value;
                if (randomValue < 0.5f)
                {
                    TransitionToWandering();
                }
                else
                {
                    TransitionToDespawn();
                }
            }
            else
            {
                TransitionToQueue();
            }
        }
        #endregion

        #region 상태 전환
        /// <summary>
        /// 대기열로 전환
        /// </summary>
        private void TransitionToQueue()
        {
            if (queueManager == null)
            {
                AIDebugLogger.LogWarning(AIName, "QueueManager가 없어서 대기열 이동 불가");
                TransitionToWandering();
                return;
            }

            stateMachine.TransitionToState(AIStateMachine.AIState.MovingToQueue);
            StartCoroutine(QueueBehaviorCoroutine());
        }

        /// <summary>
        /// 배회로 전환
        /// </summary>
        private void TransitionToWandering()
        {
            stateMachine.TransitionToState(AIStateMachine.AIState.Wandering);
            movementController.StartWandering();
        }

        /// <summary>
        /// 방으로 이동
        /// </summary>
        private void TransitionToRoom()
        {
            if (roomProvider == null)
            {
                AIDebugLogger.LogWarning(AIName, "RoomProvider가 없어서 방 이동 불가");
                TransitionToWandering();
                return;
            }

            if (roomProvider.TryAssignRoom(AIName, out RoomAssignment assignment))
            {
                currentRoom = assignment;
                stateMachine.TransitionToState(AIStateMachine.AIState.MovingToRoom);
                movementController.SetDestination(assignment.RoomPosition);
                AIDebugLogger.LogRoomAction(AIName, "방 이동 시작", -1);
            }
            else
            {
                AIDebugLogger.LogWarning(AIName, "사용 가능한 방이 없음");
                TransitionToWandering();
            }
        }

        /// <summary>
        /// 디스폰으로 전환
        /// </summary>
        private void TransitionToDespawn()
        {
            stateMachine.TransitionToState(AIStateMachine.AIState.ReturningToSpawn);
            
            Vector3 spawnPos = poolManager?.GetSpawnPosition() ?? Vector3.zero;
            movementController.SetDestination(spawnPos);
        }
        #endregion

        #region 대기열 처리
        /// <summary>
        /// 대기열 행동 코루틴
        /// </summary>
        private IEnumerator QueueBehaviorCoroutine()
        {
            AIDebugLogger.Log(AIName, "대기열 진입 시도", LogCategory.Queue);

            // 대기열 진입 시도
            if (!queueManager.TryJoinQueue(this))
            {
                AIDebugLogger.Log(AIName, "대기열 진입 실패", LogCategory.Queue);
                yield return new WaitForSeconds(UnityEngine.Random.Range(2f, 5f));
                TransitionToWandering();
                yield break;
            }

            // 대기열에서 서비스 대기
            stateMachine.TransitionToState(AIStateMachine.AIState.WaitingInQueue);
            Vector3 queuePos = queueManager.GetQueuePosition(this);
            movementController.SetDestination(queuePos);

            // 서비스 대기
            while (true)
            {
                // 17시 체크
                if (timeProvider?.IsForcedDespawnTime() == true)
                {
                    Handle17OClockForcedDespawn();
                    yield break;
                }

                // 서비스 가능 여부 확인
                if (movementController.IsAtDestination && queueManager.CanReceiveService(this))
                {
                    queueManager.StartService(this);
                    yield return ProcessService();
                    break;
                }

                yield return new WaitForSeconds(0.1f);
            }
        }

        /// <summary>
        /// 서비스 처리
        /// </summary>
        private IEnumerator ProcessService()
        {
            AIDebugLogger.Log(AIName, "서비스 처리 시작", LogCategory.Queue);

            // 방 할당 시도
            if (currentRoom == null && roomProvider.HasAvailableRooms())
            {
                TransitionToRoom();
            }
            else if (currentRoom != null)
            {
                // 체크아웃 처리
                ProcessCheckout();
                TransitionToDespawn();
            }
            else
            {
                // 방이 없으면 배회
                TransitionToWandering();
            }

            yield return null;
        }
        #endregion

        #region 방 사용
        /// <summary>
        /// 방 사용 시작
        /// </summary>
        private void StartRoomUsage()
        {
            if (currentRoom == null) return;

            stateMachine.TransitionToState(AIStateMachine.AIState.UsingRoom);
            roomUseCoroutine = StartCoroutine(UseRoomCoroutine());
        }

        /// <summary>
        /// 방 사용 코루틴
        /// </summary>
        private IEnumerator UseRoomCoroutine()
        {
            AIDebugLogger.LogRoomAction(AIName, "방 사용 시작", -1);
            
            float useTime = UnityEngine.Random.Range(10f, 30f);
            float elapsed = 0f;
            
            while (elapsed < useTime)
            {
                // 17시 체크
                if (timeProvider?.IsForcedDespawnTime() == true)
                {
                    Handle17OClockForcedDespawn();
                    yield break;
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            FinishRoomUsage();
        }

        /// <summary>
        /// 방 사용 완료
        /// </summary>
        private void FinishRoomUsage()
        {
            if (currentRoom == null) return;

            AIDebugLogger.LogRoomAction(AIName, "방 사용 완료", -1);
            
            // 결제 정보 추가
            paymentProcessor?.AddPayment(AIName, currentRoom.Price, currentRoom.RoomId, currentRoom.Reputation);
            
            // 체크아웃 대기열로 이동
            stateMachine.TransitionToState(AIStateMachine.AIState.ReportingRoomQueue);
            StartCoroutine(QueueBehaviorCoroutine());
        }

        /// <summary>
        /// 체크아웃 처리
        /// </summary>
        private void ProcessCheckout()
        {
            if (currentRoom == null) return;

            // 결제 처리
            int paidAmount = paymentProcessor?.ProcessPayment(AIName) ?? 0;
            
            // 방 해제
            roomProvider?.ReleaseRoom(AIName, currentRoom.RoomId);
            currentRoom = null;
            
            AIDebugLogger.Log(AIName, $"체크아웃 완료 - 결제: {paidAmount}원", LogCategory.Room, true);
        }
        #endregion

        #region 상태별 업데이트
        /// <summary>
        /// 현재 상태별 업데이트
        /// </summary>
        private void UpdateCurrentState()
        {
            switch (stateMachine.CurrentState)
            {
                case AIStateMachine.AIState.MovingToRoom:
                    UpdateMovingToRoom();
                    break;
                case AIStateMachine.AIState.ReturningToSpawn:
                    UpdateReturningToSpawn();
                    break;
            }
        }

        /// <summary>
        /// 방으로 이동 중 업데이트
        /// </summary>
        private void UpdateMovingToRoom()
        {
            if (movementController.IsAtDestination && currentRoom != null &&
                roomProvider.IsPositionInRoom(transform.position, currentRoom.RoomId))
            {
                AIDebugLogger.LogRoomAction(AIName, "방 도착", -1);
                StartRoomUsage();
            }
        }

        /// <summary>
        /// 스폰으로 복귀 중 업데이트
        /// </summary>
        private void UpdateReturningToSpawn()
        {
            if (movementController.IsAtDestination)
            {
                AIDebugLogger.Log(AIName, "스폰 지점 도착, 디스폰", LogCategory.General, true);
                ReturnToPool();
            }
        }
        #endregion

        #region 이벤트 핸들러
        /// <summary>
        /// 상태 변경 이벤트 핸들러
        /// </summary>
        private void OnStateChanged(AIStateMachine.AIState oldState, AIStateMachine.AIState newState)
        {
            AIDebugLogger.LogStateChange(AIName, oldState.ToString(), newState.ToString());
            CleanupStateTransition(oldState);
        }

        /// <summary>
        /// 17시 강제 디스폰 핸들러
        /// </summary>
        private void Handle17OClockForcedDespawn()
        {
            AIDebugLogger.LogTimeEvent(AIName, "17시 강제 디스폰");
            ReturnToPool();
        }

        /// <summary>
        /// 11시 체크아웃 핸들러
        /// </summary>
        private void HandleCheckoutDespawn()
        {
            if (!stateMachine.IsInRoomRelatedState())
            {
                AIDebugLogger.LogTimeEvent(AIName, "11시 체크아웃 디스폰");
                TransitionToDespawn();
            }
        }

        /// <summary>
        /// 시간별 행동 업데이트 핸들러
        /// </summary>
        private void OnHourlyBehaviorUpdate(int hour)
        {
            if (!stateMachine.IsInCriticalState())
            {
                AIDebugLogger.LogTimeEvent(AIName, $"{hour}시 행동 재결정");
                DetermineInitialBehavior();
            }
        }
        #endregion

        #region 유틸리티
        /// <summary>
        /// 코루틴 정리
        /// </summary>
        private void CleanupCoroutines()
        {
            if (roomUseCoroutine != null)
            {
                StopCoroutine(roomUseCoroutine);
                roomUseCoroutine = null;
            }
        }

        /// <summary>
        /// 상태 전환 시 정리
        /// </summary>
        private void CleanupStateTransition(AIStateMachine.AIState oldState)
        {
            switch (oldState)
            {
                case AIStateMachine.AIState.Wandering:
                    movementController.StopWandering();
                    break;
                case AIStateMachine.AIState.UsingRoom:
                    if (roomUseCoroutine != null)
                    {
                        StopCoroutine(roomUseCoroutine);
                        roomUseCoroutine = null;
                    }
                    break;
            }
        }

        /// <summary>
        /// 오브젝트 풀로 반환
        /// </summary>
        private void ReturnToPool()
        {
            CleanupCoroutines();
            
            // 방 해제
            if (currentRoom != null)
            {
                roomProvider?.ReleaseRoom(AIName, currentRoom.RoomId);
                currentRoom = null;
            }
            
            // 대기열에서 제거
            queueManager?.LeaveQueue(this);
            
            // 풀로 반환
            if (poolManager != null)
            {
                poolManager.ReturnToPool(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        #endregion

        #region 공개 메서드 (호환성)
        /// <summary>
        /// 외부에서 상태 확인용
        /// </summary>
        public bool IsInCriticalState() => stateMachine?.IsInCriticalState() ?? false;
        
        /// <summary>
        /// 외부에서 방 관련 상태 확인용
        /// </summary>
        public bool IsInRoomRelatedState() => stateMachine?.IsInRoomRelatedState() ?? false;
        #endregion
    }
}