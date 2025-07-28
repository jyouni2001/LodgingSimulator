using UnityEngine;
using System.Collections.Generic;
using System;

namespace JY
{
    /// <summary>
    /// 방 감지 시스템의 인터페이스
    /// </summary>
    public interface IRoomDetector
    {
        // 이벤트
        event System.Action<GameObject[]> OnRoomsUpdated;
        
        // 속성
        int DetectedRoomCount { get; }
        string CurrentScanStatus { get; }
        bool IsScanning { get; }
        
        // 메서드
        void ScanForRooms();
        void SetScanFloor(int floorLevel);
        void SetScanAllFloors(bool scanAll);
        List<RoomInfo> GetDetectedRooms();
        RoomStatistics GetRoomStatistics();
    }
    
    /// <summary>
    /// 방 데이터 관리 인터페이스
    /// </summary>
    public interface IRoomDataManager
    {
        // 이벤트
        event Action<List<RoomInfo>> OnRoomsUpdated;
        event Action<RoomInfo> OnRoomAdded;
        event Action<RoomInfo> OnRoomRemoved;
        
        // 메서드
        void UpdateRooms(RoomScanResult scanResult);
        void AddRoom(RoomInfo room);
        void RemoveRoom(string roomId);
        List<RoomInfo> GetAllRooms();
        List<RoomInfo> GetValidRooms();
        List<RoomInfo> GetInvalidRooms();
        RoomInfo GetRoomById(string roomId);
        void ClearAllRooms();
        void Dispose();
    }
    
    /// <summary>
    /// 방 팩토리 인터페이스
    /// </summary>
    public interface IRoomFactory
    {
        List<GameObject> CreateRoomGameObjects(List<RoomInfo> validRooms);
        GameObject CreateRoomGameObject(RoomInfo room);
        void CleanupExistingRooms();
        void CleanupRoomGameObject(RoomInfo room);
        void Dispose();
    }
    
    /// <summary>
    /// 건축 시스템 인터페이스 (의존성 분리용)
    /// </summary>
    public interface IPlacementSystem
    {
        bool GetFloorLock();
        int GetCurrentActiveFloor();
        bool IsFloorActive(int floor);
        int currentPurchaseLevel { get; }
    }
    
    /// <summary>
    /// 룸 서비스 인터페이스 (AI 시스템용)
    /// </summary>
    public interface IRoomService
    {
        // 속성
        bool HasRoom { get; }
        RoomInfo CurrentRoom { get; }
        int CurrentRoomIndex { get; }
        
        // 이벤트
        event System.Action<RoomInfo> OnRoomEntered;
        event System.Action<RoomInfo> OnRoomExited;
        event System.Action OnRoomUsageCompleted;
        
        // 메서드
        void Initialize(string agentId, AIMovement movement, AIStateManager state);
        bool TryFindAvailableRoom();
        void MoveToRoom();
        void StartUsingRoom();
        void CompleteRoomUsage();
        void ReleaseRoom();
        void UpdateRoomList(List<RoomInfo> newRooms);
    }
    
    /// <summary>
    /// 룸 이벤트 인터페이스
    /// </summary>
    public interface IRoomEvents
    {
        event Action<List<RoomInfo>> OnRoomsDetected;
        event Action<RoomInfo> OnRoomCreated;
        event Action<RoomInfo> OnRoomDestroyed;
        event Action<string, int> OnFloorChanged;
        
        void TriggerRoomsDetected(List<RoomInfo> rooms);
        void TriggerRoomCreated(RoomInfo room);
        void TriggerRoomDestroyed(RoomInfo room);
        void TriggerFloorChanged(string systemName, int newFloor);
    }
} 