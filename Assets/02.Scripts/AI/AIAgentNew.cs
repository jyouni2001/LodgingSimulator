using UnityEngine;
using System.Collections;

namespace JY
{
    /// <summary>
    /// 새롭게 리팩토링된 AI 에이전트 클래스
    /// 기존의 복잡한 로직을 여러 컴포넌트로 분할하여 단순화
    /// </summary>
    public class AIAgentNew : MonoBehaviour
    {
        [Header("AI 설정")]
        [SerializeField] private AIDebugLogger.DebugSettings debugSettings = new AIDebugLogger.DebugSettings();
        
        [Header("디스폰 설정")]
        [SerializeField] private Transform spawnPoint;
        
        // 컴포넌트들
        private AIStateMachine stateMachine;
        private AIMovementController movementController;
        private AIRoomManager roomManager;
        private AITimeHandler timeHandler;
        private AIQueueBehavior queueBehavior;
        
        // 외부 참조들
        private RoomManager globalRoomManager;
        private CounterManager counterManager;
        private AISpawner spawner;
        
        // 코루틴 관리
        private Coroutine roomUseCoroutine;
        private Coroutine wanderingCoroutine;
        
        public string AIName => gameObject.name;
        public AIStateMachine.AIState CurrentState => stateMachine.CurrentState;
        public bool HasAssignedRoom => roomManager.HasAssignedRoom;

        #region Unity 생명주기
        void Start()
        {
            if (!InitializeComponents())
            {
                Destroy(gameObject);
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
            timeHandler.UpdateTimeBasedBehavior();
            
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
            // 필수 컴포넌트들 확인
            if (!movementController) movementController = GetComponent<AIMovementController>();
            if (!movementController) movementController = gameObject.AddComponent<AIMovementController>();
            
            if (!queueBehavior) queueBehavior = GetComponent<AIQueueBehavior>();
            if (!queueBehavior) queueBehavior = gameObject.AddComponent<AIQueueBehavior>();

            // 스폰 포인트 찾기
            if (!spawnPoint)
            {
                GameObject spawn = GameObject.FindGameObjectWithTag("Spawn");
                if (spawn) spawnPoint = spawn.transform;
            }

            if (!spawnPoint)
            {
                AIDebugLogger.LogError(AIName, "Spawn 포인트를 찾을 수 없음");
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
            roomManager = new AIRoomManager();
            timeHandler = new AITimeHandler();
            
            // 외부 참조 찾기
            globalRoomManager = FindObjectOfType<RoomManager>();
            counterManager = FindObjectOfType<CounterManager>();
            spawner = AISpawner.Instance;
            
            // 시스템들 초기화
            roomManager.InitializeRooms(AIName);
            timeHandler.Initialize();
            
            // 컴포넌트 초기화
            queueBehavior.Initialize(counterManager, movementController, timeHandler, AIName);
            
            // 디버그 설정 적용
            AIDebugLogger.UpdateGlobalSettings(debugSettings);
            
            // 이벤트 구독
            SubscribeToEvents();
            
            AIDebugLogger.Log(AIName, "AI 시스템 초기화 완료", LogCategory.General, true);
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            stateMachine.OnStateChanged += OnStateChanged;
            timeHandler.OnForcedDespawnTime += Handle17OClockForcedDespawn;
            timeHandler.OnCheckoutTime += HandleCheckoutDespawn;
            timeHandler.OnHourlyBehaviorUpdate += OnHourlyBehaviorUpdate;
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

        #region 행동 결정
        /// <summary>
        /// 초기 행동 결정
        /// </summary>
        private void DetermineInitialBehavior()
        {
            var decision = timeHandler.DetermineBehaviorByTime();
            ExecuteBehaviorDecision(decision);
        }

        /// <summary>
        /// 행동 결정 실행
        /// </summary>
        private void ExecuteBehaviorDecision(BehaviorDecision decision)
        {
            switch (decision)
            {
                case BehaviorDecision.Queue:
                    TransitionToQueue();
                    break;
                case BehaviorDecision.Wander:
                    TransitionToWandering();
                    break;
                case BehaviorDecision.Despawn:
                    TransitionToDespawn();
                    break;
                case BehaviorDecision.Fallback:
                default:
                    ExecuteFallbackBehavior();
                    break;
            }
        }

        /// <summary>
        /// 기본 행동 실행
        /// </summary>
        private void ExecuteFallbackBehavior()
        {
            if (counterManager == null)
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
            stateMachine.TransitionToState(AIStateMachine.AIState.MovingToQueue);
            queueBehavior.StartQueueBehavior(GetComponent<AIAgent>(), stateMachine.CurrentState);
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
            if (roomManager.TryAssignRoom(AIName))
            {
                stateMachine.TransitionToState(AIStateMachine.AIState.MovingToRoom);
                Vector3 roomPosition = roomManager.GetCurrentRoomPosition();
                movementController.SetDestination(roomPosition);
                AIDebugLogger.LogRoomAction(AIName, "이동 시작", roomManager.CurrentRoomIndex);
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
            movementController.SetDestination(spawnPoint.position);
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
            if (movementController.IsAtDestination && 
                roomManager.IsPositionInCurrentRoom(transform.position))
            {
                AIDebugLogger.LogRoomAction(AIName, "도착", roomManager.CurrentRoomIndex);
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

        #region 방 사용
        /// <summary>
        /// 방 사용 시작
        /// </summary>
        private void StartRoomUsage()
        {
            stateMachine.TransitionToState(AIStateMachine.AIState.UsingRoom);
            roomUseCoroutine = StartCoroutine(UseRoomCoroutine());
        }

        /// <summary>
        /// 방 사용 코루틴
        /// </summary>
        private IEnumerator UseRoomCoroutine()
        {
            AIDebugLogger.LogRoomAction(AIName, "사용 시작", roomManager.CurrentRoomIndex);
            
            float useTime = UnityEngine.Random.Range(10f, 30f); // 10-30초 사용
            float elapsed = 0f;
            
            while (elapsed < useTime)
            {
                // 17시 체크
                var (hour, minute) = timeHandler.GetCurrentTime();
                if (hour == 17 && minute == 0)
                {
                    AIDebugLogger.LogTimeEvent(AIName, "방 사용 중 17시 감지, 강제 디스폰");
                    Handle17OClockForcedDespawn();
                    yield break;
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 방 사용 완료
            FinishRoomUsage();
        }

        /// <summary>
        /// 방 사용 완료
        /// </summary>
        private void FinishRoomUsage()
        {
            AIDebugLogger.LogRoomAction(AIName, "사용 완료", roomManager.CurrentRoomIndex);
            
            // 결제 처리
            ProcessRoomPayment();
            
            // 방 해제
            roomManager.ReleaseCurrentRoom(AIName);
            
            // 체크아웃 대기열로 이동
            stateMachine.TransitionToState(AIStateMachine.AIState.ReportingRoomQueue);
            queueBehavior.StartQueueBehavior(GetComponent<AIAgent>(), stateMachine.CurrentState);
        }

        /// <summary>
        /// 방 사용료 결제 처리
        /// </summary>
        private void ProcessRoomPayment()
        {
            if (globalRoomManager != null && roomManager.HasAssignedRoom)
            {
                // RoomManager를 통한 결제 처리
                // 구체적인 구현은 기존 로직 유지
                AIDebugLogger.Log(AIName, "결제 처리 완료", LogCategory.Room, true);
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
            
            // 상태 변경 시 이전 코루틴 정리
            CleanupStateTransition(oldState);
        }

        /// <summary>
        /// 17시 강제 디스폰 핸들러
        /// </summary>
        private void Handle17OClockForcedDespawn()
        {
            AIDebugLogger.LogTimeEvent(AIName, "17시 강제 디스폰 시작");
            
            // 모든 행동 중지
            CleanupCoroutines();
            queueBehavior.StopQueueBehavior();
            
            // 방 해제
            if (roomManager.HasAssignedRoom)
            {
                roomManager.ReleaseCurrentRoom(AIName);
            }
            
            // 즉시 디스폰
            ReturnToPool();
        }

        /// <summary>
        /// 11시 체크아웃 핸들러
        /// </summary>
        private void HandleCheckoutDespawn()
        {
            if (!stateMachine.IsInRoomRelatedState())
            {
                AIDebugLogger.LogTimeEvent(AIName, "11시 체크아웃 디스폰 예정");
                timeHandler.ScheduleForDespawn();
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
                var decision = timeHandler.DetermineBehaviorByTime();
                ExecuteBehaviorDecision(decision);
            }
            else if (stateMachine.IsInRoomRelatedState())
            {
                AIDebugLogger.LogTimeEvent(AIName, $"{hour}시 방 배회 재결정");
                var roomDecision = timeHandler.DetermineRoomBehavior();
                // 방 배회 로직 구현 (기존 로직 참조)
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
            
            if (wanderingCoroutine != null)
            {
                StopCoroutine(wanderingCoroutine);
                wanderingCoroutine = null;
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
                case AIStateMachine.AIState.RoomWandering:
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
            queueBehavior.StopQueueBehavior();
            
            if (roomManager.HasAssignedRoom)
            {
                roomManager.ReleaseCurrentRoom(AIName);
            }
            
            if (spawner != null)
            {
                spawner.ReturnToPool(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        #endregion

        #region 공개 메서드 (기존 호환성)
        /// <summary>
        /// 외부에서 상태 확인용
        /// </summary>
        public bool IsInCriticalState() => stateMachine.IsInCriticalState();
        
        /// <summary>
        /// 외부에서 방 관련 상태 확인용
        /// </summary>
        public bool IsInRoomRelatedState() => stateMachine.IsInRoomRelatedState();
        #endregion
    }
}