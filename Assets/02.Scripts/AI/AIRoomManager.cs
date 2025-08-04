using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using JY.RoomDetection;

namespace JY
{
    /// <summary>
    /// AI의 방 관리를 담당하는 클래스
    /// 방 검색, 할당, 해제 등의 기능을 처리
    /// </summary>
    public class AIRoomManager
    {
        [System.Serializable]
        private class RoomInfo
        {
            public Transform transform;
            public bool isOccupied;
            public float size;
            public GameObject gameObject;
            public string roomId;
            public Bounds bounds;

            public RoomInfo(GameObject roomObj)
            {
                gameObject = roomObj;
                transform = roomObj.transform;
                isOccupied = false;

                var collider = roomObj.GetComponent<Collider>();
                size = collider != null ? collider.bounds.size.magnitude * 0.3f : 2f;
                var roomContents = roomObj.GetComponent<RoomContents>();
                bounds = roomContents != null ? roomContents.roomBounds : 
                        (collider != null ? collider.bounds : new Bounds(transform.position, Vector3.one * 2f));

                if (collider == null)
                {
                    Debug.LogWarning($"룸 {roomObj.name}에 Collider가 없습니다. 기본 크기(2) 사용.");
                }

                Vector3 pos = roomObj.transform.position;
                roomId = $"Room_{pos.x:F0}_{pos.z:F0}";
            }
        }

        private static readonly object lockObject = new object();
        private static List<RoomInfo> roomList = new List<RoomInfo>();
        
        public int CurrentRoomIndex { get; private set; } = -1;
        public bool HasAssignedRoom => CurrentRoomIndex != -1;
        public int RoomCount => roomList.Count;

        // 이벤트
        public delegate void RoomsUpdatedHandler(GameObject[] rooms);
        private static event RoomsUpdatedHandler OnRoomsUpdated;

        /// <summary>
        /// 방 리스트 초기화
        /// </summary>
        public void InitializeRooms(string ownerName)
        {
            lock (lockObject)
            {
                if (roomList.Count == 0)
                {
                    roomList.Clear();
                    Debug.Log($"AI {ownerName}: 룸 초기화 시작");

                                var roomDetectors = GameObject.FindObjectsByType<JY.RoomDetection.RoomDetectorSimplified>(FindObjectsSortMode.None);
            if (roomDetectors.Length > 0)
            {
                foreach (var detector in roomDetectors)
                        {
                            detector.ScanForRooms();
                            detector.OnRoomsUpdated += rooms =>
                            {
                                if (rooms != null && rooms.Length > 0)
                                {
                                    UpdateRoomList(rooms);
                                }
                            };
                        }
                        Debug.Log($"AI {ownerName}: RoomDetector로 룸 감지 시작.");
                    }
                    else
                    {
                        GameObject[] taggedRooms = GameObject.FindGameObjectsWithTag("Room");
                        foreach (GameObject room in taggedRooms)
                        {
                            if (!roomList.Any(r => r.gameObject == room))
                            {
                                roomList.Add(new RoomInfo(room));
                            }
                        }
                        Debug.Log($"AI {ownerName}: 태그로 {roomList.Count}개 룸 발견.");
                    }

                    if (roomList.Count == 0)
                    {
                        Debug.LogWarning($"AI {ownerName}: 룸을 찾을 수 없습니다!");
                    }
                    else
                    {
                        Debug.Log($"AI {ownerName}: {roomList.Count}개 룸 초기화 완료.");
                    }

                    if (OnRoomsUpdated == null)
                    {
                        OnRoomsUpdated += UpdateRoomList;
                    }
                }
            }
        }

        /// <summary>
        /// 사용 가능한 방 할당 시도
        /// </summary>
        public bool TryAssignRoom(string ownerName)
        {
            lock (lockObject)
            {
                var availableRooms = roomList.Select((room, index) => new { room, index })
                                           .Where(r => !r.room.isOccupied)
                                           .Select(r => r.index)
                                           .ToList();

                if (availableRooms.Count == 0)
                {
                    Debug.Log($"AI {ownerName}: 사용 가능한 룸 없음.");
                    return false;
                }

                int selectedRoomIndex = availableRooms[UnityEngine.Random.Range(0, availableRooms.Count)];
                if (!roomList[selectedRoomIndex].isOccupied)
                {
                    roomList[selectedRoomIndex].isOccupied = true;
                    CurrentRoomIndex = selectedRoomIndex;
                    Debug.Log($"AI {ownerName}: 룸 {selectedRoomIndex + 1}번 할당됨.");
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// 현재 할당된 방 해제
        /// </summary>
        public void ReleaseCurrentRoom(string ownerName)
        {
            lock (lockObject)
            {
                if (CurrentRoomIndex != -1 && CurrentRoomIndex < roomList.Count)
                {
                    roomList[CurrentRoomIndex].isOccupied = false;
                    Debug.Log($"AI {ownerName}: 룸 {CurrentRoomIndex + 1}번 해제됨.");
                    CurrentRoomIndex = -1;
                }
            }
        }

        /// <summary>
        /// 현재 방의 위치 반환
        /// </summary>
        public Vector3 GetCurrentRoomPosition()
        {
            if (CurrentRoomIndex != -1 && CurrentRoomIndex < roomList.Count)
            {
                return roomList[CurrentRoomIndex].transform.position;
            }
            return Vector3.zero;
        }

        /// <summary>
        /// 현재 방의 범위 반환
        /// </summary>
        public Bounds GetCurrentRoomBounds()
        {
            if (CurrentRoomIndex != -1 && CurrentRoomIndex < roomList.Count)
            {
                return roomList[CurrentRoomIndex].bounds;
            }
            return new Bounds();
        }

        /// <summary>
        /// 현재 방이 플레이어 위치를 포함하는지 확인
        /// </summary>
        public bool IsPositionInCurrentRoom(Vector3 position)
        {
            if (CurrentRoomIndex != -1 && CurrentRoomIndex < roomList.Count)
            {
                return roomList[CurrentRoomIndex].bounds.Contains(position);
            }
            return false;
        }

        /// <summary>
        /// 방 리스트 업데이트 (정적 메서드)
        /// </summary>
        public static void UpdateRoomList(GameObject[] newRooms)
        {
            if (newRooms == null || newRooms.Length == 0) return;

            lock (lockObject)
            {
                HashSet<string> processedRoomIds = new HashSet<string>();
                List<RoomInfo> updatedRoomList = new List<RoomInfo>();

                foreach (GameObject room in newRooms)
                {
                    if (room != null)
                    {
                        RoomInfo newRoom = new RoomInfo(room);
                        if (!processedRoomIds.Contains(newRoom.roomId))
                        {
                            processedRoomIds.Add(newRoom.roomId);
                            var existingRoom = roomList.FirstOrDefault(r => r.roomId == newRoom.roomId);
                            if (existingRoom != null)
                            {
                                newRoom.isOccupied = existingRoom.isOccupied;
                                updatedRoomList.Add(newRoom);
                            }
                            else
                            {
                                updatedRoomList.Add(newRoom);
                            }
                        }
                    }
                }

                if (updatedRoomList.Count > 0)
                {
                    roomList = updatedRoomList;
                    Debug.Log($"룸 리스트 업데이트 완료. 총 룸 수: {roomList.Count}");
                }
            }
        }

        /// <summary>
        /// 방 업데이트 알림 (정적 메서드)
        /// </summary>
        public static void NotifyRoomsUpdated(GameObject[] rooms)
        {
            OnRoomsUpdated?.Invoke(rooms);
        }

        /// <summary>
        /// 정적 초기화 (Domain Reload 대응)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitializeStatics()
        {
            roomList.Clear();
            OnRoomsUpdated = null;
        }
    }
}