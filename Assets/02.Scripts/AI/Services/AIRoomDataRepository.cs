using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JY.AI
{
    /// <summary>
    /// AI 시스템을 위한 룸 데이터 저장소 (Repository 패턴)
    /// 모든 AI가 공유하는 룸 정보를 중앙화하여 관리
    /// </summary>
    public static class AIRoomDataRepository
    {
        // 정적 룸 리스트 (모든 AI가 공유)
        private static List<AIRoomData> roomList = new List<AIRoomData>();
        private static readonly object lockObject = new object();

        // 이벤트
        public static event System.Action<List<AIRoomData>> OnRoomListUpdated;
        public static event System.Action<AIRoomData> OnRoomAdded;
        public static event System.Action<string> OnRoomRemoved;

        #region 속성

        /// <summary>
        /// 전체 룸 개수
        /// </summary>
        public static int TotalRoomCount
        {
            get
            {
                lock (lockObject)
                {
                    return roomList.Count;
                }
            }
        }

        /// <summary>
        /// 사용 가능한 룸 개수
        /// </summary>
        public static int AvailableRoomCount
        {
            get
            {
                lock (lockObject)
                {
                    return roomList.Count(r => !r.isOccupied);
                }
            }
        }

        /// <summary>
        /// 사용 중인 룸 개수
        /// </summary>
        public static int OccupiedRoomCount
        {
            get
            {
                lock (lockObject)
                {
                    return roomList.Count(r => r.isOccupied);
                }
            }
        }

        #endregion

        #region 룸 데이터 관리

        /// <summary>
        /// 룸 리스트 전체 업데이트 (GameObject 배열로부터)
        /// </summary>
        public static void UpdateRoomList(GameObject[] newRooms)
        {
            if (newRooms == null) return;

            lock (lockObject)
            {
                HashSet<string> processedRoomIds = new HashSet<string>();
                List<AIRoomData> updatedRoomList = new List<AIRoomData>();

                foreach (GameObject room in newRooms)
                {
                    if (room != null)
                    {
                        AIRoomData newRoom = new AIRoomData(room);
                        if (!processedRoomIds.Contains(newRoom.roomId))
                        {
                            processedRoomIds.Add(newRoom.roomId);
                            var existingRoom = roomList.FirstOrDefault(r => r.roomId == newRoom.roomId);
                            if (existingRoom != null)
                            {
                                newRoom.isOccupied = existingRoom.isOccupied;
                            }
                            updatedRoomList.Add(newRoom);
                        }
                    }
                }

                var oldCount = roomList.Count;
                roomList = updatedRoomList;
                
                Debug.Log($"[AIRoomDataRepository] 룸 리스트 업데이트: {oldCount} -> {roomList.Count}개");
                OnRoomListUpdated?.Invoke(roomList.ToList());
            }
        }

        /// <summary>
        /// Room.RoomInfo 리스트로 업데이트 (Room 시스템과 연동)
        /// </summary>
        public static void UpdateRoomListFromRoomInfo(List<JY.RoomInfo> newRooms)
        {
            if (newRooms == null) return;
            
            lock (lockObject)
            {
                var oldCount = roomList.Count;
                roomList.Clear();
                
                // Room.RoomInfo를 AIRoomData로 변환
                foreach (var roomInfo in newRooms)
                {
                    if (roomInfo.gameObject != null)
                    {
                        roomList.Add(new AIRoomData(roomInfo.gameObject));
                    }
                }
                
                Debug.Log($"[AIRoomDataRepository] Room.RoomInfo로 {oldCount} -> {roomList.Count}개 방 업데이트");
                OnRoomListUpdated?.Invoke(roomList.ToList());
            }
        }

        /// <summary>
        /// 단일 룸 추가
        /// </summary>
        public static void AddRoom(JY.RoomInfo room)
        {
            if (room?.gameObject == null) return;

            lock (lockObject)
            {
                if (!roomList.Any(r => r.roomId == room.roomId))
                {
                    var newRoom = new AIRoomData(room.gameObject);
                    roomList.Add(newRoom);
                    
                    Debug.Log($"[AIRoomDataRepository] 방 추가됨 - {room.roomId}");
                    OnRoomAdded?.Invoke(newRoom);
                    OnRoomListUpdated?.Invoke(roomList.ToList());
                }
            }
        }

        /// <summary>
        /// 단일 룸 제거
        /// </summary>
        public static void RemoveRoom(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return;

            lock (lockObject)
            {
                var existingRoom = roomList.FirstOrDefault(r => r.roomId == roomId);
                if (existingRoom != null)
                {
                    roomList.Remove(existingRoom);
                    
                    Debug.Log($"[AIRoomDataRepository] 방 제거됨 - {roomId}");
                    OnRoomRemoved?.Invoke(roomId);
                    OnRoomListUpdated?.Invoke(roomList.ToList());
                }
            }
        }

        #endregion

        #region 룸 조회

        /// <summary>
        /// 전체 룸 리스트 조회 (복사본 반환)
        /// </summary>
        public static List<AIRoomData> GetAllRooms()
        {
            lock (lockObject)
            {
                return roomList.ToList();
            }
        }

        /// <summary>
        /// 사용 가능한 룸들 조회
        /// </summary>
        public static List<AIRoomData> GetAvailableRooms()
        {
            lock (lockObject)
            {
                return roomList.Where(r => !r.isOccupied).ToList();
            }
        }

        /// <summary>
        /// 특정 ID의 룸 조회
        /// </summary>
        public static AIRoomData GetRoomById(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return null;

            lock (lockObject)
            {
                return roomList.FirstOrDefault(r => r.roomId == roomId);
            }
        }

        /// <summary>
        /// 인덱스로 룸 조회
        /// </summary>
        public static AIRoomData GetRoomByIndex(int index)
        {
            lock (lockObject)
            {
                return (index >= 0 && index < roomList.Count) ? roomList[index] : null;
            }
        }

        #endregion

        #region 점유 상태 관리

        /// <summary>
        /// 룸 점유 상태 설정
        /// </summary>
        public static bool SetRoomOccupied(string roomId, bool occupied)
        {
            if (string.IsNullOrEmpty(roomId)) return false;

            lock (lockObject)
            {
                var room = roomList.FirstOrDefault(r => r.roomId == roomId);
                if (room != null)
                {
                    room.isOccupied = occupied;
                    Debug.Log($"[AIRoomDataRepository] 룸 {roomId} 점유 상태: {occupied}");
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// 가장 가까운 사용 가능한 룸 찾기
        /// </summary>
        public static AIRoomData FindNearestAvailableRoom(Vector3 position)
        {
            lock (lockObject)
            {
                var availableRooms = roomList.Where(r => !r.isOccupied).ToList();
                
                if (availableRooms.Count == 0)
                {
                    return null;
                }

                return availableRooms
                    .OrderBy(r => Vector3.Distance(position, r.transform.position))
                    .FirstOrDefault();
            }
        }

        #endregion

        #region 메모리 관리

        /// <summary>
        /// 전체 데이터 정리
        /// </summary>
        public static void ClearAllStaticRoomData()
        {
            lock (lockObject)
            {
                var count = roomList.Count;
                roomList.Clear();
                
                // 이벤트 구독자들도 정리
                OnRoomListUpdated = null;
                OnRoomAdded = null;
                OnRoomRemoved = null;
                
                Debug.Log($"[AIRoomDataRepository] 정적 룸 데이터 {count}개 정리 완료");
            }
        }

        /// <summary>
        /// 메모리 사용량 체크 (디버그용)
        /// </summary>
        public static void CheckStaticMemoryUsage()
        {
            lock (lockObject)
            {
                var totalMemory = GC.GetTotalMemory(false);
                Debug.Log($"[AIRoomDataRepository] 룸 데이터: {roomList.Count}개, 총 메모리: {totalMemory / 1024 / 1024:F2}MB");
            }
        }

        #endregion

        #region 디버그

        /// <summary>
        /// 룸 상태 정보 출력
        /// </summary>
        public static void DebugPrintRoomStatus()
        {
            lock (lockObject)
            {
                Debug.Log($"=== AIRoomDataRepository 상태 ===");
                Debug.Log($"전체 룸: {TotalRoomCount}개");
                Debug.Log($"사용 가능: {AvailableRoomCount}개");
                Debug.Log($"사용 중: {OccupiedRoomCount}개");
                
                foreach (var room in roomList)
                {
                    Debug.Log($"- {room.roomId}: {(room.isOccupied ? "사용중" : "사용가능")}");
                }
            }
        }

        #endregion
    }
} 