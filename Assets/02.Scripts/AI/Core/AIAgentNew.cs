using UnityEngine;
using UnityEngine.AI;

namespace JY
{
    /// <summary>
    /// 새로운 간소화된 AI 에이전트 클래스
    /// 컴포지션 패턴을 사용하여 각 전문 서비스들을 조합
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(AIStateManager))]
    [RequireComponent(typeof(AIMovement))]
    [RequireComponent(typeof(AIRoomService))]
    [RequireComponent(typeof(AIQueueService))]
    [RequireComponent(typeof(AITimeService))]
    [RequireComponent(typeof(AIBehaviorController))]
    public class AIAgentNew : MonoBehaviour
    {
        [Header("AI 설정")]
        [SerializeField] private AIConfiguration configuration = new AIConfiguration();
        
        // 컴포넌트 참조
        private AIStateManager stateManager;
        private AIMovement movement;
        private AIRoomService roomService;
        private AIQueueService queueService;
        private AITimeService timeService;
        private AIBehaviorController behaviorController;
        
        // AI 정보
        private string agentId;
        private Transform spawnPoint;
        private AISpawner spawner;
        
        // 속성
        public string AgentId => agentId;
        public AIState CurrentState => stateManager.CurrentState;
        public string CurrentDestination => stateManager.CurrentDestination;
        public bool IsMoving => movement.IsMoving;
        public AIConfiguration Configuration => configuration;
        
        // 이벤트 (외부 시스템과의 호환성을 위해)
        public System.Action<AIAgentNew> OnServiceComplete;
        
        #region Unity 생명주기
        
        private void Awake()
        {
            // 고유 ID 생성
            agentId = System.Guid.NewGuid().ToString();
            
            // 컴포넌트 초기화
            InitializeComponents();
        }
        
        private void Start()
        {
            // 시스템 초기화
            InitializeSystems();
            
            // 행동 시작
            behaviorController.StartBehavior();
            
            AIEvents.TriggerAgentSpawned(agentId);
        }
        
        private void OnEnable()
        {
            // AI 재활성화
            InitializeAI();
        }
        
        private void OnDestroy()
        {
            // 정리 작업
            Cleanup();
        }
        
        #endregion
        
        #region 초기화
        
        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            stateManager = GetComponent<AIStateManager>();
            movement = GetComponent<AIMovement>();
            roomService = GetComponent<AIRoomService>();
            queueService = GetComponent<AIQueueService>();
            timeService = GetComponent<AITimeService>();
            behaviorController = GetComponent<AIBehaviorController>();
            
            if (stateManager == null || movement == null || roomService == null || 
                queueService == null || timeService == null || behaviorController == null)
            {
                Debug.LogError($"AIAgentNew {agentId}: 필수 컴포넌트가 누락되었습니다!");
            }
        }
        
        /// <summary>
        /// 시스템 초기화
        /// </summary>
        private void InitializeSystems()
        {
            // 각 컴포넌트 초기화
            stateManager.Initialize(agentId);
            movement.Initialize(spawnPoint);
            roomService.Initialize(agentId, movement, stateManager);
            queueService.Initialize(agentId, movement, stateManager);
            timeService.Initialize(agentId, stateManager, movement);
            behaviorController.Initialize(agentId, stateManager, movement, roomService, queueService, timeService);
            
            // 이벤트 구독
            queueService.OnServiceCompleted += OnServiceCompleteHandler;
            
            Debug.Log($"AIAgentNew {agentId}: 시스템 초기화 완료");
        }
        
        /// <summary>
        /// AI 초기화 (재활성화 시)
        /// </summary>
        public void InitializeAI()
        {
            if (stateManager != null)
            {
                stateManager.Reset();
            }
            
            if (movement != null && spawnPoint != null)
            {
                movement.Initialize(spawnPoint);
            }
            
            Debug.Log($"AIAgentNew {agentId}: AI 초기화 완료");
        }
        
        #endregion
        
        #region 외부 인터페이스 (기존 시스템과의 호환성)
        
        /// <summary>
        /// 스포너 설정
        /// </summary>
        public void SetSpawner(AISpawner aiSpawner)
        {
            spawner = aiSpawner;
            spawnPoint = aiSpawner.transform;
            
            if (movement != null)
            {
                movement.Initialize(spawnPoint);
            }
        }
        
        /// <summary>
        /// 대기열 목적지 설정 (CounterManager와의 호환성)
        /// </summary>
        public void SetQueueDestination(Vector3 position)
        {
            queueService?.SetQueueDestination(position);
        }
        
        /// <summary>
        /// 서비스 완료 처리 (CounterManager와의 호환성)
        /// </summary>
        public void OnServiceComplete()
        {
            queueService?.CompleteService();
            OnServiceComplete?.Invoke(this);
        }
        
        #endregion
        
        #region 상태 조회 메서드
        
        /// <summary>
        /// 현재 AI 정보 반환
        /// </summary>
        public string GetAIInfo()
        {
            if (behaviorController == null) return "초기화되지 않음";
            
            return behaviorController.GetBehaviorInfo();
        }
        
        /// <summary>
        /// 간단한 상태 정보
        /// </summary>
        public string GetSimpleStatus()
        {
            return $"{agentId[..8]}: {stateManager?.CurrentDestination ?? "알 수 없음"}";
        }
        
        /// <summary>
        /// 룸 사용 여부
        /// </summary>
        public bool IsUsingRoom()
        {
            return roomService?.HasRoom ?? false;
        }
        
        /// <summary>
        /// 대기열 상태
        /// </summary>
        public bool IsInQueue()
        {
            return queueService?.IsInQueue ?? false;
        }
        
        #endregion
        
        #region 이벤트 핸들러
        
        /// <summary>
        /// 서비스 완료 핸들러
        /// </summary>
        private void OnServiceCompleteHandler()
        {
            // 외부 시스템에 알림
            OnServiceComplete?.Invoke(this);
        }
        
        #endregion
        
        #region UI 디버그 (기존 호환성)
        
        private void OnGUI()
        {
            // 디버그 UI가 활성화된 경우에만 표시
            if (!AIConfiguration.globalShowDebugUI || !configuration.debugUIEnabled) return;
            
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
            if (screenPos.z > 0)
            {
                Vector2 guiPosition = new Vector2(screenPos.x, Screen.height - screenPos.y);
                string displayText = stateManager?.CurrentDestination ?? "상태 불명";
                GUI.Label(new Rect(guiPosition.x - 50, guiPosition.y - 50, 100, 40), displayText);
            }
        }
        
        #endregion
        
        #region 정리
        
        /// <summary>
        /// 리소스 정리
        /// </summary>
        private void Cleanup()
        {
            // 행동 컨트롤러 정리
            behaviorController?.Cleanup();
            
            // 서비스들 정리
            roomService?.Cleanup();
            queueService?.Reset();
            timeService?.Reset();
            
            // 이벤트 구독 해제
            if (queueService != null)
                queueService.OnServiceCompleted -= OnServiceCompleteHandler;
            
            AIEvents.TriggerAgentDespawned(agentId);
            
            Debug.Log($"AIAgentNew {agentId}: 정리 완료");
        }
        
        #endregion
        
        #region 디버그 메서드
        
        /// <summary>
        /// 강제 상태 변경 (디버그용)
        /// </summary>
        [ContextMenu("디버그: 배회 상태로 변경")]
        public void DebugSetWandering()
        {
            stateManager?.ChangeState(AIState.Wandering, "디버그: 강제 배회");
        }
        
        /// <summary>
        /// 강제 대기열 진입 (디버그용)
        /// </summary>
        [ContextMenu("디버그: 대기열 진입")]
        public void DebugJoinQueue()
        {
            stateManager?.ChangeState(AIState.MovingToQueue, "디버그: 강제 대기열 진입");
        }
        
        /// <summary>
        /// 강제 퇴장 (디버그용)
        /// </summary>
        [ContextMenu("디버그: 강제 퇴장")]
        public void DebugForceLeave()
        {
            timeService?.ScheduleDespawn();
        }
        
        /// <summary>
        /// AI 정보 출력 (디버그용)
        /// </summary>
        [ContextMenu("디버그: AI 정보 출력")]
        public void DebugPrintInfo()
        {
            Debug.Log($"=== AI 정보 ({agentId}) ===\n{GetAIInfo()}");
        }
        
        #endregion
        
        #region 메모리 관리
        
        /// <summary>
        /// 메모리 정리 (메모리 누수 방지)
        /// </summary>
        private void OnDestroy()
        {
            try
            {
                // 모든 서비스 정리
                behaviorController?.Cleanup();
                movement?.Cleanup();
                roomService?.Cleanup();
                queueService?.Cleanup();
                timeService?.Cleanup();
                
                // 상태 관리자 정리
                stateManager?.Cleanup();
                
                // 참조 정리
                behaviorController = null;
                movement = null;
                roomService = null;
                queueService = null;
                timeService = null;
                stateManager = null;
                agentId = null;
                navMeshAgent = null;
                
                Debug.Log($"[AIAgentNew] 메모리 정리 완료: {name}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AIAgentNew 정리 중 오류: {ex.Message}");
            }
        }
        
        #endregion
    }
} 