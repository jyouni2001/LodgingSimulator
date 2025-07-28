using System;
using UnityEngine;

namespace JY
{
    /// <summary>
    /// AI 이벤트 시스템
    /// </summary>
    public static class AIEvents
    {
        // 상태 변경 이벤트
        public static event Action<string, AIState, AIState> OnStateChanged;
        
        // 룸 관련 이벤트
        public static event Action<string, int> OnRoomAssigned;
        public static event Action<string, int> OnRoomReleased;
        public static event Action<string> OnRoomUsageStarted;
        public static event Action<string> OnRoomUsageCompleted;
        
        // 대기열 관련 이벤트
        public static event Action<string> OnQueueJoined;
        public static event Action<string> OnQueueLeft;
        public static event Action<string> OnServiceStarted;
        public static event Action<string> OnServiceCompleted;
        
        // 생명주기 이벤트
        public static event Action<string> OnAgentSpawned;
        public static event Action<string> OnAgentDespawned;
        
        // 이벤트 발생 메서드들
        public static void TriggerStateChanged(string agentId, AIState oldState, AIState newState)
        {
            OnStateChanged?.Invoke(agentId, oldState, newState);
        }
        
        public static void TriggerRoomAssigned(string agentId, int roomIndex)
        {
            OnRoomAssigned?.Invoke(agentId, roomIndex);
        }
        
        public static void TriggerRoomReleased(string agentId, int roomIndex)
        {
            OnRoomReleased?.Invoke(agentId, roomIndex);
        }
        
        public static void TriggerRoomUsageStarted(string agentId)
        {
            OnRoomUsageStarted?.Invoke(agentId);
        }
        
        public static void TriggerRoomUsageCompleted(string agentId)
        {
            OnRoomUsageCompleted?.Invoke(agentId);
        }
        
        public static void TriggerQueueJoined(string agentId)
        {
            OnQueueJoined?.Invoke(agentId);
        }
        
        public static void TriggerQueueLeft(string agentId)
        {
            OnQueueLeft?.Invoke(agentId);
        }
        
        public static void TriggerServiceStarted(string agentId)
        {
            OnServiceStarted?.Invoke(agentId);
        }
        
        public static void TriggerServiceCompleted(string agentId)
        {
            OnServiceCompleted?.Invoke(agentId);
        }
        
        public static void TriggerAgentSpawned(string agentId)
        {
            OnAgentSpawned?.Invoke(agentId);
        }
        
        public static void TriggerAgentDespawned(string agentId)
        {
            OnAgentDespawned?.Invoke(agentId);
        }
    }
} 