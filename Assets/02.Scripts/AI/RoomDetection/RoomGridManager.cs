using UnityEngine;
using System.Collections.Generic;
using ZLinq;

namespace JY.RoomDetection
{
    /// <summary>
    /// 방 감지를 위한 그리드 시스템 관리
    /// 3D 좌표를 그리드로 변환하고 셀 정보를 관리
    /// </summary>
    public class RoomGridManager
    {
        [System.Serializable]
        public class GridSettings
        {
            public float cellSize = 1f;
            public float floorDetectionOffset = -0.5f;
            public LayerMask roomElementsLayer = -1;
        }

        // 그리드 데이터
        private Dictionary<Vector3Int, RoomCell> roomGrid = new Dictionary<Vector3Int, RoomCell>();
        private GridSettings settings;
        private Grid unityGrid;

        public RoomGridManager(GridSettings gridSettings, Grid grid = null)
        {
            settings = gridSettings;
            unityGrid = grid;
        }

        /// <summary>
        /// 씬에서 그리드 데이터 업데이트
        /// </summary>
        public void UpdateGridFromScene(int targetFloor = -1)
        {
            roomGrid.Clear();
            
            AIDebugLogger.Log("GridManager", "그리드 업데이트 시작", LogCategory.Room);

            // 각 태그별로 오브젝트 처리
            ProcessTaggedObjects("Floor", CellType.Floor, targetFloor);
            ProcessTaggedObjects("Wall", CellType.Wall, targetFloor);
            ProcessTaggedObjects("Door", CellType.Door, targetFloor);
            ProcessTaggedObjects("Bed", CellType.Bed, targetFloor);
            
            AIDebugLogger.Log("GridManager", $"그리드 업데이트 완료 - 총 {roomGrid.Count}개 셀", LogCategory.Room);
        }

        /// <summary>
        /// 태그된 오브젝트들을 그리드에 추가
        /// </summary>
        private void ProcessTaggedObjects(string tag, CellType cellType, int targetFloor)
        {
            var objects = GameObject.FindGameObjectsWithTag(tag);
            int processedCount = 0;

            foreach (var obj in objects)
            {
                // 층 필터링 (targetFloor가 -1이면 모든 층 처리)
                if (targetFloor != -1)
                {
                    int objFloor = GetFloorLevel(obj.transform.position.y);
                    if (objFloor != targetFloor) continue;
                }

                Vector3Int gridPos = WorldToGridPosition(obj.transform.position, cellType);
                
                if (!roomGrid.ContainsKey(gridPos))
                {
                    roomGrid[gridPos] = new RoomCell
                    {
                        position = gridPos,
                        worldHeight = obj.transform.position.y,
                        objects = new List<GameObject>()
                    };
                }

                var cell = roomGrid[gridPos];
                
                // 셀 타입 설정
                switch (cellType)
                {
                    case CellType.Floor:
                        cell.isFloor = true;
                        break;
                    case CellType.Wall:
                        cell.isWall = true;
                        break;
                    case CellType.Door:
                        cell.isDoor = true;
                        break;
                    case CellType.Bed:
                        cell.isBed = true;
                        break;
                }

                // 오브젝트 추가 (중복 방지)
                GameObject targetObj = obj.transform.parent?.gameObject ?? obj;
                if (!cell.objects.Contains(targetObj))
                {
                    cell.objects.Add(targetObj);
                }

                processedCount++;
            }

            AIDebugLogger.Log("GridManager", $"{tag} 태그: {processedCount}개 처리됨", LogCategory.Room);
        }

        /// <summary>
        /// 월드 좌표를 그리드 좌표로 변환
        /// </summary>
        public Vector3Int WorldToGridPosition(Vector3 worldPos, CellType cellType)
        {
            // 셀 타입에 따른 오프셋 적용
            float yOffset = (cellType == CellType.Floor || cellType == CellType.Bed) 
                ? settings.floorDetectionOffset : 0f;
            
            Vector3 adjustedPos = new Vector3(worldPos.x, worldPos.y + yOffset, worldPos.z);
            
            if (unityGrid != null)
            {
                return unityGrid.WorldToCell(adjustedPos);
            }
            else
            {
                // 기본 그리드 변환
                return new Vector3Int(
                    Mathf.FloorToInt(adjustedPos.x / settings.cellSize),
                    Mathf.FloorToInt(adjustedPos.y / settings.cellSize),
                    Mathf.FloorToInt(adjustedPos.z / settings.cellSize)
                );
            }
        }

        /// <summary>
        /// 그리드 좌표를 월드 좌표로 변환
        /// </summary>
        public Vector3 GridToWorldPosition(Vector3Int gridPos)
        {
            if (unityGrid != null)
            {
                return unityGrid.GetCellCenterWorld(gridPos);
            }
            else
            {
                return new Vector3(
                    gridPos.x * settings.cellSize,
                    gridPos.y * settings.cellSize,
                    gridPos.z * settings.cellSize
                );
            }
        }

        /// <summary>
        /// 특정 위치의 셀 타입 반환
        /// </summary>
        public CellType GetCellType(Vector3Int gridPos)
        {
            if (!roomGrid.TryGetValue(gridPos, out RoomCell cell))
                return CellType.Empty;

            // 우선순위: 벽 > 문 > 침대 > 바닥
            if (cell.isWall) return CellType.Wall;
            if (cell.isDoor) return CellType.Door;
            if (cell.isBed) return CellType.Bed;
            if (cell.isFloor) return CellType.Floor;
            
            return CellType.Empty;
        }

        /// <summary>
        /// 특정 위치의 게임오브젝트들 반환
        /// </summary>
        public List<GameObject> GetObjectsAt(Vector3Int gridPos)
        {
            if (roomGrid.TryGetValue(gridPos, out RoomCell cell))
            {
                return cell.objects ?? new List<GameObject>();
            }
            return new List<GameObject>();
        }

        /// <summary>
        /// 모든 침대 위치 반환
        /// </summary>
        public List<Vector3Int> GetAllBedPositions()
        {
            return roomGrid.AsValueEnumerable()
                .Where(kvp => kvp.Value.isBed)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// Y 좌표로부터 층 레벨 계산
        /// </summary>
        private int GetFloorLevel(float yPosition)
        {
            // 간단한 층 계산 (4유닛마다 한 층)
            return Mathf.Max(1, Mathf.FloorToInt(yPosition / 4f) + 1);
        }

        /// <summary>
        /// 그리드 정보 반환 (디버그용)
        /// </summary>
        public string GetGridInfo()
        {
            int floorCount = 0, wallCount = 0, doorCount = 0, bedCount = 0;
            
            foreach (var cell in roomGrid.Values)
            {
                if (cell.isFloor) floorCount++;
                if (cell.isWall) wallCount++;
                if (cell.isDoor) doorCount++;
                if (cell.isBed) bedCount++;
            }
            
            return $"그리드 셀: {roomGrid.Count}개 (바닥: {floorCount}, 벽: {wallCount}, 문: {doorCount}, 침대: {bedCount})";
        }
    }

    /// <summary>
    /// 셀 타입 열거형
    /// </summary>
    public enum CellType
    {
        Empty,
        Floor,
        Wall,
        Door,
        Bed
    }

    /// <summary>
    /// 방 셀 정보
    /// </summary>
    public class RoomCell
    {
        public Vector3Int position;
        public float worldHeight;
        public bool isFloor;
        public bool isWall;
        public bool isDoor;
        public bool isBed;
        public List<GameObject> objects = new List<GameObject>();
    }
}