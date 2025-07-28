using UnityEngine;
using System.Collections.Generic;
using System;

namespace JY
{
    /// <summary>
    /// 중앙화된 룸 이벤트 관리 시스템
    /// 시스템 간 결합도를 낮추고 이벤트 기반 아키텍처 구현
    /// </summary>
    public class RoomEventManager : MonoBehaviour, IRoomEvents
    {
        #region 싱글톤
        
        private static RoomEventManager instance;
        public static RoomEventManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<RoomEventManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("RoomEventManager");
                        instance = go.AddComponent<RoomEventManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }
        
        #endregion
        
        #region 이벤트 정의
        
        // Room Detection Events
        public event Action<List<RoomInfo>> OnRoomsDetected;
        public event Action<RoomInfo> OnRoomCreated;
        public event Action<RoomInfo> OnRoomDestroyed;
        public event Action<string, int> OnFloorChanged;
        
        // Room Usage Events (AI 시스템용)
        public event Action<string, RoomInfo> OnRoomOccupied;    // agentId, room
        public event Action<string, RoomInfo> OnRoomReleased;    // agentId, room  
        public event Action<string> OnRoomUsageCompleted;        // agentId
        
        // System Events
        public event Action<string> OnSystemInitialized;        // systemName
        public event Action<string> OnSystemShutdown;           // systemName
        public event Action OnAllSystemsReady;
        
        // Debug Events
        public event Action<string, bool> OnDebugMessage;       // message, isImportant
        
        #endregion
        
        #region Unity 생명주기
        
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                
                Debug.Log("[RoomEventManager] 중앙 이벤트 시스템 초기화 완료");
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }
        
        private void OnDestroy()
        {
            if (instance == this)
            {
                ClearAllEvents();
                instance = null;
            }
        }
        
        #endregion
        
        #region IRoomEvents 구현
        
        public void TriggerRoomsDetected(List<RoomInfo> rooms)
        {
            try
            {
                OnRoomsDetected?.Invoke(rooms);
                LogEvent($"방 감지 완료: {rooms?.Count ?? 0}개", false);
            }
            catch (Exception ex)
            {
                LogError($"RoomsDetected 이벤트 처리 중 오류: {ex.Message}");
            }
        }
        
        public void TriggerRoomCreated(RoomInfo room)
        {
            try
            {
                OnRoomCreated?.Invoke(room);
                LogEvent($"방 생성: {room?.roomId}", false);
            }
            catch (Exception ex)
            {
                LogError($"RoomCreated 이벤트 처리 중 오류: {ex.Message}");
            }
        }
        
        public void TriggerRoomDestroyed(RoomInfo room)
        {
            try
            {
                OnRoomDestroyed?.Invoke(room);
                LogEvent($"방 제거: {room?.roomId}", false);
            }
            catch (Exception ex)
            {
                LogError($"RoomDestroyed 이벤트 처리 중 오류: {ex.Message}");
            }
        }
        
        public void TriggerFloorChanged(string systemName, int newFloor)
        {
            try
            {
                OnFloorChanged?.Invoke(systemName, newFloor);
                LogEvent($"층 변경: {systemName} → {newFloor}층", true);
            }
            catch (Exception ex)
            {
                LogError($"FloorChanged 이벤트 처리 중 오류: {ex.Message}");
            }
        }
        
        #endregion
        
        #region 추가 이벤트 트리거 메서드
        
        public void TriggerRoomOccupied(string agentId, RoomInfo room)
        {
            try
            {
                OnRoomOccupied?.Invoke(agentId, room);
                LogEvent($"방 사용 시작: {agentId} → {room?.roomId}", false);
            }
            catch (Exception ex)
            {
                LogError($"RoomOccupied 이벤트 처리 중 오류: {ex.Message}");
            }
        }
        
        public void TriggerRoomReleased(string agentId, RoomInfo room)
        {
            try
            {
                OnRoomReleased?.Invoke(agentId, room);
                LogEvent($"방 사용 종료: {agentId} ← {room?.roomId}", false);
            }
            catch (Exception ex)
            {
                LogError($"RoomReleased 이벤트 처리 중 오류: {ex.Message}");
            }
        }
        
        public void TriggerRoomUsageCompleted(string agentId)
        {
            try
            {
                OnRoomUsageCompleted?.Invoke(agentId);
                LogEvent($"방 이용 완료: {agentId}", false);
            }
            catch (Exception ex)
            {
                LogError($"RoomUsageCompleted 이벤트 처리 중 오류: {ex.Message}");
            }
        }
        
        public void TriggerSystemInitialized(string systemName)
        {
            try
            {
                OnSystemInitialized?.Invoke(systemName);
                LogEvent($"시스템 초기화: {systemName}", true);
                
                CheckAllSystemsReady();
            }
            catch (Exception ex)
            {
                LogError($"SystemInitialized 이벤트 처리 중 오류: {ex.Message}");
            }
        }
        
        public void TriggerSystemShutdown(string systemName)
        {
            try
            {
                OnSystemShutdown?.Invoke(systemName);
                LogEvent($"시스템 종료: {systemName}", true);
            }
            catch (Exception ex)
            {
                LogError($"SystemShutdown 이벤트 처리 중 오류: {ex.Message}");
            }
        }
        
        #endregion
        
        #region 시스템 상태 관리
        
        private readonly HashSet<string> initializedSystems = new HashSet<string>();
        private readonly string[] requiredSystems = { "RoomDetector", "PlacementSystem", "AISystem" };
        
        private void CheckAllSystemsReady()
        {
            bool allReady = true;
            foreach (string system in requiredSystems)
            {
                if (!initializedSystems.Contains(system))
                {
                    allReady = false;
                    break;
                }
            }
            
            if (allReady)
            {
                OnAllSystemsReady?.Invoke();
                LogEvent("모든 시스템 준비 완료", true);
            }
        }
        
        public bool IsSystemInitialized(string systemName)
        {
            return initializedSystems.Contains(systemName);
        }
        
        public void RegisterSystemInitialized(string systemName)
        {
            initializedSystems.Add(systemName);
            TriggerSystemInitialized(systemName);
        }
        
        public void UnregisterSystem(string systemName)
        {
            initializedSystems.Remove(systemName);
            TriggerSystemShutdown(systemName);
        }
        
        #endregion
        
        #region 디버그 및 로깅
        
        [Header("디버그 설정")]
        [SerializeField] private bool showEventLogs = true;
        [SerializeField] private bool showImportantEventsOnly = false;
        
        private void LogEvent(string message, bool isImportant)
        {
            if (!showEventLogs) return;
            if (showImportantEventsOnly && !isImportant) return;
            
            Debug.Log($"[RoomEventManager] {message}");
            OnDebugMessage?.Invoke(message, isImportant);
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[RoomEventManager] {message}");
            OnDebugMessage?.Invoke($"ERROR: {message}", true);
        }
        
        [ContextMenu("이벤트 구독자 상태 출력")]
        public void PrintEventSubscribers()
        {
            var info = "=== RoomEventManager 이벤트 구독 상태 ===\n";
            info += $"OnRoomsDetected: {OnRoomsDetected?.GetInvocationList()?.Length ?? 0}개 구독자\n";
            info += $"OnRoomCreated: {OnRoomCreated?.GetInvocationList()?.Length ?? 0}개 구독자\n";
            info += $"OnRoomDestroyed: {OnRoomDestroyed?.GetInvocationList()?.Length ?? 0}개 구독자\n";
            info += $"OnFloorChanged: {OnFloorChanged?.GetInvocationList()?.Length ?? 0}개 구독자\n";
            info += $"OnRoomOccupied: {OnRoomOccupied?.GetInvocationList()?.Length ?? 0}개 구독자\n";
            info += $"OnRoomReleased: {OnRoomReleased?.GetInvocationList()?.Length ?? 0}개 구독자\n";
            info += $"초기화된 시스템: {initializedSystems.Count}개\n";
            
            foreach (string system in initializedSystems)
            {
                info += $"  - {system}\n";
            }
            
            Debug.Log(info);
        }
        
        #endregion
        
        #region 메모리 관리
        
        /// <summary>
        /// 모든 이벤트 구독 해제 (메모리 누수 방지)
        /// </summary>
        public void ClearAllEvents()
        {
            try
            {
                OnRoomsDetected = null;
                OnRoomCreated = null;
                OnRoomDestroyed = null;
                OnFloorChanged = null;
                OnRoomOccupied = null;
                OnRoomReleased = null;
                OnRoomUsageCompleted = null;
                OnSystemInitialized = null;
                OnSystemShutdown = null;
                OnAllSystemsReady = null;
                OnDebugMessage = null;
                
                initializedSystems.Clear();
                
                LogEvent("모든 이벤트 정리 완료", true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"이벤트 정리 중 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 특정 시스템의 이벤트만 해제
        /// </summary>
        public void UnsubscribeSystem(string systemName)
        {
            // C#의 제한으로 특정 구독자만 해제하기 어려우므로
            // 시스템별 이벤트 관리는 각 시스템에서 직접 처리
            LogEvent($"시스템 이벤트 구독 해제 요청: {systemName}", false);
        }
        
        #endregion
    }
} 