using UnityEngine;

namespace JY
{
    /// <summary>
    /// AI 상태 관리 클래스
    /// 상태 변경과 상태별 로직을 담당
    /// </summary>
    public class AIStateManager : MonoBehaviour
    {
        [Header("상태 정보")]
        [SerializeField] private AIState currentState = AIState.MovingToQueue;
        [SerializeField] private string currentDestination = "대기열로 이동 중";
        
        // 의존성
        private string agentId;
        
        // 상태 속성
        public AIState CurrentState => currentState;
        public string CurrentDestination => currentDestination;
        
        // 이벤트
        public System.Action<AIState, AIState> OnStateChanged;
        
        /// <summary>
        /// 초기화
        /// </summary>
        public void Initialize(string id)
        {
            agentId = id;
            currentState = AIState.MovingToQueue;
            currentDestination = "대기열로 이동 중";
        }
        
        /// <summary>
        /// 상태 변경
        /// </summary>
        public void ChangeState(AIState newState, string destination = "")
        {
            AIState previousState = currentState;
            currentState = newState;
            
            if (!string.IsNullOrEmpty(destination))
            {
                currentDestination = destination;
            }
            else
            {
                currentDestination = GetDefaultDestination(newState);
            }
            
            // 이벤트 발생
            OnStateChanged?.Invoke(previousState, newState);
            AIEvents.TriggerStateChanged(agentId, previousState, newState);
            
            Debug.Log($"AI {agentId}: 상태 변경 {previousState} -> {newState} ({currentDestination})");
        }
        
        /// <summary>
        /// 상태별 기본 목적지 설정
        /// </summary>
        private string GetDefaultDestination(AIState state)
        {
            return state switch
            {
                AIState.Wandering => "배회 중",
                AIState.MovingToQueue => "대기열로 이동 중",
                AIState.WaitingInQueue => "대기열에서 대기 중",
                AIState.MovingToRoom => "방으로 이동 중",
                AIState.UsingRoom => "방 사용 중",
                AIState.UseWandering => "방에서 배회 중",
                AIState.ReportingRoom => "방 사용 완료 보고 중",
                AIState.ReportingRoomQueue => "보고 대기열에서 대기",
                AIState.LeavingFinal => "최종 퇴장 중",
                _ => "알 수 없는 상태"
            };
        }
        
        /// <summary>
        /// 상태 확인 메서드들
        /// </summary>
        public bool IsMoving()
        {
            return currentState == AIState.MovingToQueue || 
                   currentState == AIState.MovingToRoom || 
                   currentState == AIState.LeavingFinal;
        }
        
        public bool IsWaiting()
        {
            return currentState == AIState.WaitingInQueue || 
                   currentState == AIState.ReportingRoomQueue;
        }
        
        public bool IsUsingRoom()
        {
            return currentState == AIState.UsingRoom || 
                   currentState == AIState.UseWandering;
        }
        
        public bool CanChangeToState(AIState newState)
        {
            // 상태 변경 규칙 정의
            return newState switch
            {
                AIState.MovingToQueue => currentState == AIState.Wandering,
                AIState.WaitingInQueue => currentState == AIState.MovingToQueue,
                AIState.MovingToRoom => currentState == AIState.WaitingInQueue,
                AIState.UsingRoom => currentState == AIState.MovingToRoom,
                AIState.UseWandering => currentState == AIState.UsingRoom,
                AIState.ReportingRoom => IsUsingRoom(),
                AIState.ReportingRoomQueue => currentState == AIState.ReportingRoom,
                AIState.LeavingFinal => true, // 언제든 퇴장 가능
                _ => false
            };
        }
        
        /// <summary>
        /// AI 재초기화
        /// </summary>
        public void Reset()
        {
            ChangeState(AIState.MovingToQueue, "대기열로 이동 중");
        }
    }
} 