using UnityEngine;

namespace JY
{
    /// <summary>
    /// AI 시간 서비스 클래스
    /// 시간 기반 행동 결정과 스케줄 관리를 담당
    /// </summary>
    public class AITimeService : MonoBehaviour
    {
        [Header("시간 설정")]
        [SerializeField] private int despawnHour = 17; // 17시 디스폰
        [SerializeField] private bool enableTimeBasedBehavior = true;
        
        // 시간 상태
        private TimeSystem timeSystem;
        private int lastBehaviorUpdateHour = -1;
        private bool isScheduledForDespawn = false;
        
        // 의존성
        private AIStateManager stateManager;
        private AIMovement aiMovement;
        private string agentId;
        
        // 이벤트
        public System.Action OnDespawnScheduled;
        public System.Action<int> OnHourlyBehaviorUpdate;
        
        // 속성
        public bool IsScheduledForDespawn => isScheduledForDespawn;
        public int CurrentHour => timeSystem?.CurrentHour ?? -1;
        public int CurrentMinute => timeSystem?.CurrentMinute ?? -1;
        
        /// <summary>
        /// 초기화
        /// </summary>
        public void Initialize(string id, AIStateManager state, AIMovement movement)
        {
            agentId = id;
            stateManager = state;
            aiMovement = movement;
            
            // TimeSystem 찾기 (ServiceLocator 사용)
            timeSystem = ServiceLocator.TimeSystem as TimeSystem;
            if (timeSystem == null)
            {
                Debug.LogWarning($"AITimeService {agentId}: TimeSystem을 찾을 수 없습니다.");
                enableTimeBasedBehavior = false;
            }
        }
        
        /// <summary>
        /// 시간 기반 행동 업데이트
        /// </summary>
        public void UpdateTimeBehavior()
        {
            if (!enableTimeBasedBehavior || timeSystem == null) return;
            
            int currentHour = timeSystem.CurrentHour;
            
            // 시간이 변경되었을 때만 처리
            if (currentHour == lastBehaviorUpdateHour) return;
            
            lastBehaviorUpdateHour = currentHour;
            
            // 디스폰 시간 확인
            CheckDespawnTime(currentHour);
            
            // 시간별 행동 결정
            DetermineBehaviorByTime(currentHour);
            
            OnHourlyBehaviorUpdate?.Invoke(currentHour);
        }
        
        /// <summary>
        /// 디스폰 시간 확인
        /// </summary>
        private void CheckDespawnTime(int hour)
        {
            if (hour >= despawnHour && !isScheduledForDespawn)
            {
                // 룸을 사용 중이 아닌 경우에만 디스폰 예약
                if (!stateManager.IsUsingRoom())
                {
                    ScheduleDespawn();
                }
                else
                {
                    Debug.Log($"AI {agentId}: {hour}시 디스폰 시간이지만 룸 사용 중이므로 대기");
                }
            }
        }
        
        /// <summary>
        /// 디스폰 예약
        /// </summary>
        public void ScheduleDespawn()
        {
            if (isScheduledForDespawn) return;
            
            isScheduledForDespawn = true;
            stateManager.ChangeState(AIState.LeavingFinal, "퇴장 예정");
            
            OnDespawnScheduled?.Invoke();
            
            Debug.Log($"AI {agentId}: {despawnHour}시 디스폰 예약됨");
        }
        
        /// <summary>
        /// 시간별 행동 결정
        /// </summary>
        private void DetermineBehaviorByTime(int hour)
        {
            if (isScheduledForDespawn)
            {
                Debug.Log($"AI {agentId}: 디스폰 예정이므로 행동 재결정 생략");
                return;
            }
            
            Debug.Log($"AI {agentId}: {hour}시 행동 재결정 시작");
            
            // 중요한 상태에서는 행동 재결정 생략
            if (stateManager.IsUsingRoom() || stateManager.CurrentState == AIState.ReportingRoom)
            {
                Debug.Log($"AI {agentId}: {hour}시 중요한 상태로 행동 재결정 생략 (상태: {stateManager.CurrentState})");
                return;
            }
            
            // 시간별 특별 행동 (필요시 확장)
            switch (hour)
            {
                case 6: // 아침
                case 7:
                case 8:
                    DecideEarlyMorningBehavior();
                    break;
                    
                case 12: // 점심
                case 13:
                    DecideLunchTimeBehavior();
                    break;
                    
                case 18: // 저녁 (디스폰 후)
                case 19:
                case 20:
                    DecideEveningBehavior();
                    break;
                    
                default:
                    DecideDefaultBehavior();
                    break;
            }
        }
        
        /// <summary>
        /// 이른 아침 행동
        /// </summary>
        private void DecideEarlyMorningBehavior()
        {
            // 활발한 활동 시간
            if (stateManager.CurrentState == AIState.Wandering)
            {
                // 대기열로 이동 시도
                stateManager.ChangeState(AIState.MovingToQueue, "아침 서비스 이용");
            }
        }
        
        /// <summary>
        /// 점심 시간 행동
        /// </summary>
        private void DecideLunchTimeBehavior()
        {
            // 점심 시간 특별 행동 (필요시 구현)
            DecideDefaultBehavior();
        }
        
        /// <summary>
        /// 저녁 행동
        /// </summary>
        private void DecideEveningBehavior()
        {
            // 저녁 시간은 이미 디스폰된 상태이므로 특별한 처리 불필요
            DecideDefaultBehavior();
        }
        
        /// <summary>
        /// 기본 행동
        /// </summary>
        private void DecideDefaultBehavior()
        {
            // 현재 상태에 따른 기본 행동
            switch (stateManager.CurrentState)
            {
                case AIState.Wandering:
                    // 배회 중이면 가끔 대기열로 이동
                    if (Random.Range(0f, 1f) < 0.3f) // 30% 확률
                    {
                        stateManager.ChangeState(AIState.MovingToQueue, "서비스 이용 시도");
                    }
                    break;
                    
                case AIState.UseWandering:
                    // 룸에서 배회 중이면 계속 배회
                    Debug.Log($"AI {agentId}: 룸에서 배회 재결정");
                    break;
            }
        }
        
        /// <summary>
        /// 대체 행동 (TimeSystem이 없을 때)
        /// </summary>
        public void FallbackBehavior()
        {
            Debug.Log($"AI {agentId}: TimeSystem 없음, 기본 행동으로 전환");
            
            // 기본적으로 대기열로 이동
            if (stateManager.CurrentState == AIState.Wandering)
            {
                stateManager.ChangeState(AIState.MovingToQueue, "기본 서비스 이용");
            }
        }
        
        /// <summary>
        /// 시간 서비스 리셋
        /// </summary>
        public void Reset()
        {
            lastBehaviorUpdateHour = -1;
            isScheduledForDespawn = false;
        }
        
        /// <summary>
        /// 현재 시간 정보 반환
        /// </summary>
        public string GetTimeInfo()
        {
            if (timeSystem == null) return "시간 시스템 없음";
            
            return $"{timeSystem.CurrentHour:D2}:{timeSystem.CurrentMinute:D2} " +
                   $"(디스폰 예정: {isScheduledForDespawn})";
        }
    }
} 