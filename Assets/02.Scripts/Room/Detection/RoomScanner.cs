using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace JY
{
    /// <summary>
    /// 방 스캔 로직을 담당하는 클래스 (성능 최적화 적용)
    /// 씬의 오브젝트들을 분석하여 방 후보를 찾아냄
    /// </summary>
    public class RoomScanner
    {
        private readonly RoomScanSettings settings;
        private readonly Grid grid;
        private readonly FloodFillProcessor floodFillProcessor;
        
        // 성능 최적화: GameObject 캐싱 시스템
        private static Dictionary<string, GameObject[]> cachedObjectsByTag = new Dictionary<string, GameObject[]>();
        private static float lastCacheUpdateTime = 0f;
        private static readonly float CACHE_REFRESH_INTERVAL = 1f; // 1초마다 캐시 갱신
        
        // 재사용 가능한 컬렉션들
        private readonly Dictionary<Vector3Int, RoomCell> reusableCellGrid = new Dictionary<Vector3Int, RoomCell>();
        
        public RoomScanner(RoomScanSettings scanSettings, Grid gridSystem)
        {
            settings = scanSettings;
            grid = gridSystem;
            floodFillProcessor = new FloodFillProcessor(settings, grid);
        }
        
        /// <summary>
        /// 방 스캔 실행
        /// </summary>
        public RoomScanResult ScanForRooms()
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new RoomScanResult();
            result.scanStatus = "스캔 중...";
            
            try
            {
                RoomUtilities.DebugLog("=== 방 스캔 시작 ===", true, settings);
                
                // 1. 오브젝트 수집 (캐싱 최적화)
                var objectsByTag = CollectObjectsByTagsCached(settings);
                LogObjectCounts(objectsByTag);
                
                // 2. 셀 그리드 생성 (재사용 최적화)
                var cellGrid = BuildCellGridOptimized(objectsByTag);
                result.totalScannedCells = cellGrid.Count;
                
                // 3. 바닥 영역 탐지
                var floorAreas = floodFillProcessor.FindAllConnectedFloorAreas(cellGrid);
                
                // 4. 각 바닥 영역을 방으로 변환
                foreach (var floorArea in floorAreas)
                {
                    var room = CreateRoomFromFloorArea(floorArea, cellGrid);
                    if (room != null)
                    {
                        result.detectedRooms.Add(room);
                    }
                }
                
                // 5. 선베드 방 추가 (활성화된 경우)
                if (settings.enableSunbedRooms)
                {
                    var sunbedRooms = CreateSunbedRooms(objectsByTag, cellGrid);
                    result.detectedRooms.AddRange(sunbedRooms);
                }
                
                result.scanStatus = "스캔 완료";
                RoomUtilities.DebugLog($"=== 방 스캔 완료: {result.detectedRooms.Count}개 방 발견 ===", true, settings);
                
            }
            catch (System.Exception ex)
            {
                result.scanStatus = $"스캔 오류: {ex.Message}";
                RoomUtilities.DebugLog($"스캔 중 오류 발생: {ex.Message}", true, settings);
            }
            finally
            {
                stopwatch.Stop();
                result.scanDuration = (float)stopwatch.Elapsed.TotalSeconds;
                result.lastScanTime = System.DateTime.Now;
            }
            
            return result;
        }
        
        /// <summary>
        /// 셀 그리드 생성
        /// </summary>
        private Dictionary<Vector3Int, RoomCell> BuildCellGrid(Dictionary<string, GameObject[]> objectsByTag)
        {
            var cellGrid = new Dictionary<Vector3Int, RoomCell>();
            
            // 바닥 오브젝트 처리
            ProcessObjectsOfType(objectsByTag[RoomConstants.TAG_FLOOR], cellGrid, 
                                (cell, obj) => { cell.isFloor = true; cell.floorObject = obj; });
            
            // 벽 오브젝트 처리
            ProcessObjectsOfType(objectsByTag[RoomConstants.TAG_WALL], cellGrid,
                                (cell, obj) => { cell.isWall = true; cell.wallObject = obj; });
            
            // 문 오브젝트 처리
            ProcessObjectsOfType(objectsByTag[RoomConstants.TAG_DOOR], cellGrid,
                                (cell, obj) => { cell.isDoor = true; cell.doorObject = obj; });
            
            // 침대 오브젝트 처리
            ProcessObjectsOfType(objectsByTag[RoomConstants.TAG_BED], cellGrid,
                                (cell, obj) => { cell.isBed = true; cell.bedObject = obj; });
            
            // 선베드 오브젝트 처리
            ProcessObjectsOfType(objectsByTag[RoomConstants.TAG_SUNBED], cellGrid,
                                (cell, obj) => { cell.isSunbed = true; cell.sunbedObject = obj; });
            
            RoomUtilities.DebugLog($"셀 그리드 생성 완료: {cellGrid.Count}개 셀", false, settings);
            
            return cellGrid;
        }
        
        /// <summary>
        /// 특정 타입의 오브젝트들을 셀 그리드에 추가
        /// </summary>
        private void ProcessObjectsOfType(GameObject[] objects, 
                                         Dictionary<Vector3Int, RoomCell> cellGrid,
                                         System.Action<RoomCell, GameObject> setProperty)
        {
            foreach (GameObject obj in objects)
            {
                if (obj == null) continue;
                
                Vector3Int gridPos = RoomUtilities.WorldToGridPosition(obj.transform.position, grid);
                
                if (!cellGrid.ContainsKey(gridPos))
                {
                    cellGrid[gridPos] = new RoomCell(gridPos);
                }
                
                setProperty(cellGrid[gridPos], obj);
            }
        }
        
        /// <summary>
        /// 바닥 영역에서 방 생성
        /// </summary>
        private RoomInfo CreateRoomFromFloorArea(List<Vector3Int> floorCells, 
                                                Dictionary<Vector3Int, RoomCell> cellGrid)
        {
            if (floorCells.Count == 0) return null;
            
            var room = new RoomInfo();
            room.floorCells = floorCells;
            
            // 층 정보 계산
            Vector3 firstFloorWorldPos = RoomUtilities.GridToWorldPosition(floorCells[0], grid);
            room.floorLevel = FloorConstants.GetFloorLevel(firstFloorWorldPos.y);
            
            // 방 구성 요소 찾기
            room.walls = floodFillProcessor.FindWallsInArea(floorCells, cellGrid);
            room.doors = floodFillProcessor.FindDoorsInArea(floorCells, cellGrid);
            room.beds = floodFillProcessor.FindBedsInArea(floorCells, cellGrid);
            room.sunbeds = floodFillProcessor.FindSunbedsInArea(floorCells, cellGrid);
            
            // 방 경계와 중심 계산
            room.bounds = RoomUtilities.CalculateRoomBounds(floorCells, grid, room.floorLevel);
            room.center = room.bounds.center;
            
            // 방 ID 생성
            room.roomId = RoomUtilities.GenerateRoomId(room.center, room.floorLevel);
            
            // 가격과 명성도 계산
            room.calculatedPrice = RoomUtilities.CalculateRoomPrice(room);
            room.calculatedReputation = RoomUtilities.CalculateRoomReputation(room);
            
            RoomUtilities.DebugLog($"방 생성: {room.GetSummary()}", false, settings);
            
            return room;
        }
        
        /// <summary>
        /// 선베드 방들 생성
        /// </summary>
        private List<RoomInfo> CreateSunbedRooms(Dictionary<string, GameObject[]> objectsByTag,
                                                Dictionary<Vector3Int, RoomCell> cellGrid)
        {
            var sunbedRooms = new List<RoomInfo>();
            var processedSunbeds = new HashSet<GameObject>();
            
            foreach (GameObject sunbedObj in objectsByTag[RoomConstants.TAG_SUNBED])
            {
                if (sunbedObj == null || processedSunbeds.Contains(sunbedObj)) continue;
                
                Vector3Int sunbedGridPos = RoomUtilities.WorldToGridPosition(sunbedObj.transform.position, grid);
                
                // 선베드가 이미 일반 방에 포함되어 있는지 확인
                if (IsSunbedInExistingRoom(sunbedObj, sunbedGridPos, cellGrid))
                {
                    continue;
                }
                
                // 독립적인 선베드 방 생성
                var sunbedRoom = CreateSunbedRoom(sunbedObj, sunbedGridPos);
                if (sunbedRoom != null)
                {
                    sunbedRooms.Add(sunbedRoom);
                    processedSunbeds.Add(sunbedObj);
                    RoomUtilities.DebugLog($"독립적인 Sunbed 방 생성: {sunbedRoom.roomId}", false, settings);
                }
            }
            
            return sunbedRooms;
        }
        
        /// <summary>
        /// 선베드가 기존 방에 포함되어 있는지 확인
        /// </summary>
        private bool IsSunbedInExistingRoom(GameObject sunbedObj, Vector3Int sunbedGridPos,
                                           Dictionary<Vector3Int, RoomCell> cellGrid)
        {
            // 선베드 주변에 바닥이 있는지 확인
            foreach (Vector3Int direction in RoomConstants.DIRECTIONS_8)
            {
                Vector3Int neighborPos = sunbedGridPos + direction;
                
                if (cellGrid.ContainsKey(neighborPos) && cellGrid[neighborPos].isFloor)
                {
                    return true; // 바닥이 인접해 있으면 기존 방의 일부로 간주
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 선베드 방 생성
        /// </summary>
        private RoomInfo CreateSunbedRoom(GameObject sunbedObj, Vector3Int gridPos)
        {
            var room = new RoomInfo();
            room.isSunbedRoom = true;
            room.fixedPrice = settings.sunbedRoomPrice;
            room.fixedReputation = settings.sunbedRoomReputation;
            
            // 선베드를 중심으로 한 작은 영역 설정
            room.floorCells.Add(gridPos);
            room.sunbeds.Add(sunbedObj.transform.parent?.gameObject ?? sunbedObj);
            
            // FloorConstants를 사용하여 정확한 층 정보 계산
            float sunbedY = sunbedObj.transform.position.y;
            room.floorLevel = FloorConstants.GetFloorLevel(sunbedY);
            
            // 방 경계 설정
            room.bounds = RoomUtilities.CalculateRoomBounds(room.floorCells, grid, room.floorLevel);
            room.center = room.bounds.center;
            
            // 방 ID 생성
            room.roomId = RoomUtilities.GenerateRoomId(room.center, room.floorLevel, true);
            
            RoomUtilities.DebugLog($"Sunbed 방 생성: {room.GetSummary()}", false, settings);
            
            return room;
        }
        
        /// <summary>
        /// 오브젝트 개수 로그 출력
        /// </summary>
        private void LogObjectCounts(Dictionary<string, GameObject[]> objectsByTag)
        {
            if (!settings.showScanLogs) return;
            
            RoomUtilities.DebugLog("=== 오브젝트 개수 확인 ===", false, settings);
            
            foreach (var kvp in objectsByTag)
            {
                string tag = kvp.Key;
                int count = kvp.Value.Length;
                
                RoomUtilities.DebugLog($"{tag} 태그: {count}개", false, settings);
                
                if (count == 0)
                {
                    RoomUtilities.DebugLog($"⚠️ 경고: {tag} 태그가 설정된 오브젝트가 없습니다!", true, settings);
                }
            }
        }
        
        /// <summary>
        /// 스캔 설정 검증
        /// </summary>
        public bool ValidateSettings()
        {
            if (grid == null)
            {
                RoomUtilities.DebugLog("오류: Grid 시스템이 설정되지 않았습니다!", true, settings);
                return false;
            }
            
            if (settings.maxFloodFillIterations <= 0)
            {
                RoomUtilities.DebugLog("오류: maxFloodFillIterations이 0 이하입니다!", true, settings);
                return false;
            }
            
            if (settings.maxRoomSize <= 0)
            {
                RoomUtilities.DebugLog("오류: maxRoomSize가 0 이하입니다!", true, settings);
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 태그 상태 확인
        /// </summary>
        public void CheckTagStatus()
        {
            RoomUtilities.DebugLog("=== 태그 상태 확인 시작 ===", true, settings);
            
            string[] tagsToCheck = { 
                RoomConstants.TAG_FLOOR, 
                RoomConstants.TAG_WALL, 
                RoomConstants.TAG_DOOR, 
                RoomConstants.TAG_BED, 
                RoomConstants.TAG_SUNBED 
            };
            
            foreach (string tag in tagsToCheck)
            {
                var objects = GameObject.FindGameObjectsWithTag(tag);
                RoomUtilities.DebugLog($"{tag} 태그: {objects.Length}개", true, settings);
                
                if (objects.Length == 0)
                {
                    RoomUtilities.DebugLog($"⚠️ 경고: {tag} 태그가 설정된 오브젝트가 없습니다!", true, settings);
                }
                else
                {
                    for (int i = 0; i < Mathf.Min(objects.Length, 3); i++)
                    {
                        var obj = objects[i];
                        RoomUtilities.DebugLog($"  - {obj.name} (위치: {obj.transform.position})", true, settings);
                    }
                    if (objects.Length > 3)
                    {
                        RoomUtilities.DebugLog($"  ... 그 외 {objects.Length - 3}개 더", true, settings);
                    }
                }
            }
            
            RoomUtilities.DebugLog("=== 태그 상태 확인 완료 ===", true, settings);
        }
        
        #region 성능 최적화 메서드
        
        /// <summary>
        /// 캐싱된 GameObject 수집 (성능 최적화)
        /// </summary>
        private Dictionary<string, GameObject[]> CollectObjectsByTagsCached(RoomScanSettings settings)
        {
            float currentTime = Time.realtimeSinceStartup;
            
            // 캐시가 만료되었거나 처음 실행인 경우 갱신
            if (currentTime - lastCacheUpdateTime > CACHE_REFRESH_INTERVAL || cachedObjectsByTag.Count == 0)
            {
                RefreshObjectCache();
                lastCacheUpdateTime = currentTime;
            }
            
            var result = new Dictionary<string, GameObject[]>();
            
            string[] tags = { 
                RoomConstants.TAG_FLOOR, 
                RoomConstants.TAG_WALL, 
                RoomConstants.TAG_DOOR, 
                RoomConstants.TAG_BED, 
                RoomConstants.TAG_SUNBED 
            };
            
            foreach (string tag in tags)
            {
                if (cachedObjectsByTag.TryGetValue(tag, out GameObject[] cachedObjects))
                {
                    // 층별 필터링 적용
                    var filteredObjects = FilterObjectsByFloor(cachedObjects, settings);
                    result[tag] = filteredObjects;
                    
                    // 디버그 로그
                    if (settings.showScanLogs)
                    {
                        RoomUtilities.DebugLog($"{tag} 태그 (캐시됨): 전체 {cachedObjects.Length}개 중 {filteredObjects.Length}개 선택 (층: {settings.currentScanFloor})", false, settings);
                    }
                }
                else
                {
                    result[tag] = new GameObject[0];
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// GameObject 캐시 새로고침
        /// </summary>
        private void RefreshObjectCache()
        {
            cachedObjectsByTag.Clear();
            
            string[] tags = { 
                RoomConstants.TAG_FLOOR, 
                RoomConstants.TAG_WALL, 
                RoomConstants.TAG_DOOR, 
                RoomConstants.TAG_BED, 
                RoomConstants.TAG_SUNBED 
            };
            
            foreach (string tag in tags)
            {
                var objects = GameObject.FindGameObjectsWithTag(tag);
                cachedObjectsByTag[tag] = objects;
            }
            
            RoomUtilities.DebugLog($"GameObject 캐시 갱신 완료 ({cachedObjectsByTag.Count}개 태그)", false, settings);
        }
        
        /// <summary>
        /// 층별 GameObject 필터링
        /// </summary>
        private GameObject[] FilterObjectsByFloor(GameObject[] objects, RoomScanSettings settings)
        {
            if (settings.scanAllFloors) return objects;
            
            var filtered = new List<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                if (RoomUtilities.ShouldProcessObject(objects[i], settings))
                {
                    filtered.Add(objects[i]);
                }
            }
            
            return filtered.ToArray();
        }
        
        /// <summary>
        /// 캐시 강제 새로고침 (외부 호출용)
        /// </summary>
        public static void ForceRefreshCache()
        {
            cachedObjectsByTag.Clear();
            lastCacheUpdateTime = 0f;
        }
        
        /// <summary>
        /// 셀 그리드 생성 (성능 최적화)
        /// </summary>
        private Dictionary<Vector3Int, RoomCell> BuildCellGridOptimized(Dictionary<string, GameObject[]> objectsByTag)
        {
            // 재사용 가능한 Dictionary 초기화
            reusableCellGrid.Clear();
            
            // 바닥 오브젝트 처리 (가장 많이 호출되므로 최우선 최적화)
            ProcessObjectsOfTypeOptimized(objectsByTag[RoomConstants.TAG_FLOOR], reusableCellGrid, 
                                         (cell, obj) => { cell.isFloor = true; cell.floorObject = obj; });
            
            // 벽 오브젝트 처리
            ProcessObjectsOfTypeOptimized(objectsByTag[RoomConstants.TAG_WALL], reusableCellGrid,
                                         (cell, obj) => { cell.isWall = true; cell.wallObject = obj; });
            
            // 문 오브젝트 처리
            ProcessObjectsOfTypeOptimized(objectsByTag[RoomConstants.TAG_DOOR], reusableCellGrid,
                                         (cell, obj) => { cell.isDoor = true; cell.doorObject = obj; });
            
            // 침대 오브젝트 처리
            ProcessObjectsOfTypeOptimized(objectsByTag[RoomConstants.TAG_BED], reusableCellGrid,
                                         (cell, obj) => { cell.isBed = true; cell.bedObject = obj; });
            
            // 선베드 오브젝트 처리
            ProcessObjectsOfTypeOptimized(objectsByTag[RoomConstants.TAG_SUNBED], reusableCellGrid,
                                         (cell, obj) => { cell.isSunbed = true; cell.sunbedObject = obj; });
            
            RoomUtilities.DebugLog($"셀 그리드 생성 완료 (최적화): {reusableCellGrid.Count}개 셀", false, settings);
            
            // 새 Dictionary로 복사하여 반환 (외부에서 안전하게 사용)
            return new Dictionary<Vector3Int, RoomCell>(reusableCellGrid);
        }
        
        /// <summary>
        /// 특정 타입의 오브젝트들을 셀 그리드에 추가 (성능 최적화)
        /// </summary>
        private void ProcessObjectsOfTypeOptimized(GameObject[] objects, 
                                                  Dictionary<Vector3Int, RoomCell> cellGrid,
                                                  System.Action<RoomCell, GameObject> setProperty)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject obj = objects[i];
                if (obj == null) continue;
                
                Vector3Int gridPos = RoomUtilities.WorldToGridPosition(obj.transform.position, grid);
                
                // TryGetValue를 사용하여 성능 향상
                if (!cellGrid.TryGetValue(gridPos, out RoomCell cell))
                {
                    cell = new RoomCell(gridPos);
                    cellGrid[gridPos] = cell;
                }
                
                setProperty(cell, obj);
            }
        }
        
        /// <summary>
        /// 캐시 상태 체크 (디버그용)
        /// </summary>
        public static void CheckCacheStatus()
        {
            var info = $"=== GameObject 캐시 상태 ===\n" +
                      $"캐시된 태그 수: {cachedObjectsByTag.Count}\n" +
                      $"마지막 갱신: {Time.realtimeSinceStartup - lastCacheUpdateTime:F2}초 전\n" +
                      $"갱신 주기: {CACHE_REFRESH_INTERVAL}초\n";
            
            foreach (var kvp in cachedObjectsByTag)
            {
                info += $"{kvp.Key}: {kvp.Value.Length}개\n";
            }
            
            UnityEngine.Debug.Log(info);
        }
        
        #endregion
    }
} 