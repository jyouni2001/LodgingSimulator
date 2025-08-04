using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using ZLinq;

namespace JY.RoomDetection
{
    /// <summary>
    /// 단순화된 방 감지 시스템
    /// 기존의 복잡한 RoomDetector를 대체하는 최적화된 버전
    /// </summary>
    public class RoomDetectorSimplified : MonoBehaviour
    {
        [Header("기본 설정")]
        [SerializeField] private bool autoScanOnStart = true;
        [SerializeField] private float scanInterval = 5f;
        
        [Header("감지 설정")]
        [SerializeField] private RoomDetectionCore.DetectionSettings detectionSettings = new RoomDetectionCore.DetectionSettings();
        
        [Header("그리드 설정")]
        [SerializeField] private RoomGridManager.GridSettings gridSettings = new RoomGridManager.GridSettings();
        
        [Header("선베드 설정")]
        [SerializeField] private SunbedRoomProcessor.SunbedSettings sunbedSettings = new SunbedRoomProcessor.SunbedSettings();
        
        [Header("디버그 설정")]
        [SerializeField] private AIDebugLogger.DebugSettings debugSettings = new AIDebugLogger.DebugSettings();
        
        [Header("층별 설정")]
        [SerializeField] private bool scanAllFloors = true;
        [SerializeField] private int currentScanFloor = 1;
        [SerializeField] private int maxFloors = 5;

        // 감지된 방 목록
        private List<RoomInfo> detectedRooms = new List<RoomInfo>();
        
        // 핵심 컴포넌트들
        private RoomDetectionCore detectionCore;
        private RoomGridManager gridManager;
        private SunbedRoomProcessor sunbedProcessor;
        
        // 자동 스캔
        private Coroutine autoScanCoroutine;
        
        // 이벤트
        public System.Action<GameObject[]> OnRoomsUpdated;

        public int DetectedRoomCount => detectedRooms.Count;
        public bool IsInitialized { get; private set; }

        #region Unity 생명주기
        void Start()
        {
            Initialize();
            
            if (autoScanOnStart)
            {
                StartAutoScan();
            }
        }

        void OnDestroy()
        {
            StopAutoScan();
        }
        #endregion

        #region 초기화
        /// <summary>
        /// 시스템 초기화
        /// </summary>
        private void Initialize()
        {
            // 디버그 설정 적용
            AIDebugLogger.UpdateGlobalSettings(debugSettings);
            
            // Unity Grid 찾기
            Grid unityGrid = FindObjectOfType<Grid>();
            
            // 핵심 컴포넌트들 생성
            gridManager = new RoomGridManager(gridSettings, unityGrid);
            detectionCore = new RoomDetectionCore(detectionSettings, gridManager);
            sunbedProcessor = new SunbedRoomProcessor(sunbedSettings, gridManager);
            
            IsInitialized = true;
            
            AIDebugLogger.Log(gameObject.name, "방 감지 시스템 초기화 완료", LogCategory.Room, true);
        }
        #endregion

        #region 방 스캔
        /// <summary>
        /// 방 스캔 실행
        /// </summary>
        public void ScanForRooms()
        {
            if (!IsInitialized)
            {
                AIDebugLogger.LogWarning(gameObject.name, "시스템이 초기화되지 않았습니다");
                return;
            }

            AIDebugLogger.Log(gameObject.name, "방 스캔 시작", LogCategory.Room, true);

            try
            {
                // 그리드 업데이트
                int targetFloor = scanAllFloors ? -1 : currentScanFloor;
                gridManager.UpdateGridFromScene(targetFloor);
                
                // 방 감지
                var newRooms = DetectRoomsFromBeds();
                
                // 선베드 방 추가
                if (sunbedSettings.enableSunbedRooms)
                {
                    var sunbedRooms = sunbedProcessor.FindSunbedRooms(newRooms);
                    newRooms.AddRange(sunbedRooms);
                }
                
                // 방 목록 업데이트
                UpdateDetectedRooms(newRooms);
                
                AIDebugLogger.Log(gameObject.name, $"방 스캔 완료: {detectedRooms.Count}개 방 감지", LogCategory.Room, true);
            }
            catch (System.Exception e)
            {
                AIDebugLogger.LogError(gameObject.name, $"방 스캔 중 오류: {e.Message}", LogCategory.Room);
            }
        }

        /// <summary>
        /// 침대 위치에서 방들을 감지
        /// </summary>
        private List<RoomInfo> DetectRoomsFromBeds()
        {
            var detectedRooms = new List<RoomInfo>();
            var globalVisitedCells = new HashSet<Vector3Int>();
            
            // 모든 침대 위치 가져오기
            var bedPositions = gridManager.GetAllBedPositions();
            
            AIDebugLogger.Log(gameObject.name, $"침대 {bedPositions.Count}개에서 방 감지 시작", LogCategory.Room);
            
            foreach (var bedPos in bedPositions)
            {
                if (globalVisitedCells.Contains(bedPos))
                    continue;
                
                RoomInfo room = detectionCore.DetectRoomFromBed(bedPos, globalVisitedCells);
                if (room != null)
                {
                    detectedRooms.Add(room);
                    AIDebugLogger.LogRoomAction(gameObject.name, $"방 감지 성공", detectedRooms.Count - 1);
                }
            }
            
            return detectedRooms;
        }

        /// <summary>
        /// 감지된 방 목록 업데이트
        /// </summary>
        private void UpdateDetectedRooms(List<RoomInfo> newRooms)
        {
            // 기존 방 오브젝트들 제거
            foreach (var room in detectedRooms)
            {
                if (room.gameObject != null)
                {
                    DestroyImmediate(room.gameObject);
                }
            }
            
            // 새 방 목록으로 교체
            detectedRooms = newRooms;
            
            // 방 게임오브젝트 생성
            foreach (var room in detectedRooms)
            {
                CreateRoomGameObject(room);
            }
            
            // 이벤트 발생
            if (OnRoomsUpdated != null && detectedRooms.Count > 0)
            {
                var roomObjects = detectedRooms.AsValueEnumerable()
                    .Select(r => r.gameObject)
                    .Where(go => go != null)
                    .ToArray();
                
                OnRoomsUpdated.Invoke(roomObjects);
            }
        }

        /// <summary>
        /// 방 게임오브젝트 생성
        /// </summary>
        private void CreateRoomGameObject(RoomInfo roomInfo)
        {
            // 방 루트 오브젝트 생성
            GameObject roomObject = new GameObject($"DetectedRoom_{roomInfo.roomId}");
            roomObject.transform.position = roomInfo.center;
            roomObject.tag = "Room";
            
            // RoomContents 컴포넌트 추가
            var roomContents = roomObject.AddComponent<RoomContents>();
            roomContents.roomID = roomInfo.roomId;
            roomContents.SetRoomBounds(roomInfo.bounds);
            
            // 선베드 방인 경우 특별 설정
            if (roomInfo.isSunbedRoom)
            {
                roomContents.SetAsSunbedRoom(roomInfo.fixedPrice, roomInfo.fixedReputation);
            }
            
            // 생성된 오브젝트를 방 정보에 저장
            roomInfo.gameObject = roomObject;
            
            AIDebugLogger.LogRoomAction(gameObject.name, $"게임오브젝트 생성: {roomInfo.roomId}", -1);
        }
        #endregion

        #region 자동 스캔
        /// <summary>
        /// 자동 스캔 시작
        /// </summary>
        public void StartAutoScan()
        {
            StopAutoScan();
            autoScanCoroutine = StartCoroutine(AutoScanCoroutine());
            AIDebugLogger.Log(gameObject.name, $"자동 스캔 시작 (간격: {scanInterval}초)", LogCategory.Room, true);
        }

        /// <summary>
        /// 자동 스캔 중지
        /// </summary>
        public void StopAutoScan()
        {
            if (autoScanCoroutine != null)
            {
                StopCoroutine(autoScanCoroutine);
                autoScanCoroutine = null;
                AIDebugLogger.Log(gameObject.name, "자동 스캔 중지", LogCategory.Room, true);
            }
        }

        /// <summary>
        /// 자동 스캔 코루틴
        /// </summary>
        private IEnumerator AutoScanCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(scanInterval);
                ScanForRooms();
            }
        }
        #endregion

        #region 공개 메서드
        /// <summary>
        /// 수동 방 스캔 (Inspector 버튼용)
        /// </summary>
        [ContextMenu("수동 방 스캔")]
        public void ManualScanRooms()
        {
            ScanForRooms();
        }

        /// <summary>
        /// 감지된 방 목록 반환
        /// </summary>
        public GameObject[] GetDetectedRooms()
        {
            return detectedRooms.AsValueEnumerable()
                .Select(r => r.gameObject)
                .Where(go => go != null)
                .ToArray();
        }

        /// <summary>
        /// 특정 층만 스캔하도록 설정
        /// </summary>
        public void SetScanFloor(int floorLevel)
        {
            currentScanFloor = Mathf.Clamp(floorLevel, 1, maxFloors);
            scanAllFloors = false;
            AIDebugLogger.Log(gameObject.name, $"스캔 층 설정: {currentScanFloor}층", LogCategory.Room, true);
        }

        /// <summary>
        /// 모든 층 스캔 모드 설정
        /// </summary>
        public void SetScanAllFloors()
        {
            scanAllFloors = true;
            AIDebugLogger.Log(gameObject.name, "모든 층 스캔 모드 활성화", LogCategory.Room, true);
        }

        /// <summary>
        /// 현재 상태 정보 반환
        /// </summary>
        public string GetStatusInfo()
        {
            string scanMode = scanAllFloors ? "전체 층" : $"{currentScanFloor}층";
            return $"감지된 방: {detectedRooms.Count}개, 스캔 모드: {scanMode}, 자동 스캔: {(autoScanCoroutine != null ? "활성" : "비활성")}";
        }
        #endregion
    }
}