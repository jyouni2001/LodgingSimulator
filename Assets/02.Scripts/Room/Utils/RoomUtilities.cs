using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace JY
{
    /// <summary>
    /// 방 시스템 유틸리티 클래스
    /// </summary>
    public static class RoomUtilities
    {
        /// <summary>
        /// 오브젝트가 현재 스캔 대상인지 확인 (층별 필터링 개선)
        /// </summary>
        public static bool ShouldProcessObject(GameObject obj, RoomScanSettings settings)
        {
            if (settings.scanAllFloors) return true;
            
            float objY = obj.transform.position.y;
            int objFloor = GetAccurateFloorLevel(objY, obj);
            
            return objFloor == settings.currentScanFloor;
        }
        
        /// <summary>
        /// 정확한 층 계산 (PlacementSystem과 연동)
        /// </summary>
        public static int GetAccurateFloorLevel(float yPosition, GameObject obj = null)
        {
            // PlacementSystem의 활성 층 정보를 참고
            var placementSystem = PlacementSystem.Instance;
            if (placementSystem != null)
            {
                // PlacementSystem의 plane bounds를 기반으로 층 계산
                int floorFromPlanes = GetFloorFromPlacementSystem(yPosition, placementSystem);
                if (floorFromPlanes > 0)
                {
                    return floorFromPlanes;
                }
            }
            
            // 기본 FloorConstants 계산 방식 개선
            return GetImprovedFloorLevel(yPosition);
        }
        
        /// <summary>
        /// PlacementSystem의 plane 정보를 기반으로 층 계산
        /// </summary>
        private static int GetFloorFromPlacementSystem(float yPosition, PlacementSystem placementSystem)
        {
            try
            {
                // 1층 plane들 확인
                if (placementSystem.plane1f != null)
                {
                    foreach (var plane in placementSystem.plane1f)
                    {
                        if (plane != null && plane.activeSelf)
                        {
                            var renderer = plane.GetComponent<Renderer>();
                            if (renderer != null && IsWithinPlaneBounds(yPosition, renderer.bounds, 1))
                            {
                                return 1;
                            }
                        }
                    }
                }
                
                // 2층 plane들 확인
                if (placementSystem.plane2f != null)
                {
                    foreach (var plane in placementSystem.plane2f)
                    {
                        if (plane != null && plane.activeSelf)
                        {
                            var renderer = plane.GetComponent<Renderer>();
                            if (renderer != null && IsWithinPlaneBounds(yPosition, renderer.bounds, 2))
                            {
                                return 2;
                            }
                        }
                    }
                }
                
                // 3층 plane들 확인
                if (placementSystem.plane3f != null)
                {
                    foreach (var plane in placementSystem.plane3f)
                    {
                        if (plane != null && plane.activeSelf)
                        {
                            var renderer = plane.GetComponent<Renderer>();
                            if (renderer != null && IsWithinPlaneBounds(yPosition, renderer.bounds, 3))
                            {
                                return 3;
                            }
                        }
                    }
                }
                
                // 4층 plane들 확인
                if (placementSystem.plane4f != null)
                {
                    foreach (var plane in placementSystem.plane4f)
                    {
                        if (plane != null && plane.activeSelf)
                        {
                            var renderer = plane.GetComponent<Renderer>();
                            if (renderer != null && IsWithinPlaneBounds(yPosition, renderer.bounds, 4))
                            {
                                return 4;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"PlacementSystem 층 계산 오류: {ex.Message}");
            }
            
            return 0; // 계산 실패
        }
        
        /// <summary>
        /// Y 위치가 plane bounds 내에 있는지 확인
        /// </summary>
        private static bool IsWithinPlaneBounds(float yPosition, Bounds planeBounds, int expectedFloor)
        {
            float floorBaseY = FloorConstants.GetFloorBaseY(expectedFloor);
            float floorTopY = floorBaseY + FloorConstants.ROOM_HEIGHT;
            
            // 층 높이 범위 내에 있는지 확인 (허용 오차 포함)
            float tolerance = FloorConstants.FLOOR_TOLERANCE * 0.5f;
            return yPosition >= (floorBaseY - tolerance) && yPosition <= (floorTopY + tolerance);
        }
        
        /// <summary>
        /// 개선된 FloorConstants 기반 층 계산
        /// </summary>
        private static int GetImprovedFloorLevel(float yPosition)
        {
            // 더 정확한 반올림 방식 사용
            float normalizedHeight = yPosition / FloorConstants.FLOOR_HEIGHT;
            int calculatedFloor = Mathf.RoundToInt(normalizedHeight) + 1;
            
            // 최소 1층, 최대 10층으로 제한
            return Mathf.Clamp(calculatedFloor, 1, 10);
        }
        
        /// <summary>
        /// 태그로 오브젝트들 수집 (층별 필터링 개선)
        /// </summary>
        public static Dictionary<string, GameObject[]> CollectObjectsByTags(RoomScanSettings settings)
        {
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
                var allObjects = GameObject.FindGameObjectsWithTag(tag);
                var filteredObjects = allObjects.Where(obj => ShouldProcessObject(obj, settings)).ToArray();
                
                result[tag] = filteredObjects;
                
                // 디버그 로그
                if (settings.showScanLogs)
                {
                    DebugLog($"{tag} 태그: 전체 {allObjects.Length}개 중 {filteredObjects.Length}개 선택 (층: {settings.currentScanFloor})", false, settings);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 현재 활성 층 감지
        /// </summary>
        public static int GetCurrentActiveFloor()
        {
            var placementSystem = PlacementSystem.Instance;
            if (placementSystem == null) return 1;
            
            // FloorLock 상태와 구매 레벨로 판단
            if (placementSystem.GetFloorLock() && placementSystem.currentPurchaseLevel >= 4)
            {
                // 2층 이상 해금된 경우, 활성화된 가장 높은 층 반환
                if (IsFloorActive(placementSystem, 4)) return 4;
                if (IsFloorActive(placementSystem, 3)) return 3;
                if (IsFloorActive(placementSystem, 2)) return 2;
            }
            
            return 1; // 기본값
        }
        
        /// <summary>
        /// 특정 층이 활성화되어 있는지 확인
        /// </summary>
        private static bool IsFloorActive(PlacementSystem placementSystem, int floor)
        {
            List<GameObject> planes = floor switch
            {
                1 => placementSystem.plane1f,
                2 => placementSystem.plane2f,
                3 => placementSystem.plane3f,
                4 => placementSystem.plane4f,
                _ => null
            };
            
            if (planes == null) return false;
            
            return planes.Any(plane => plane != null && plane.activeSelf);
        }
        
        /// <summary>
        /// 그리드 위치를 월드 위치로 변환
        /// </summary>
        public static Vector3 GridToWorldPosition(Vector3Int gridPos, Grid grid)
        {
            if (grid == null) return Vector3.zero;
            return grid.GetCellCenterWorld(gridPos);
        }
        
        /// <summary>
        /// 월드 위치를 그리드 위치로 변환
        /// </summary>
        public static Vector3Int WorldToGridPosition(Vector3 worldPos, Grid grid)
        {
            if (grid == null) return Vector3Int.zero;
            return grid.WorldToCell(worldPos);
        }
        
        /// <summary>
        /// 방 경계 계산 (층별 정확한 Y축 적용)
        /// </summary>
        public static Bounds CalculateRoomBounds(List<Vector3Int> floorCells, Grid grid, int floorLevel)
        {
            if (floorCells.Count == 0) return new Bounds();
            
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            
            foreach (Vector3Int cellPos in floorCells)
            {
                Vector3 worldPos = GridToWorldPosition(cellPos, grid);
                
                min.x = Mathf.Min(min.x, worldPos.x);
                min.z = Mathf.Min(min.z, worldPos.z);
                max.x = Mathf.Max(max.x, worldPos.x);
                max.z = Mathf.Max(max.z, worldPos.z);
            }
            
            // 층별 정확한 Y축 설정
            float floorBaseY = FloorConstants.GetFloorBaseY(floorLevel);
            min.y = floorBaseY;
            max.y = floorBaseY + FloorConstants.ROOM_HEIGHT;
            
                         Vector3 center = (min + max) * 0.5f;
             Vector3 size = max - min;
            
            return new Bounds(center, size);
        }
        
        /// <summary>
        /// 방 ID 생성 (층 정보 포함)
        /// </summary>
        public static string GenerateRoomId(Vector3 center, int floorLevel, bool isSunbedRoom = false)
        {
            string prefix = isSunbedRoom ? "SunbedRoom" : "Room";
            return $"{prefix}_F{floorLevel}_{center.x:F0}_{center.z:F0}";
        }
        
        /// <summary>
        /// 방 가격 계산
        /// </summary>
        public static float CalculateRoomPrice(RoomInfo room)
        {
            if (room.isSunbedRoom)
            {
                return room.fixedPrice > 0 ? room.fixedPrice : RoomConstants.DEFAULT_SUNBED_ROOM_PRICE;
            }
            
            // 일반 방 가격 계산 (층별 보정 추가)
            float basePrice = 100f;
            float sizeMultiplier = room.GetRoomSize() * 10f;
            float bedMultiplier = room.beds.Count * 50f;
            float doorMultiplier = room.doors.Count * 20f;
            float floorMultiplier = room.floorLevel * 25f; // 층이 높을수록 비싸짐
            
            return basePrice + sizeMultiplier + bedMultiplier + doorMultiplier + floorMultiplier;
        }
        
        /// <summary>
        /// 방 명성도 계산
        /// </summary>
        public static float CalculateRoomReputation(RoomInfo room)
        {
            if (room.isSunbedRoom)
            {
                return room.fixedReputation > 0 ? room.fixedReputation : RoomConstants.DEFAULT_SUNBED_ROOM_REPUTATION;
            }
            
            // 일반 방 명성도 계산 (층별 보정 추가)
            float baseReputation = 50f;
            float sizeBonus = Mathf.Min(room.GetRoomSize() * 2f, 50f);
            float bedBonus = room.beds.Count * 10f;
            float doorPenalty = Mathf.Max(0, (room.doors.Count - 1) * 5f);
            float floorBonus = (room.floorLevel - 1) * 15f; // 높은 층일수록 명성도 높음
            
            return baseReputation + sizeBonus + bedBonus - doorPenalty + floorBonus;
        }
        
        /// <summary>
        /// 셀 내용 설명 반환
        /// </summary>
        public static string GetCellContentDescription(RoomCell cell)
        {
            var contents = new List<string>();
            if (cell.isFloor) contents.Add("바닥");
            if (cell.isWall) contents.Add("벽");
            if (cell.isDoor) contents.Add("문");
            if (cell.isBed) contents.Add("침대");
            if (cell.isSunbed) contents.Add("선베드");
            
            return contents.Count > 0 ? string.Join(", ", contents) : "빈 셀";
        }
        
        /// <summary>
        /// 방 게임오브젝트 생성 (DEPRECATED - RoomFactory 사용 권장)
        /// </summary>
        [System.Obsolete("RoomFactory.CreateRoomGameObject 사용을 권장합니다.", false)]
        public static GameObject CreateRoomGameObject(RoomInfo room)
        {
            // RoomFactory로 위임하여 중복 코드 제거
            var factory = new RoomFactory(new RoomScanSettings());
            return factory.CreateRoomGameObject(room);
        }
        
        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        public static void DebugLog(string message, bool isImportant, RoomScanSettings settings)
        {
            if (!settings.showDebugLogs) return;
            if (settings.showImportantLogsOnly && !isImportant) return;
            
            Debug.Log($"[RoomSystem] {message}");
        }
        
        /// <summary>
        /// 방향이 유효한지 확인
        /// </summary>
        public static bool IsValidDirection(Vector3Int direction)
        {
            return RoomConstants.DIRECTIONS_8.Contains(direction);
        }
        
        /// <summary>
        /// 인접한 셀들 반환
        /// </summary>
        public static List<Vector3Int> GetAdjacentCells(Vector3Int centerCell, bool use8Directions = true)
        {
            var directions = use8Directions ? RoomConstants.DIRECTIONS_8 : RoomConstants.DIRECTIONS_4;
            var adjacentCells = new List<Vector3Int>();
            
            foreach (Vector3Int direction in directions)
            {
                adjacentCells.Add(centerCell + direction);
            }
            
            return adjacentCells;
        }
        
        /// <summary>
        /// 두 위치 사이의 거리 계산 (그리드 기준)
        /// </summary>
        public static int GetGridDistance(Vector3Int pos1, Vector3Int pos2)
        {
            return Mathf.Abs(pos1.x - pos2.x) + Mathf.Abs(pos1.z - pos2.z);
        }
        
        /// <summary>
        /// 위치가 영역 내에 있는지 확인
        /// </summary>
        public static bool IsPositionInBounds(Vector3Int position, Bounds bounds, Grid grid)
        {
            Vector3 worldPos = GridToWorldPosition(position, grid);
            return bounds.Contains(worldPos);
        }
    }
} 