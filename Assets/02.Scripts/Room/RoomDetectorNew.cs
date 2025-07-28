using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace JY
{
    /// <summary>
    /// 새로운 간소화된 방 감지 시스템 (인터페이스 기반 의존성 분리)
    /// 컴포지션 패턴을 사용하여 각 전문 컴포넌트들을 조합
    /// </summary>
    public class RoomDetectorNew : MonoBehaviour, IRoomDetector
    {
        [Header("기본 컴포넌트")]
        [Tooltip("건축 시스템 참조")]
        [SerializeField] private PlacementSystem placementSystem;
        
        [Tooltip("그리드 시스템 참조")]
        [SerializeField] private Grid grid;
        
        // 인터페이스 기반 의존성 (느슨한 결합)
        private IPlacementSystem iPlacementSystem;
        private RoomEventManager eventManager;
        
        [Header("스캔 설정")]
        [SerializeField] private RoomScanSettings scanSettings = new RoomScanSettings();
        
        [Header("자동 스캔 설정")]
        [SerializeField] private bool enableAutoScan = true;
        [SerializeField] private bool scanOnStart = true;
        
        [Header("현재 상태")]
        [SerializeField] private int detectedRoomCount = 0;
        [SerializeField] private string currentScanStatus = "초기화 중...";
        [SerializeField] private string lastScanTime = "";
        
        // 컴포넌트들
        private RoomScanner roomScanner;
        private RoomValidator roomValidator;
        private RoomFactory roomFactory;
        private RoomDataManager dataManager;
        
        // 스캔 관련
        private Coroutine autoScanCoroutine;
        private bool isInitialized = false;
        private bool isScanning = false;
        
        // 이벤트 (외부 시스템과의 호환성)
        public System.Action<GameObject[]> OnRoomsUpdated;
        
        // 속성
        public int DetectedRoomCount => detectedRoomCount;
        public string CurrentScanStatus => currentScanStatus;
        public bool IsScanning => isScanning;
        public RoomScanSettings Settings => scanSettings;
        
        #region Unity 생명주기
        
        private void Awake()
        {
            InitializeComponents();
        }
        
        private void Start()
        {
            if (scanOnStart)
            {
                StartCoroutine(InitializeAndScan());
            }
        }
        
        private void OnEnable()
        {
            if (enableAutoScan && autoScanCoroutine == null)
            {
                StartAutoScan();
            }
        }
        
        private void OnDisable()
        {
            StopAutoScan();
        }
        
        private void OnDestroy()
        {
            Cleanup();
        }
        
        #endregion
        
        #region 초기화
        
        /// <summary>
        /// 컴포넌트 초기화 (인터페이스 기반)
        /// </summary>
        private void InitializeComponents()
        {
            try
            {
                // 중앙 이벤트 시스템 초기화
                eventManager = RoomEventManager.Instance;
                
                // Grid 시스템 자동 탐지
                if (grid == null)
                {
                    grid = FindObjectOfType<Grid>();
                }
                
                // PlacementSystem 자동 탐지 및 인터페이스 캐스팅
                if (placementSystem == null)
                {
                    placementSystem = FindObjectOfType<PlacementSystem>();
                }
                
                if (placementSystem != null)
                {
                    iPlacementSystem = placementSystem; // 인터페이스로 사용
                }
                
                if (grid == null)
                {
                    Debug.LogError("RoomDetectorNew: Grid 시스템을 찾을 수 없습니다!");
                    return;
                }
                
                // 컴포넌트들 생성
                roomScanner = new RoomScanner(scanSettings, grid);
                roomValidator = new RoomValidator(scanSettings);
                roomFactory = new RoomFactory(scanSettings);
                dataManager = new RoomDataManager(scanSettings);
                
                // 이벤트 구독 (중앙 이벤트 시스템 + 로컬 이벤트)
                dataManager.OnRoomsUpdated += OnRoomsUpdatedHandler;
                
                // 중앙 이벤트 시스템에 방 감지 완료 이벤트 연결
                if (eventManager != null)
                {
                    eventManager.OnFloorChanged += OnFloorChangedHandler;
                }
                
                isInitialized = true;
                currentScanStatus = "초기화 완료";
                
                // 시스템 초기화 완료 알림
                eventManager?.RegisterSystemInitialized("RoomDetector");
                
                RoomUtilities.DebugLog("RoomDetectorNew 인터페이스 기반 초기화 완료", true, scanSettings);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"RoomDetectorNew 초기화 중 오류: {ex.Message}");
                currentScanStatus = $"초기화 실패: {ex.Message}";
            }
        }
        
        /// <summary>
        /// 초기화 후 스캔 코루틴
        /// </summary>
        private IEnumerator InitializeAndScan()
        {
            // 초기화 대기
            while (!isInitialized)
            {
                yield return new WaitForSeconds(0.1f);
            }
            
            // 첫 스캔 실행
            yield return new WaitForSeconds(1f); // 시스템 안정화 대기
            ScanForRooms();
        }
        
        #endregion
        
        #region 방 스캔
        
        /// <summary>
        /// 방 스캔 실행
        /// </summary>
        public void ScanForRooms()
        {
            if (!isInitialized)
            {
                RoomUtilities.DebugLog("초기화되지 않은 상태에서 스캔 시도", true, scanSettings);
                return;
            }
            
            if (isScanning)
            {
                RoomUtilities.DebugLog("이미 스캔이 진행 중입니다", false, scanSettings);
                return;
            }
            
            StartCoroutine(ScanCoroutine());
        }
        
        /// <summary>
        /// 스캔 코루틴
        /// </summary>
        private IEnumerator ScanCoroutine()
        {
            isScanning = true;
            currentScanStatus = "스캔 중...";
            
            try
            {
                // 0. 현재 활성 층 자동 감지 및 설정
                UpdateScanFloorFromPlacementSystem();
                yield return null;
                
                // 1. 방 스캔
                var scanResult = roomScanner.ScanForRooms();
                yield return null; // 프레임 양보
                
                // 2. 유효성 검사
                currentScanStatus = "유효성 검사 중...";
                var validationResult = roomValidator.ValidateRooms(scanResult.detectedRooms);
                yield return null;
                
                // 3. 데이터 업데이트
                currentScanStatus = "데이터 업데이트 중...";
                dataManager.UpdateRooms(validationResult);
                yield return null;
                
                // 4. 게임 오브젝트 생성
                currentScanStatus = "방 오브젝트 생성 중...";
                var validRooms = dataManager.GetValidRooms();
                
                // 기존 방 오브젝트 정리
                roomFactory.CleanupExistingRooms();
                yield return null;
                
                // 새 방 오브젝트 생성
                var roomGameObjects = roomFactory.CreateRoomGameObjects(validRooms);
                yield return null;
                
                // 5. 정리 및 이벤트 발생
                if (roomGameObjects.Count > 0)
                {
                    roomFactory.OrganizeRoomsUnderParent(roomGameObjects);
                }
                
                // 상태 업데이트
                detectedRoomCount = validRooms.Count;
                currentScanStatus = $"스캔 완료 - {detectedRoomCount}개 방 발견";
                lastScanTime = System.DateTime.Now.ToString("HH:mm:ss");
                
                // 외부 시스템에 알림
                OnRoomsUpdated?.Invoke(roomGameObjects.ToArray());
                
                // RoomManager에 방 등록 (기존 시스템과의 호환성)
                RegisterRoomsToManager(validRooms);
                
                RoomUtilities.DebugLog($"스캔 완료: {detectedRoomCount}개 유효한 방 발견", true, scanSettings);
                
            }
            catch (System.Exception ex)
            {
                currentScanStatus = $"스캔 오류: {ex.Message}";
                RoomUtilities.DebugLog($"스캔 중 오류 발생: {ex.Message}", true, scanSettings);
            }
            finally
            {
                isScanning = false;
            }
        }
        
        /// <summary>
        /// RoomManager에 방 등록 (기존 시스템과의 호환성)
        /// </summary>
        private void RegisterRoomsToManager(List<RoomInfo> validRooms)
        {
            var roomManager = FindObjectOfType<RoomManager>();
            if (roomManager == null) return;
            
            foreach (var room in validRooms)
            {
                if (room.gameObject != null)
                {
                    try
                    {
                        // RoomDetector.RoomInfo를 RoomManager용으로 변환하여 등록
                        roomManager.RegisterRoomFromDetector(CreateCompatibleRoomInfo(room), room.gameObject);
                    }
                    catch (System.Exception ex)
                    {
                        RoomUtilities.DebugLog($"RoomManager 등록 실패 {room.roomId}: {ex.Message}", false, scanSettings);
                    }
                }
            }
        }
        
        /// <summary>
        /// 기존 시스템과 호환되는 RoomInfo 생성
        /// </summary>
        private object CreateCompatibleRoomInfo(RoomInfo room)
        {
            // 기존 RoomDetector.RoomInfo와 호환되는 객체 생성
            // 리플렉션을 사용하거나 새로운 호환성 클래스 필요
            return room; // 임시로 그대로 반환
        }
        
        #endregion
        
        #region 자동 스캔
        
        /// <summary>
        /// 자동 스캔 시작
        /// </summary>
        public void StartAutoScan()
        {
            if (!enableAutoScan || autoScanCoroutine != null) return;
            
            autoScanCoroutine = StartCoroutine(AutoScanCoroutine());
            RoomUtilities.DebugLog("자동 스캔 시작", true, scanSettings);
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
                RoomUtilities.DebugLog("자동 스캔 중지", true, scanSettings);
            }
        }
        
        /// <summary>
        /// 자동 스캔 코루틴
        /// </summary>
        private IEnumerator AutoScanCoroutine()
        {
            while (enableAutoScan)
            {
                yield return new WaitForSeconds(scanSettings.scanInterval);
                
                if (!isScanning)
                {
                    ScanForRooms();
                }
            }
        }
        
        #endregion
        
        #region 층 감지 및 설정
        
        /// <summary>
        /// PlacementSystem으로부터 현재 활성 층을 감지하여 스캔 설정 업데이트 (인터페이스 기반)
        /// </summary>
        private void UpdateScanFloorFromPlacementSystem()
        {
            if (iPlacementSystem == null) return;
            
            // 현재 활성 층 감지 (인터페이스 사용)
            int activeFloor = iPlacementSystem.GetCurrentActiveFloor();
            
            // 스캔 설정 업데이트
            if (activeFloor != scanSettings.currentScanFloor)
            {
                int oldFloor = scanSettings.currentScanFloor;
                scanSettings.currentScanFloor = activeFloor;
                scanSettings.scanAllFloors = false; // 특정 층만 스캔
                
                RoomUtilities.DebugLog($"활성 층 자동 감지: {oldFloor}층 → {activeFloor}층", true, scanSettings);
                
                // 중앙 이벤트 시스템에 층 변경 알림
                eventManager?.TriggerFloorChanged("RoomDetector", activeFloor);
            }
            
            // FloorLock 상태에 따른 추가 처리 (인터페이스 사용)
            if (iPlacementSystem.GetFloorLock() && iPlacementSystem.currentPurchaseLevel >= 4)
            {
                // 2층 이상이 해금된 경우, 모든 층 스캔 옵션 제공
                if (scanSettings.scanAllFloors)
                {
                    RoomUtilities.DebugLog("다층 건물 모드: 모든 층 스캔 활성화", true, scanSettings);
                }
            }
        }
        
        /// <summary>
        /// 모든 활성 층을 스캔하도록 설정
        /// </summary>
        public void ScanAllActiveFloors()
        {
            if (placementSystem == null) return;
            
            scanSettings.scanAllFloors = true;
            RoomUtilities.DebugLog("모든 활성 층 스캔 모드 활성화", true, scanSettings);
        }
        
        /// <summary>
        /// 특정 층만 스캔하도록 설정 (인터페이스 기반)
        /// </summary>
        /// <param name="floorLevel">스캔할 층 번호</param>
        public void SetScanSpecificFloor(int floorLevel)
        {
            if (iPlacementSystem != null && iPlacementSystem.IsFloorActive(floorLevel))
            {
                scanSettings.currentScanFloor = floorLevel;
                scanSettings.scanAllFloors = false;
                RoomUtilities.DebugLog($"특정 층 스캔 설정: {floorLevel}층", true, scanSettings);
                
                // 중앙 이벤트 시스템에 층 변경 알림
                eventManager?.TriggerFloorChanged("RoomDetector", floorLevel);
            }
            else
            {
                RoomUtilities.DebugLog($"층 {floorLevel}이 활성화되지 않았거나 존재하지 않습니다.", true, scanSettings);
            }
        }
        
        /// <summary>
        /// 층 변경 이벤트 핸들러 (다른 시스템에서 층이 변경될 때)
        /// </summary>
        private void OnFloorChangedHandler(string systemName, int newFloor)
        {
            if (systemName != "RoomDetector") // 자신이 보낸 이벤트가 아닌 경우만 처리
            {
                SetScanFloor(newFloor);
                RoomUtilities.DebugLog($"외부 시스템({systemName})에 의한 층 변경: {newFloor}층", true, scanSettings);
            }
        }
        
        #endregion
        
        #region 외부 인터페이스
        
        /// <summary>
        /// 수동 방 스캔 (기존 호환성)
        /// </summary>
        [ContextMenu("수동 방 스캔 시작")]
        public void ManualScanRooms()
        {
            RoomUtilities.DebugLog("=== 수동 방 스캔 시작 ===", true, scanSettings);
            ScanForRooms();
        }
        
        /// <summary>
        /// 현재 층만 스캔 (디버그용)
        /// </summary>
        [ContextMenu("현재 활성 층만 스캔")]
        public void ScanCurrentFloorOnly()
        {
            UpdateScanFloorFromPlacementSystem();
            ScanForRooms();
        }
        
        /// <summary>
        /// 모든 층 스캔 (디버그용)
        /// </summary>
        [ContextMenu("모든 활성 층 스캔")]
        public void ScanAllFloorsDebug()
        {
            ScanAllActiveFloors();
            ScanForRooms();
        }
        
        /// <summary>
        /// 태그 상태 확인 (기존 호환성)
        /// </summary>
        [ContextMenu("태그 상태 확인")]
        public void CheckTagStatus()
        {
            if (roomScanner != null)
            {
                roomScanner.CheckTagStatus();
            }
        }
        
        /// <summary>
        /// 스캔 층 설정 (기존 호환성)
        /// </summary>
        public void SetScanFloor(int floorLevel)
        {
            scanSettings.currentScanFloor = Mathf.Clamp(floorLevel, 1, scanSettings.maxFloors);
            scanSettings.scanAllFloors = false;
            RoomUtilities.DebugLog($"스캔 층 설정: {scanSettings.currentScanFloor}층", true, scanSettings);
        }
        
        /// <summary>
        /// 모든 층 스캔 모드 설정 (기존 호환성)
        /// </summary>
        public void SetScanAllFloors()
        {
            scanSettings.scanAllFloors = true;
            RoomUtilities.DebugLog("모든 층 스캔 모드 활성화", true, scanSettings);
        }
        
        /// <summary>
        /// 감지된 방들 반환 (기존 호환성)
        /// </summary>
        public GameObject[] GetDetectedRooms()
        {
            if (dataManager == null) return new GameObject[0];
            
            var validRooms = dataManager.GetValidRooms();
            var gameObjects = new List<GameObject>();
            
            foreach (var room in validRooms)
            {
                if (room.gameObject != null)
                {
                    gameObjects.Add(room.gameObject);
                }
            }
            
            return gameObjects.ToArray();
        }
        
        /// <summary>
        /// 현재 스캔 정보 반환 (기존 호환성)
        /// </summary>
        public string GetScanInfo()
        {
            if (dataManager == null) return "초기화되지 않음";
            
            return $"감지된 방: {detectedRoomCount}개, 상태: {currentScanStatus}, 마지막 스캔: {lastScanTime}";
        }
        
        /// <summary>
        /// 방 정보 반환 (IRoomDetector 인터페이스 구현)
        /// </summary>
        List<RoomInfo> IRoomDetector.GetDetectedRooms()
        {
            return dataManager?.GetAllRooms() ?? new List<RoomInfo>();
        }
        
        /// <summary>
        /// 스캔 층 설정 (IRoomDetector 인터페이스 구현)
        /// </summary>
        public void SetScanFloor(int floorLevel)
        {
            SetScanSpecificFloor(floorLevel);
        }
        
        /// <summary>
        /// 모든 층 스캔 설정 (IRoomDetector 인터페이스 구현)
        /// </summary>
        public void SetScanAllFloors(bool scanAll)
        {
            scanSettings.scanAllFloors = scanAll;
            if (scanAll)
            {
                ScanAllActiveFloors();
            }
            
            RoomUtilities.DebugLog($"모든 층 스캔 설정: {scanAll}", true, scanSettings);
        }
        
        #endregion
        
        #region 데이터 조회
        
        /// <summary>
        /// 방 통계 조회
        /// </summary>
        public RoomStatistics GetRoomStatistics()
        {
            return dataManager?.GetRoomStatistics() ?? new RoomStatistics();
        }
        
        /// <summary>
        /// ID로 방 찾기
        /// </summary>
        public RoomInfo GetRoomById(string roomId)
        {
            return dataManager?.GetRoomById(roomId);
        }
        
        /// <summary>
        /// 위치로 방 찾기
        /// </summary>
        public RoomInfo GetRoomAtPosition(Vector3 position)
        {
            return dataManager?.GetRoomAtPosition(position);
        }
        
        /// <summary>
        /// 층별 방 조회
        /// </summary>
        public List<RoomInfo> GetRoomsByFloor(int floorLevel)
        {
            return dataManager?.GetRoomsByFloor(floorLevel) ?? new List<RoomInfo>();
        }
        
        #endregion
        
        #region 이벤트 핸들러
        
        /// <summary>
        /// 방 데이터 업데이트 핸들러 (중앙 이벤트 시스템 연동)
        /// </summary>
        private void OnRoomsUpdatedHandler(List<RoomInfo> rooms)
        {
            detectedRoomCount = rooms?.Count ?? 0;
            
            // 게임 오브젝트 배열 생성
            var gameObjects = new List<GameObject>();
            if (rooms != null)
            {
                foreach (var room in rooms)
                {
                    if (room.gameObject != null)
                    {
                        gameObjects.Add(room.gameObject);
                    }
                }
            }
            
            // 외부 시스템에 알림 (기존 호환성)
            OnRoomsUpdated?.Invoke(gameObjects.ToArray());
            
            // 중앙 이벤트 시스템에 방 감지 완료 알림
            eventManager?.TriggerRoomsDetected(rooms ?? new List<RoomInfo>());
        }
        
        #endregion
        
        #region 설정 및 디버그
        
        /// <summary>
        /// 스캔 설정 업데이트
        /// </summary>
        public void UpdateScanSettings(RoomScanSettings newSettings)
        {
            scanSettings = newSettings;
            
            // 컴포넌트들에 새 설정 적용
            if (isInitialized)
            {
                roomScanner = new RoomScanner(scanSettings, grid);
                roomValidator = new RoomValidator(scanSettings);
                roomFactory = new RoomFactory(scanSettings);
                
                RoomUtilities.DebugLog("스캔 설정 업데이트 완료", true, scanSettings);
            }
        }
        
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("디버그 정보 출력")]
        public void PrintDebugInfo()
        {
            var stats = GetRoomStatistics();
            var info = $"=== RoomDetectorNew 디버그 정보 ===\n" +
                      $"초기화 상태: {isInitialized}\n" +
                      $"스캔 상태: {isScanning}\n" +
                      $"현재 상태: {currentScanStatus}\n" +
                      $"마지막 스캔: {lastScanTime}\n" +
                      $"자동 스캔: {enableAutoScan}\n" +
                      stats.ToString();
            
            Debug.Log(info);
        }
        
        #endregion
        
        #region 정리 & 메모리 관리
        
        /// <summary>
        /// 완전한 리소스 정리 (메모리 누수 방지)
        /// </summary>
        private void Cleanup()
        {
            try
            {
                // 1. 자동 스캔 중지
                StopAutoScan();
                
                // 2. 실행 중인 코루틴 정리
                if (autoScanCoroutine != null)
                {
                    StopCoroutine(autoScanCoroutine);
                    autoScanCoroutine = null;
                }
                
                // 3. 이벤트 구독 해제 및 컴포넌트 정리
                if (dataManager != null)
                {
                    dataManager.OnRoomsUpdated -= OnRoomsUpdatedHandler;
                    dataManager.Dispose();
                }
                
                if (roomFactory != null)
                {
                    roomFactory.Dispose();
                }
                
                // 중앙 이벤트 시스템 해제
                if (eventManager != null)
                {
                    eventManager.OnFloorChanged -= OnFloorChangedHandler;
                    eventManager.UnregisterSystem("RoomDetector");
                }
                
                // 4. 외부 이벤트 정리
                OnRoomsUpdated = null;
                
                // 5. 컴포넌트 참조 정리
                roomScanner = null;
                roomValidator = null;  
                roomFactory = null;
                dataManager = null;
                
                // 6. 상태 초기화
                isInitialized = false;
                isScanning = false;
                detectedRoomCount = 0;
                currentScanStatus = "정리됨";
                
                // 7. 메모리 강제 정리 (디버그 빌드에서만)
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                System.GC.Collect();
                #endif
                
                RoomUtilities.DebugLog("RoomDetectorNew 완전 정리 완료", true, scanSettings);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"RoomDetectorNew 정리 중 오류: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Unity 종료 시 정리
        /// </summary>
        private void OnDestroy()
        {
            Cleanup();
        }
        
        /// <summary>
        /// 애플리케이션 종료 시 정리
        /// </summary>
        private void OnApplicationQuit()
        {
            Cleanup();
        }
        
        /// <summary>
        /// 비활성화 시 정리 (Scene 전환 등)
        /// </summary>
        private void OnDisable()
        {
            if (enableAutoScan && autoScanCoroutine != null)
            {
                StopAutoScan();
                RoomUtilities.DebugLog("RoomDetectorNew 비활성화로 인한 자동 스캔 중지", true, scanSettings);
            }
        }
        
        /// <summary>
        /// 메모리 상태 체크 (디버그용)
        /// </summary>
        [ContextMenu("메모리 상태 체크")]
        public void CheckMemoryStatus()
        {
            var status = $"=== RoomDetectorNew 메모리 상태 ===\n" +
                        $"초기화됨: {isInitialized}\n" +
                        $"스캔 중: {isScanning}\n" +
                        $"자동 스캔 코루틴: {(autoScanCoroutine != null ? "실행 중" : "중지됨")}\n" +
                        $"DataManager: {(dataManager != null ? "활성" : "null")}\n" +
                        $"이벤트 구독자: {(OnRoomsUpdated?.GetInvocationList()?.Length ?? 0)}개\n";
            
            Debug.Log(status);
            
            // DataManager 메모리 체크
            dataManager?.CheckMemoryUsage();
        }
        
        /// <summary>
        /// 강제 메모리 정리 (디버그용)
        /// </summary>
        [ContextMenu("강제 메모리 정리")]
        public void ForceCleanupMemory()
        {
            Cleanup();
            
            // 강제 가비지 컬렉션
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            
            RoomUtilities.DebugLog("강제 메모리 정리 완료", true, scanSettings);
        }
        
        #endregion
    }
} 