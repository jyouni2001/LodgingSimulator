using UnityEngine;
using System.Collections.Generic;

namespace JY.AI.Interfaces
{
    /// <summary>
    /// 방 정보를 제공하는 인터페이스
    /// AI 시스템이 RoomManager에 직접 의존하지 않도록 추상화
    /// </summary>
    public interface IRoomProvider
    {
        /// <summary>
        /// 사용 가능한 방이 있는지 확인
        /// </summary>
        bool HasAvailableRooms();
        
        /// <summary>
        /// 방 할당 시도
        /// </summary>
        bool TryAssignRoom(string aiId, out RoomAssignment assignment);
        
        /// <summary>
        /// 방 해제
        /// </summary>
        void ReleaseRoom(string aiId, string roomId);
        
        /// <summary>
        /// 방 사용료 계산
        /// </summary>
        int CalculateRoomPrice(string roomId);
        
        /// <summary>
        /// 방 명성도 계산
        /// </summary>
        int CalculateRoomReputation(string roomId);
        
        /// <summary>
        /// 특정 위치가 방 안에 있는지 확인
        /// </summary>
        bool IsPositionInRoom(Vector3 position, string roomId);
    }

    /// <summary>
    /// 방 할당 정보
    /// </summary>
    public class RoomAssignment
    {
        public string RoomId { get; set; }
        public Vector3 RoomPosition { get; set; }
        public Bounds RoomBounds { get; set; }
        public int Price { get; set; }
        public int Reputation { get; set; }
        public bool IsSunbedRoom { get; set; }
    }

    /// <summary>
    /// RoomManager를 IRoomProvider로 감싸는 어댑터
    /// </summary>
    public class RoomManagerAdapter : IRoomProvider
    {
        private RoomManager roomManager;
        private Dictionary<string, string> aiRoomAssignments = new Dictionary<string, string>();

        public RoomManagerAdapter(RoomManager manager)
        {
            roomManager = manager;
        }

        public bool HasAvailableRooms()
        {
            if (roomManager == null || roomManager.allRooms == null)
                return false;

            foreach (var room in roomManager.allRooms)
            {
                if (room != null && !room.IsRoomUsed)
                    return true;
            }
            return false;
        }

        public bool TryAssignRoom(string aiId, out RoomAssignment assignment)
        {
            assignment = null;

            if (roomManager == null || roomManager.allRooms == null)
                return false;

            // 사용 가능한 방 찾기
            RoomContents availableRoom = null;
            foreach (var room in roomManager.allRooms)
            {
                if (room != null && !room.IsRoomUsed)
                {
                    availableRoom = room;
                    break;
                }
            }

            if (availableRoom == null)
                return false;

            // 방 할당
            assignment = new RoomAssignment
            {
                RoomId = availableRoom.roomID,
                RoomPosition = availableRoom.transform.position,
                RoomBounds = availableRoom.roomBounds,
                Price = availableRoom.TotalRoomPrice,
                Reputation = availableRoom.TotalRoomReputation,
                IsSunbedRoom = availableRoom.isSunbedRoom
            };

            // 할당 기록
            aiRoomAssignments[aiId] = availableRoom.roomID;
            
            return true;
        }

        public void ReleaseRoom(string aiId, string roomId)
        {
            if (aiRoomAssignments.ContainsKey(aiId))
            {
                aiRoomAssignments.Remove(aiId);
            }

            // RoomManager에서 방 해제 처리는 기존 로직 사용
            // 실제 구현에서는 RoomContents의 사용 상태를 변경
        }

        public int CalculateRoomPrice(string roomId)
        {
            var room = FindRoomById(roomId);
            return room?.TotalRoomPrice ?? 0;
        }

        public int CalculateRoomReputation(string roomId)
        {
            var room = FindRoomById(roomId);
            return room?.TotalRoomReputation ?? 0;
        }

        public bool IsPositionInRoom(Vector3 position, string roomId)
        {
            var room = FindRoomById(roomId);
            return room?.roomBounds.Contains(position) ?? false;
        }

        private RoomContents FindRoomById(string roomId)
        {
            if (roomManager?.allRooms == null) return null;

            foreach (var room in roomManager.allRooms)
            {
                if (room?.roomID == roomId)
                    return room;
            }
            return null;
        }
    }
}