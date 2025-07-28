using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace JY
{
    /// <summary>
    /// FloodFill 알고리즘을 담당하는 클래스
    /// 방의 바닥 영역을 탐지하는 핵심 로직 (성능 최적화 적용)
    /// </summary>
    public class FloodFillProcessor
    {
        private readonly RoomScanSettings settings;
        private readonly Grid grid;
        
        // 성능 최적화: 재사용 가능한 컬렉션들 (오브젝트 풀링)
        private readonly List<Vector3Int> reusableFloodFillResult = new List<Vector3Int>();
        private readonly Queue<Vector3Int> reusableQueue = new Queue<Vector3Int>();
        private readonly HashSet<Vector3Int> reusableVisited = new HashSet<Vector3Int>();
        private readonly List<GameObject> reusableWallList = new List<GameObject>();
        private readonly List<GameObject> reusableDoorList = new List<GameObject>();
        private readonly HashSet<GameObject> reusableObjectSet = new HashSet<GameObject>();
        
        public FloodFillProcessor(RoomScanSettings scanSettings, Grid gridSystem)
        {
            settings = scanSettings;
            grid = gridSystem;
        }
        
        /// <summary>
        /// FloodFill을 사용하여 연결된 바닥 영역 찾기 (성능 최적화)
        /// </summary>
        public List<Vector3Int> FindConnectedFloorArea(Vector3Int startPos, 
                                                      Dictionary<Vector3Int, RoomCell> cellGrid,
                                                      HashSet<Vector3Int> processedCells)
        {
            // 재사용 가능한 컬렉션 초기화
            reusableFloodFillResult.Clear();
            reusableQueue.Clear();
            reusableVisited.Clear();
            
            reusableQueue.Enqueue(startPos);
            reusableVisited.Add(startPos);
            
            int iterations = 0;
            
            while (reusableQueue.Count > 0 && iterations < settings.maxFloodFillIterations)
            {
                iterations++;
                Vector3Int current = reusableQueue.Dequeue();
                
                // 현재 셀이 바닥인지 확인
                if (cellGrid.ContainsKey(current) && cellGrid[current].isFloor && !processedCells.Contains(current))
                {
                    reusableFloodFillResult.Add(current);
                    processedCells.Add(current);
                    
                    // 최대 룸 크기 체크
                    if (reusableFloodFillResult.Count >= settings.maxRoomSize)
                    {
                        RoomUtilities.DebugLog($"FloodFill: 최대 룸 크기 도달 ({settings.maxRoomSize})", false, settings);
                        break;
                    }
                    
                    // 인접한 셀들을 큐에 추가 (캐시된 방향 배열 사용)
                    for (int i = 0; i < RoomConstants.DIRECTIONS_4.Length; i++)
                    {
                        Vector3Int neighbor = current + RoomConstants.DIRECTIONS_4[i];
                        
                        if (!reusableVisited.Contains(neighbor))
                        {
                            reusableVisited.Add(neighbor);
                            reusableQueue.Enqueue(neighbor);
                        }
                    }
                }
            }
            
            if (iterations >= settings.maxFloodFillIterations)
            {
                RoomUtilities.DebugLog($"FloodFill: 최대 반복 횟수 도달 ({settings.maxFloodFillIterations})", true, settings);
            }
            
            RoomUtilities.DebugLog($"FloodFill 완료: {reusableFloodFillResult.Count}개 셀, {iterations}번 반복", false, settings);
            
            // 결과를 새 리스트로 복사하여 반환 (외부에서 안전하게 사용)
            return new List<Vector3Int>(reusableFloodFillResult);
        }
        
        /// <summary>
        /// 방 영역 내의 벽 찾기 (성능 최적화)
        /// </summary>
        public List<GameObject> FindWallsInArea(List<Vector3Int> floorCells, 
                                               Dictionary<Vector3Int, RoomCell> cellGrid)
        {
            reusableObjectSet.Clear(); // 중복 제거용 Set 재사용
            
            for (int i = 0; i < floorCells.Count; i++)
            {
                Vector3Int floorCell = floorCells[i];
                
                // 바닥 셀 주변의 벽 찾기 (캐시된 방향 배열 사용)
                for (int j = 0; j < RoomConstants.DIRECTIONS_8.Length; j++)
                {
                    Vector3Int neighborPos = floorCell + RoomConstants.DIRECTIONS_8[j];
                    
                    if (cellGrid.TryGetValue(neighborPos, out RoomCell neighborCell))
                    {
                        if (neighborCell.isWall && neighborCell.wallObject != null)
                        {
                            reusableObjectSet.Add(neighborCell.wallObject);
                        }
                    }
                }
            }
            
            // Set의 내용을 재사용 가능한 리스트로 복사
            reusableWallList.Clear();
            reusableWallList.AddRange(reusableObjectSet);
            
            return new List<GameObject>(reusableWallList);
        }
        
        /// <summary>
        /// 방 영역 내의 문 찾기 (성능 최적화)
        /// </summary>
        public List<GameObject> FindDoorsInArea(List<Vector3Int> floorCells, 
                                               Dictionary<Vector3Int, RoomCell> cellGrid)
        {
            reusableObjectSet.Clear(); // 중복 제거용 Set 재사용
            
            for (int i = 0; i < floorCells.Count; i++)
            {
                Vector3Int floorCell = floorCells[i];
                
                // 바닥 셀과 인접한 문 찾기 (캐시된 방향 배열 사용)
                for (int j = 0; j < RoomConstants.DIRECTIONS_8.Length; j++)
                {
                    Vector3Int neighborPos = floorCell + RoomConstants.DIRECTIONS_8[j];
                    
                    if (cellGrid.TryGetValue(neighborPos, out RoomCell neighborCell))
                    {
                        if (neighborCell.isDoor && neighborCell.doorObject != null)
                        {
                            reusableObjectSet.Add(neighborCell.doorObject);
                        }
                    }
                }
                
                // 바닥 셀 자체에 문이 있는지도 확인
                if (cellGrid.TryGetValue(floorCell, out RoomCell floorCellData))
                {
                    if (floorCellData.isDoor && floorCellData.doorObject != null)
                    {
                        reusableObjectSet.Add(floorCellData.doorObject);
                    }
                }
            }
            
            // Set의 내용을 재사용 가능한 리스트로 복사
            reusableDoorList.Clear();
            reusableDoorList.AddRange(reusableObjectSet);
            
            return new List<GameObject>(reusableDoorList);
        }
        
        /// <summary>
        /// 방 영역 내의 침대 찾기
        /// </summary>
        public List<GameObject> FindBedsInArea(List<Vector3Int> floorCells, 
                                              Dictionary<Vector3Int, RoomCell> cellGrid)
        {
            var beds = new HashSet<GameObject>();
            
            foreach (Vector3Int floorCell in floorCells)
            {
                // 바닥 셀에 직접 배치된 침대 찾기
                if (cellGrid.ContainsKey(floorCell))
                {
                    var cell = cellGrid[floorCell];
                    if (cell.isBed && cell.bedObject != null)
                    {
                        beds.Add(cell.bedObject);
                    }
                }
                
                // 인접한 셀의 침대도 확인 (침대가 여러 셀에 걸쳐 있을 수 있음)
                foreach (Vector3Int direction in RoomConstants.DIRECTIONS_4)
                {
                    Vector3Int neighborPos = floorCell + direction;
                    
                    if (cellGrid.ContainsKey(neighborPos))
                    {
                        var neighborCell = cellGrid[neighborPos];
                        if (neighborCell.isBed && neighborCell.bedObject != null)
                        {
                            // 침대가 방 영역 근처에 있는지 확인
                            if (IsObjectNearFloorArea(neighborCell.bedObject, floorCells))
                            {
                                beds.Add(neighborCell.bedObject);
                            }
                        }
                    }
                }
            }
            
            return beds.ToList();
        }
        
        /// <summary>
        /// 방 영역 내의 선베드 찾기
        /// </summary>
        public List<GameObject> FindSunbedsInArea(List<Vector3Int> floorCells, 
                                                 Dictionary<Vector3Int, RoomCell> cellGrid)
        {
            var sunbeds = new HashSet<GameObject>();
            
            foreach (Vector3Int floorCell in floorCells)
            {
                // 바닥 셀에 직접 배치된 선베드 찾기
                if (cellGrid.ContainsKey(floorCell))
                {
                    var cell = cellGrid[floorCell];
                    if (cell.isSunbed && cell.sunbedObject != null)
                    {
                        sunbeds.Add(cell.sunbedObject);
                    }
                }
                
                // 인접한 셀의 선베드도 확인
                foreach (Vector3Int direction in RoomConstants.DIRECTIONS_4)
                {
                    Vector3Int neighborPos = floorCell + direction;
                    
                    if (cellGrid.ContainsKey(neighborPos))
                    {
                        var neighborCell = cellGrid[neighborPos];
                        if (neighborCell.isSunbed && neighborCell.sunbedObject != null)
                        {
                            if (IsObjectNearFloorArea(neighborCell.sunbedObject, floorCells))
                            {
                                sunbeds.Add(neighborCell.sunbedObject);
                            }
                        }
                    }
                }
            }
            
            return sunbeds.ToList();
        }
        
        /// <summary>
        /// 오브젝트가 바닥 영역 근처에 있는지 확인
        /// </summary>
        private bool IsObjectNearFloorArea(GameObject obj, List<Vector3Int> floorCells)
        {
            if (obj == null || grid == null) return false;
            
            Vector3Int objGridPos = RoomUtilities.WorldToGridPosition(obj.transform.position, grid);
            
            // 오브젝트 위치에서 가장 가까운 바닥 셀과의 거리 확인
            int minDistance = floorCells.Min(floorCell => RoomUtilities.GetGridDistance(objGridPos, floorCell));
            
            return minDistance <= 2; // 2칸 이내면 방에 속한 것으로 간주
        }
        
        /// <summary>
        /// 연결된 모든 바닥 영역 찾기
        /// </summary>
        public List<List<Vector3Int>> FindAllConnectedFloorAreas(Dictionary<Vector3Int, RoomCell> cellGrid)
        {
            var allAreas = new List<List<Vector3Int>>();
            var processedCells = new HashSet<Vector3Int>();
            
            foreach (var kvp in cellGrid)
            {
                Vector3Int position = kvp.Key;
                RoomCell cell = kvp.Value;
                
                // 처리되지 않은 바닥 셀에서 시작
                if (cell.isFloor && !processedCells.Contains(position))
                {
                    var area = FindConnectedFloorArea(position, cellGrid, processedCells);
                    
                    if (area.Count > 0)
                    {
                        allAreas.Add(area);
                        RoomUtilities.DebugLog($"연결된 바닥 영역 발견: {area.Count}개 셀", false, settings);
                    }
                }
            }
            
            RoomUtilities.DebugLog($"총 {allAreas.Count}개 연결된 바닥 영역 발견", true, settings);
            
            return allAreas;
        }
        
        /// <summary>
        /// 바닥 영역의 경계선 계산
        /// </summary>
        public List<Vector3Int> CalculateAreaBoundary(List<Vector3Int> floorCells)
        {
            var boundary = new HashSet<Vector3Int>();
            
            foreach (Vector3Int floorCell in floorCells)
            {
                // 4방향으로 인접한 셀 중 바닥이 아닌 셀이 있으면 경계
                foreach (Vector3Int direction in RoomConstants.DIRECTIONS_4)
                {
                    Vector3Int neighbor = floorCell + direction;
                    
                    if (!floorCells.Contains(neighbor))
                    {
                        boundary.Add(floorCell);
                        break;
                    }
                }
            }
            
            return boundary.ToList();
        }
        
        /// <summary>
        /// 영역의 중심점 계산
        /// </summary>
        public Vector3 CalculateAreaCenter(List<Vector3Int> floorCells)
        {
            if (floorCells.Count == 0) return Vector3.zero;
            
            Vector3 sum = Vector3.zero;
            foreach (Vector3Int cell in floorCells)
            {
                sum += RoomUtilities.GridToWorldPosition(cell, grid);
            }
            
            return sum / floorCells.Count;
        }
    }
} 