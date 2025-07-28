using UnityEngine;
using System.Collections;

namespace JY
{
    /// <summary>
    /// AI 행동 컨트롤러 클래스
    /// AI의 전체적인 행동 흐름과 결정을 담당
    /// </summary>
    public class AIBehaviorController : MonoBehaviour
    {
        [Header("행동 설정")]
        [SerializeField] private float behaviorUpdateInterval = 1f;
        [SerializeField] private float wanderingRadius = 10f;
        
        // 의존성 컴포넌트들
        private AIStateManager stateManager;
        private AIMovement aiMovement;
        private AIRoomService roomService;
        private AIQueueService queueService;
        private AITimeService timeService;
        private string agentId;
        
        // 행동 코루틴
        private Coroutine behaviorUpdateCoroutine;
        private Coroutine wanderingCoroutine;
        
        // 이벤트
        public System.Action<string> OnBehaviorChanged;
        
        /// <summary>
        /// 초기화
        /// </summary>
        public void Initialize(string id, AIStateManager state, AIMovement movement, 
                             AIRoomService room, AIQueueService queue, AITimeService time)
        {
            agentId = id;
            stateManager = state;
            aiMovement = movement;
            roomService = room;
            queueService = queue;
            timeService = time;
            
            // 상태 변경 이벤트 구독
            stateManager.OnStateChanged += OnStateChangedHandler;
            
            // 이동 이벤트 구독
            aiMovement.OnDestinationReached += OnDestinationReachedHandler;
            aiMovement.OnMovementFailed += OnMovementFailedHandler;
            
            // 룸 서비스 이벤트 구독
            roomService.OnRoomUsageCompleted += OnRoomUsageCompletedHandler;
            
            // 대기열 서비스 이벤트 구독
            queueService.OnServiceCompleted += OnServiceCompletedHandler;
            
            // 시간 서비스 이벤트 구독
            timeService.OnDespawnScheduled += OnDespawnScheduledHandler;
        }
        
        /// <summary>
        /// 행동 시작
        /// </summary>
        public void StartBehavior()
        {
            if (behaviorUpdateCoroutine != null)
            {
                StopCoroutine(behaviorUpdateCoroutine);
            }
            
            behaviorUpdateCoroutine = StartCoroutine(BehaviorUpdateLoop());
            
            // 초기 행동 결정
            DetermineInitialBehavior();
        }
        
        /// <summary>
        /// 행동 중지
        /// </summary>
        public void StopBehavior()
        {
            if (behaviorUpdateCoroutine != null)
            {
                StopCoroutine(behaviorUpdateCoroutine);
                behaviorUpdateCoroutine = null;
            }
            
            if (wanderingCoroutine != null)
            {
                StopCoroutine(wanderingCoroutine);
                wanderingCoroutine = null;
            }
        }
        
        /// <summary>
        /// 행동 업데이트 루프
        /// </summary>
        private IEnumerator BehaviorUpdateLoop()
        {
            while (true)
            {
                // 시간 기반 행동 업데이트
                timeService.UpdateTimeBehavior();
                
                // 현재 상태에 따른 행동 처리
                ProcessCurrentStateBehavior();
                
                yield return new WaitForSeconds(behaviorUpdateInterval);
            }
        }
        
        /// <summary>
        /// 초기 행동 결정
        /// </summary>
        public void DetermineInitialBehavior()
        {
            // 시간 시스템이 있으면 시간 기반으로, 없으면 기본 행동
            if (timeService.CurrentHour >= 0)
            {
                timeService.UpdateTimeBehavior();
            }
            else
            {
                timeService.FallbackBehavior();
            }
        }
        
        /// <summary>
        /// 현재 상태별 행동 처리
        /// </summary>
        private void ProcessCurrentStateBehavior()
        {
            switch (stateManager.CurrentState)
            {
                case AIState.Wandering:
                    ProcessWanderingBehavior();
                    break;
                    
                case AIState.MovingToQueue:
                    ProcessMovingToQueueBehavior();
                    break;
                    
                case AIState.WaitingInQueue:
                    ProcessWaitingInQueueBehavior();
                    break;
                    
                case AIState.MovingToRoom:
                    ProcessMovingToRoomBehavior();
                    break;
                    
                case AIState.UsingRoom:
                case AIState.UseWandering:
                    ProcessUsingRoomBehavior();
                    break;
                    
                case AIState.ReportingRoom:
                    ProcessReportingRoomBehavior();
                    break;
                    
                case AIState.ReportingRoomQueue:
                    ProcessReportingRoomQueueBehavior();
                    break;
                    
                case AIState.LeavingFinal:
                    ProcessLeavingFinalBehavior();
                    break;
            }
        }
        
        /// <summary>
        /// 배회 행동 처리
        /// </summary>
        private void ProcessWanderingBehavior()
        {
            if (wanderingCoroutine == null && !aiMovement.IsMoving)
            {
                wanderingCoroutine = StartCoroutine(WanderingCoroutine());
            }
        }
        
        /// <summary>
        /// 대기열 이동 행동 처리
        /// </summary>
        private void ProcessMovingToQueueBehavior()
        {
            if (!aiMovement.IsMoving && !queueService.IsInQueue)
            {
                // 대기열 진입 시도
                if (!queueService.TryJoinQueue())
                {
                    // 대기열 진입 실패 시 배회로 전환
                    stateManager.ChangeState(AIState.Wandering, "대기열 진입 실패, 배회");
                }
            }
        }
        
        /// <summary>
        /// 대기열 대기 행동 처리
        /// </summary>
        private void ProcessWaitingInQueueBehavior()
        {
            if (queueService.CanReceiveService())
            {
                queueService.StartService();
            }
        }
        
        /// <summary>
        /// 룸 이동 행동 처리
        /// </summary>
        private void ProcessMovingToRoomBehavior()
        {
            if (!aiMovement.IsMoving && roomService.HasRoom)
            {
                // 룸에 도착했으므로 사용 시작
                roomService.StartUsingRoom();
            }
        }
        
        /// <summary>
        /// 룸 사용 행동 처리
        /// </summary>
        private void ProcessUsingRoomBehavior()
        {
            // 룸 서비스에서 자동으로 처리됨
        }
        
        /// <summary>
        /// 룸 보고 행동 처리
        /// </summary>
        private void ProcessReportingRoomBehavior()
        {
            // 대기열에 다시 진입하여 보고
            if (!queueService.IsInQueue)
            {
                stateManager.ChangeState(AIState.ReportingRoomQueue, "보고를 위한 대기열 진입");
                if (!queueService.TryJoinQueue())
                {
                    // 대기열 진입 실패 시 바로 퇴장
                    stateManager.ChangeState(AIState.LeavingFinal, "보고 실패, 퇴장");
                }
            }
        }
        
        /// <summary>
        /// 룸 보고 대기열 행동 처리
        /// </summary>
        private void ProcessReportingRoomQueueBehavior()
        {
            if (queueService.CanReceiveService())
            {
                queueService.StartService();
            }
        }
        
        /// <summary>
        /// 최종 퇴장 행동 처리
        /// </summary>
        private void ProcessLeavingFinalBehavior()
        {
            if (!aiMovement.IsMoving)
            {
                // 스폰 지점으로 이동
                if (!aiMovement.ReturnToSpawn())
                {
                    // 이동 실패 시 바로 디스폰
                    RequestDespawn();
                }
            }
        }
        
        /// <summary>
        /// 배회 코루틴
        /// </summary>
        private IEnumerator WanderingCoroutine()
        {
            while (stateManager.CurrentState == AIState.Wandering)
            {
                Vector3 wanderCenter = transform.position;
                if (aiMovement.WanderAround(wanderCenter, wanderingRadius))
                {
                    // 배회 위치로 이동 중
                    yield return new WaitUntil(() => !aiMovement.IsMoving);
                }
                
                // 다음 배회까지 대기
                yield return new WaitForSeconds(Random.Range(3f, 8f));
            }
            
            wanderingCoroutine = null;
        }
        
        #region 이벤트 핸들러
        
        /// <summary>
        /// 상태 변경 핸들러
        /// </summary>
        private void OnStateChangedHandler(AIState oldState, AIState newState)
        {
            OnBehaviorChanged?.Invoke($"상태 변경: {oldState} -> {newState}");
            
            // 배회 코루틴 정리
            if (newState != AIState.Wandering && wanderingCoroutine != null)
            {
                StopCoroutine(wanderingCoroutine);
                wanderingCoroutine = null;
            }
        }
        
        /// <summary>
        /// 목적지 도착 핸들러
        /// </summary>
        private void OnDestinationReachedHandler()
        {
            switch (stateManager.CurrentState)
            {
                case AIState.MovingToQueue:
                    // 대기열 위치에 도착
                    break;
                    
                case AIState.MovingToRoom:
                    // 룸에 도착했으므로 사용 시작은 ProcessMovingToRoomBehavior에서 처리
                    break;
                    
                case AIState.LeavingFinal:
                    // 스폰 지점에 도착, 디스폰 요청
                    RequestDespawn();
                    break;
            }
        }
        
        /// <summary>
        /// 이동 실패 핸들러
        /// </summary>
        private void OnMovementFailedHandler()
        {
            Debug.LogWarning($"AI {agentId}: 이동 실패 (상태: {stateManager.CurrentState})");
            
            // 상태에 따른 대안 행동
            switch (stateManager.CurrentState)
            {
                case AIState.MovingToQueue:
                    stateManager.ChangeState(AIState.Wandering, "대기열 이동 실패, 배회");
                    break;
                    
                case AIState.MovingToRoom:
                    stateManager.ChangeState(AIState.Wandering, "룸 이동 실패, 배회");
                    roomService.ReleaseRoom();
                    break;
                    
                case AIState.LeavingFinal:
                    RequestDespawn(); // 바로 디스폰
                    break;
            }
        }
        
        /// <summary>
        /// 룸 사용 완료 핸들러
        /// </summary>
        private void OnRoomUsageCompletedHandler()
        {
            stateManager.ChangeState(AIState.ReportingRoom, "룸 사용 완료 보고");
        }
        
        /// <summary>
        /// 서비스 완료 핸들러
        /// </summary>
        private void OnServiceCompletedHandler()
        {
            switch (stateManager.CurrentState)
            {
                case AIState.WaitingInQueue:
                    // 일반 서비스 완료 - 룸 찾기
                    if (roomService.TryFindAvailableRoom())
                    {
                        roomService.MoveToAssignedRoom();
                    }
                    else
                    {
                        stateManager.ChangeState(AIState.Wandering, "사용 가능한 룸 없음, 배회");
                    }
                    break;
                    
                case AIState.ReportingRoomQueue:
                    // 보고 서비스 완료 - 퇴장
                    stateManager.ChangeState(AIState.LeavingFinal, "보고 완료, 퇴장");
                    break;
            }
        }
        
        /// <summary>
        /// 디스폰 예약 핸들러
        /// </summary>
        private void OnDespawnScheduledHandler()
        {
            // 현재 하고 있는 일 정리
            queueService.Reset();
            roomService.ReleaseRoom();
            
            // 바로 퇴장 상태로 전환
            stateManager.ChangeState(AIState.LeavingFinal, "시간 종료, 퇴장");
        }
        
        #endregion
        
        /// <summary>
        /// 디스폰 요청
        /// </summary>
        private void RequestDespawn()
        {
            // AISpawner에게 디스폰 요청 (ServiceLocator 사용)
            var spawner = ServiceLocator.AISpawner;
            if (spawner != null)
            {
                spawner.DespawnAI(gameObject);
            }
            else
            {
                // 직접 제거
                Destroy(gameObject);
            }
            
            AIEvents.TriggerAgentDespawned(agentId);
        }
        
        /// <summary>
        /// 완전한 메모리 정리 (메모리 누수 방지)
        /// </summary>
        public void Cleanup()
        {
            try
            {
                StopBehavior();
                
                // 모든 이벤트 구독 해제
                if (stateManager != null)
                    stateManager.OnStateChanged -= OnStateChangedHandler;
                if (aiMovement != null)
                {
                    aiMovement.OnDestinationReached -= OnDestinationReachedHandler;
                    aiMovement.OnMovementFailed -= OnMovementFailedHandler;
                }
                if (roomService != null)
                    roomService.OnRoomUsageCompleted -= OnRoomUsageCompletedHandler;
                if (queueService != null)
                    queueService.OnServiceCompleted -= OnServiceCompletedHandler;
                if (timeService != null)
                    timeService.OnDespawnScheduled -= OnDespawnScheduledHandler;
                
                // 누락되었던 이벤트 해제
                OnBehaviorChanged = null;
                
                // 컴포넌트 참조 정리
                stateManager = null;
                aiMovement = null;
                roomService = null;
                queueService = null;
                timeService = null;
                agentId = null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AIBehaviorController 정리 중 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Unity 오브젝트 파괴 시 정리
        /// </summary>
        private void OnDestroy()
        {
            Cleanup();
        }
        
        /// <summary>
        /// 현재 행동 정보 반환
        /// </summary>
        public string GetBehaviorInfo()
        {
            return $"상태: {stateManager.CurrentState}, " +
                   $"목적지: {stateManager.CurrentDestination}, " +
                   $"이동중: {aiMovement.IsMoving}, " +
                   $"대기열: {queueService.GetQueueInfo()}, " +
                   $"룸: {(roomService.HasRoom ? $"룸 {roomService.CurrentRoomIndex + 1}" : "없음")}, " +
                   $"시간: {timeService.GetTimeInfo()}";
        }
    }
} 