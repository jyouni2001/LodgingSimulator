using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace JY
{
    /// <summary>
    /// AI 룸 서비스 클래스
    /// 룸 관리와 룸 사용 로직을 담당
    /// </summary>
    public class AIRoomService : MonoBehaviour
    {
        [Header("룸 설정")]
        [SerializeField] private float roomUseMinTime = 30f;
        [SerializeField] private float roomUseMaxTime = 120f;
        
        // AI 전용 룸 데이터 클래스 (Room.RoomInfo와 구분)
        [System.Serializable]
        public class AIRoomData
        {
            public Transform transform;
            public bool isOccupied;
            public float size;
            public GameObject gameObject;
            public string roomId;
            public Bounds bounds;

            public AIRoomData(GameObject roomObj)
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
        
        // Repository 사용으로 정적 리스트 제거됨
        
        // 현재 AI의 룸 정보
        private int currentRoomIndex = -1;
        private Coroutine roomUseCoroutine;
        private Coroutine roomWanderingCoroutine;
        
        // 의존성
        private AIMovement aiMovement;
        private AIStateManager stateManager;
        private string agentId;
        
        // 이벤트
        public System.Action<AIRoomData> OnRoomEntered;
        public System.Action<AIRoomData> OnRoomExited;
        public System.Action OnRoomUsageCompleted;
        
        // 속성
        public bool HasRoom => currentRoomIndex >= 0 && currentRoomIndex < AIRoomDataRepository.TotalRoomCount;
        public AIRoomData CurrentRoom => HasRoom ? AIRoomDataRepository.GetRoomByIndex(currentRoomIndex) : null;
        public int CurrentRoomIndex => currentRoomIndex;
        
        /// <summary>
        /// 초기화
        /// </summary>
        public void Initialize(string id, AIMovement movement, AIStateManager state)
        {
            agentId = id;
            aiMovement = movement;
            stateManager = state;
            
            InitializeRoomList();
        }
        
        /// <summary>
        /// 룸 리스트 초기화 (중앙 이벤트 시스템 연동)
        /// </summary>
        private void InitializeRoomList()
        {
            lock (lockObject)
            {
                if (roomList.Count > 0) return; // 이미 초기화됨
                
                // 중앙 이벤트 시스템에 구독
                var eventManager = RoomEventManager.Instance;
                if (eventManager != null)
                {
                    eventManager.OnRoomsDetected += OnRoomsDetectedHandler;
                    eventManager.OnRoomCreated += OnRoomCreatedHandler;
                    eventManager.OnRoomDestroyed += OnRoomDestroyedHandler;
                    
                    Debug.Log($"AIRoomService: 중앙 이벤트 시스템에 구독 완료");
                }
                
                // 기존 방식도 호환성을 위해 유지 (신규 RoomDetectorNew 우선)
                var newRoomDetectors = GameObject.FindObjectsByType<RoomDetectorNew>(FindObjectsSortMode.None);
                if (newRoomDetectors.Length > 0)
                {
                    foreach (var detector in newRoomDetectors)
                    {
                        var detectedRooms = ((IRoomDetector)detector).GetDetectedRooms();
                        UpdateRoomListFromRoomInfo(detectedRooms);
                        
                        // 기존 이벤트도 구독
                        detector.OnRoomsUpdated += OnRoomsUpdatedLegacy;
                    }
                    Debug.Log($"AIRoomService: RoomDetectorNew로 룸 감지 ({roomList.Count}개)");
                }
                else
                {
                    // 레거시 시스템 지원
                    var oldRoomDetectors = GameObject.FindGameObjectsWithTag("Room");
                    foreach (GameObject room in oldRoomDetectors)
                    {
                        if (!roomList.Any(r => r.gameObject == room))
                        {
                            roomList.Add(new AIRoomData(room));
                        }
                    }
                    Debug.Log($"AIRoomService: 태그로 {roomList.Count}개 룸 발견 (레거시)");
                }
                
                Debug.Log($"AIRoomService: {roomList.Count}개 룸 초기화 완료");
            }
        }
        
        /// <summary>
        /// 룸 리스트 업데이트
        /// </summary>
        public static void UpdateRoomList(GameObject[] newRooms)
        {
            if (newRooms == null || newRooms.Length == 0) return;

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

                if (updatedRoomList.Count > 0)
                {
                    roomList = updatedRoomList;
                    Debug.Log($"룸 리스트 업데이트 완료. 총 룸 수: {roomList.Count}");
                }
            }
        }
        
        /// <summary>
        /// 사용 가능한 룸 찾기
        /// </summary>
        public bool TryFindAvailableRoom()
        {
            lock (lockObject)
            {
                var availableRooms = roomList.Where(r => !r.isOccupied).ToList();
                if (availableRooms.Count == 0)
                {
                    Debug.Log($"AI {agentId}: 사용 가능한 룸이 없습니다.");
                    return false;
                }

                // 랜덤하게 룸 선택
                var selectedRoom = availableRooms[Random.Range(0, availableRooms.Count)];
                int roomIndex = roomList.IndexOf(selectedRoom);
                
                if (roomIndex >= 0)
                {
                    roomList[roomIndex].isOccupied = true;
                    currentRoomIndex = roomIndex;
                    
                    Debug.Log($"AI {agentId}: 룸 {roomIndex + 1}번 배정됨");
                    AIEvents.TriggerRoomAssigned(agentId, roomIndex);
                    
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 룸으로 이동
        /// </summary>
        public bool MoveToAssignedRoom()
        {
            if (!HasRoom) return false;
            
            var room = CurrentRoom;
            Vector3 roomCenter = room.bounds.center;
            
            if (aiMovement.MoveTo(roomCenter))
            {
                stateManager.ChangeState(AIState.MovingToRoom, $"룸 {currentRoomIndex + 1}번으로 이동");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 룸 사용 시작
        /// </summary>
        public void StartUsingRoom()
        {
            if (!HasRoom) return;
            
            var room = CurrentRoom;
            stateManager.ChangeState(AIState.UsingRoom, $"룸 {currentRoomIndex + 1}번 사용 중");
            
            OnRoomEntered?.Invoke(room);
            AIEvents.TriggerRoomUsageStarted(agentId);
            
            // 룸 사용 코루틴 시작
            roomUseCoroutine = StartCoroutine(UseRoomCoroutine());
        }
        
        /// <summary>
        /// 룸 사용 코루틴
        /// </summary>
        private IEnumerator UseRoomCoroutine()
        {
            float useTime = Random.Range(roomUseMinTime, roomUseMaxTime);
            float elapsedTime = 0f;
            
            // 룸 내부 배회 시작
            StartRoomWandering();
            
            while (elapsedTime < useTime)
            {
                yield return new WaitForSeconds(1f);
                elapsedTime += 1f;
            }
            
            // 룸 사용 완료
            CompleteRoomUsage();
        }
        
        /// <summary>
        /// 룸 내부 배회
        /// </summary>
        private void StartRoomWandering()
        {
            if (!HasRoom) return;
            
            roomWanderingCoroutine = StartCoroutine(RoomWanderingCoroutine());
        }
        
        /// <summary>
        /// 룸 배회 코루틴
        /// </summary>
        private IEnumerator RoomWanderingCoroutine()
        {
            while (stateManager.IsUsingRoom())
            {
                if (HasRoom)
                {
                    var room = CurrentRoom;
                    aiMovement.WanderAround(room.bounds.center, room.size);
                    stateManager.ChangeState(AIState.UseWandering, $"룸 {currentRoomIndex + 1}번에서 배회");
                }
                
                yield return new WaitForSeconds(Random.Range(10f, 20f));
            }
        }
        
        /// <summary>
        /// 룸 사용 완료
        /// </summary>
        public void CompleteRoomUsage()
        {
            if (!HasRoom) return;
            
            var room = CurrentRoom;
            
            // 코루틴 정리
            if (roomUseCoroutine != null)
            {
                StopCoroutine(roomUseCoroutine);
                roomUseCoroutine = null;
            }
            
            if (roomWanderingCoroutine != null)
            {
                StopCoroutine(roomWanderingCoroutine);
                roomWanderingCoroutine = null;
            }
            
            OnRoomExited?.Invoke(room);
            OnRoomUsageCompleted?.Invoke();
            AIEvents.TriggerRoomUsageCompleted(agentId);
            
            Debug.Log($"AI {agentId}: 룸 {currentRoomIndex + 1}번 사용 완료");
        }
        
        /// <summary>
        /// 룸 반환
        /// </summary>
        public void ReleaseRoom()
        {
            if (!HasRoom) return;
            
            lock (lockObject)
            {
                if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
                {
                    roomList[currentRoomIndex].isOccupied = false;
                    AIEvents.TriggerRoomReleased(agentId, currentRoomIndex);
                    Debug.Log($"AI {agentId}: 룸 {currentRoomIndex + 1}번 반환");
                }
                currentRoomIndex = -1;
            }
        }
        
        /// <summary>
        /// 정리
        /// </summary>
        public void Cleanup()
        {
            // 코루틴 정리
            if (roomUseCoroutine != null)
            {
                StopCoroutine(roomUseCoroutine);
                roomUseCoroutine = null;
            }
            
            if (roomWanderingCoroutine != null)
            {
                StopCoroutine(roomWanderingCoroutine);
                roomWanderingCoroutine = null;
            }
            
            // 룸 반환
            ReleaseRoom();
        }
        
        /// <summary>
        /// 룸 정보 반환
        /// </summary>
        public static int GetTotalRoomCount()
        {
            return roomList.Count;
        }
        
        public static int GetAvailableRoomCount()
        {
            lock (lockObject)
            {
                return roomList.Count(r => !r.isOccupied);
            }
        }
        
        #region 이벤트 핸들러 (중앙 시스템 연동)
        
        /// <summary>
        /// 방들이 감지되었을 때 호출 (중앙 이벤트 시스템)
        /// </summary>
        private static void OnRoomsDetectedHandler(List<JY.RoomInfo> rooms)
        {
            UpdateRoomListFromRoomInfo(rooms);
            Debug.Log($"AIRoomService: 중앙 이벤트로 {rooms?.Count ?? 0}개 방 업데이트");
        }
        
        /// <summary>
        /// 방이 생성되었을 때 호출 (중앙 이벤트 시스템)
        /// </summary>
        private static void OnRoomCreatedHandler(JY.RoomInfo room)
        {
            lock (lockObject)
            {
                if (room != null && room.gameObject != null && !roomList.Any(r => r.roomId == room.roomId))
                {
                    roomList.Add(new AIRoomData(room.gameObject));
                    Debug.Log($"AIRoomService: 방 추가됨 - {room.roomId}");
                }
            }
        }
        
        /// <summary>
        /// 방이 제거되었을 때 호출 (중앙 이벤트 시스템)
        /// </summary>
        private static void OnRoomDestroyedHandler(JY.RoomInfo room)
        {
            lock (lockObject)
            {
                if (room != null)
                {
                    var existingRoom = roomList.FirstOrDefault(r => r.roomId == room.roomId);
                    if (existingRoom != null)
                    {
                        roomList.Remove(existingRoom);
                        Debug.Log($"AIRoomService: 방 제거됨 - {room.roomId}");
                    }
                }
            }
        }
        
        /// <summary>
        /// 레거시 이벤트 핸들러 (기존 호환성)
        /// </summary>
        private static void OnRoomsUpdatedLegacy(GameObject[] rooms)
        {
            UpdateRoomList(rooms);
        }
        
        /// <summary>
        /// Room.RoomInfo 리스트로 업데이트 (Room 시스템과 연동)
        /// </summary>
        private static void UpdateRoomListFromRoomInfo(List<JY.RoomInfo> newRooms)
        {
            if (newRooms == null) return;
            
            lock (lockObject)
            {
                roomList.Clear();
                
                // Room.RoomInfo를 AIRoomData로 변환
                foreach (var roomInfo in newRooms)
                {
                    if (roomInfo.gameObject != null)
                    {
                        roomList.Add(new AIRoomData(roomInfo.gameObject));
                    }
                }
                
                Debug.Log($"AIRoomService: Room.RoomInfo로 {roomList.Count}개 방 업데이트");
            }
        }
        
        #endregion
        
        #region 메모리 관리
        
        /// <summary>
        /// 개별 AI 정리 작업 (메모리 누수 방지)
        /// </summary>
        private void OnDestroy()
        {
            try
            {
                // 현재 방 정리
                if (HasRoom)
                {
                    ReleaseRoom();
                }
                
                // 코루틴 정리
                if (roomUseCoroutine != null)
                {
                    StopCoroutine(roomUseCoroutine);
                    roomUseCoroutine = null;
                }
                
                if (roomWanderingCoroutine != null)
                {
                    StopCoroutine(roomWanderingCoroutine);
                    roomWanderingCoroutine = null;
                }
                
                // 이벤트 해제
                OnRoomEntered = null;
                OnRoomExited = null;
                OnRoomUsageCompleted = null;
                
                // 중앙 이벤트 시스템 구독 해제
                var eventManager = RoomEventManager.Instance;
                if (eventManager != null)
                {
                    eventManager.OnRoomsDetected -= OnRoomsDetectedHandler;
                    eventManager.OnRoomCreated -= OnRoomCreatedHandler;
                    eventManager.OnRoomDestroyed -= OnRoomDestroyedHandler;
                }
                
                // 참조 정리
                aiMovement = null;
                stateManager = null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"AIRoomService 정리 중 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 모든 AI의 정적 룸 데이터 정리 (Scene 전환 시 사용)
        /// </summary>
        public static void ClearAllStaticRoomData()
        {
            lock (lockObject)
            {
                try
                {
                    roomList?.Clear();
                    Debug.Log($"[AIRoomService] 정적 룸 데이터 정리 완료");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"정적 룸 데이터 정리 중 오류: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 메모리 상태 체크 (디버그용)
        /// </summary>
        public static void CheckStaticMemoryUsage()
        {
            lock (lockObject)
            {
                var info = $"=== AIRoomService 정적 메모리 상태 ===\n" +
                          $"룸 리스트 개수: {roomList?.Count ?? 0}\n";
                
                Debug.Log(info);
            }
        }
        
        #endregion
    }
} 