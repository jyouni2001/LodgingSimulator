using UnityEngine;
using System;

namespace JY
{
    /// <summary>
    /// AI 상태 관리를 담당하는 클래스
    /// 상태 전환과 현재 상태 추적을 처리
    /// </summary>
    public class AIStateMachine
    {
        public enum AIState
        {
            Wandering,           // 외부 배회
            MovingToQueue,       // 대기열로 이동
            WaitingInQueue,      // 대기열에서 대기
            MovingToRoom,        // 배정된 방으로 이동
            UsingRoom,           // 방 사용
            UseWandering,        // 방 사용 중 배회
            ReportingRoom,       // 방 사용 완료 보고
            ReturningToSpawn,    // 스폰 지점으로 복귀
            RoomWandering,       // 방 내부 배회
            ReportingRoomQueue   // 방 사용 완료 보고를 위해 대기열로 이동
        }

        private AIState _currentState = AIState.MovingToQueue;
        private string _currentDestination = "대기열로 이동 중";

        public AIState CurrentState => _currentState;
        public string CurrentDestination => _currentDestination;

        // 상태 변경 이벤트
        public event Action<AIState, AIState> OnStateChanged;

        /// <summary>
        /// 상태 전환
        /// </summary>
        public void TransitionToState(AIState newState)
        {
            if (_currentState == newState) return;

            AIState oldState = _currentState;
            _currentState = newState;
            UpdateDestinationText();
            
            OnStateChanged?.Invoke(oldState, newState);
        }

        /// <summary>
        /// 중요한 상태인지 확인 (방 사용 관련 상태)
        /// </summary>
        public bool IsInCriticalState()
        {
            return _currentState == AIState.MovingToRoom || 
                   _currentState == AIState.UsingRoom || 
                   _currentState == AIState.RoomWandering ||
                   _currentState == AIState.UseWandering ||
                   _currentState == AIState.ReportingRoom ||
                   _currentState == AIState.ReportingRoomQueue;
        }

        /// <summary>
        /// 방 관련 상태인지 확인
        /// </summary>
        public bool IsInRoomRelatedState()
        {
            return _currentState == AIState.UsingRoom || 
                   _currentState == AIState.RoomWandering ||
                   _currentState == AIState.UseWandering;
        }

        /// <summary>
        /// 현재 상태에 따른 목적지 텍스트 업데이트
        /// </summary>
        private void UpdateDestinationText()
        {
            _currentDestination = _currentState switch
            {
                AIState.Wandering => "자유 배회 중",
                AIState.MovingToQueue => "대기열로 이동 중",
                AIState.WaitingInQueue => "대기열에서 대기 중",
                AIState.MovingToRoom => "배정된 방으로 이동 중",
                AIState.UsingRoom => "방 사용 중",
                AIState.UseWandering => "방 외부 배회 중",
                AIState.ReportingRoom => "방 사용 완료 보고 중",
                AIState.ReturningToSpawn => "디스폰 지점으로 복귀 중",
                AIState.RoomWandering => "방 내부 배회 중",
                AIState.ReportingRoomQueue => "체크아웃 대기열로 이동 중",
                _ => "알 수 없는 상태"
            };
        }
    }
}