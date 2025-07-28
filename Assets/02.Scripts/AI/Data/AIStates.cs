using UnityEngine;
using System;

namespace JY
{
    /// <summary>
    /// AI 상태 열거형
    /// </summary>
    public enum AIState
    {
        Wandering,           // 외부 배회
        MovingToQueue,       // 대기열로 이동
        WaitingInQueue,      // 대기열에서 대기
        MovingToRoom,        // 배정된 방으로 이동
        UsingRoom,           // 방 사용
        UseWandering,        // 방 사용 중 배회
        ReportingRoom,       // 방 사용 완료 보고
        ReportingRoomQueue,  // 방 사용 완료 보고 대기열
        LeavingFinal         // 최종 퇴장
    }

    /// <summary>
    /// AI 이벤트 데이터
    /// </summary>
    [Serializable]
    public class AIEventData
    {
        public string agentId;
        public AIState previousState;
        public AIState newState;
        public float timestamp;
        public string reason;
    }

    /// <summary>
    /// AI 설정 데이터
    /// </summary>
    [Serializable]
    public class AIConfiguration
    {
        [Header("이동 설정")]
        public float arrivalDistance = 0.5f;
        public int maxRetries = 3;
        
        [Header("서비스 설정")]
        public float counterWaitTime = 5f;
        
        [Header("디버그 설정")]
        public bool debugUIEnabled = true;
        public static bool globalShowDebugUI = true;
    }
} 