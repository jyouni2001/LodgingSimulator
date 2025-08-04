using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace JY.RoomDetection
{
    /// <summary>
    /// 방 감지의 핵심 로직을 담당하는 클래스
    /// FloodFill 알고리즘을 사용하여 방을 감지하되 성능을 최적화
    /// </summary>
    public class RoomDetectionCore
    {
        [System.Serializable]
        public class DetectionSettings
        {
            [Header("성능 설정")]
            public int maxFloodFillIterations = 1000;
            public int maxRoomSize = 200;
            
            [Header("방 인식 조건")]
            public int minWalls = 3;
            public int minDoors = 1;
            public int minBeds = 1;
            
            [Header("허용 오차")]
            public float floorHeightTolerance = 1.0f;
            public float floorDetectionOffset = -0.5f;
        }

        private DetectionSettings settings;
        private RoomGridManager gridManager;
        
        public RoomDetectionCore(DetectionSettings detectionSettings, RoomGridManager roomGridManager)
        {
            settings = detectionSettings;
            gridManager = roomGridManager;
        }

        /// <summary>
        /// 침대 위치에서 시작하여 방을 감지
        /// </summary>
        public RoomInfo DetectRoomFromBed(Vector3Int bedPosition, HashSet<Vector3Int> globalVisitedCells)
        {
            // 이미 처리된 셀인지 확인
            if (globalVisitedCells.Contains(bedPosition))
            {
                return null;
            }

            AIDebugLogger.Log("RoomDetection", $"침대 {bedPosition}에서 방 감지 시작", LogCategory.Room);

            // 방 정보 수집
            var roomData = new RoomData();
            var localVisitedCells = new HashSet<Vector3Int>();
            
            // FloodFill로 방 영역 탐색
            bool success = FloodFillRoomArea(bedPosition, roomData, localVisitedCells, globalVisitedCells);
            
            if (!success)
            {
                AIDebugLogger.LogWarning("RoomDetection", $"FloodFill 실패: {bedPosition}");
                return null;
            }

            // 방 유효성 검사
            if (!ValidateRoom(roomData))
            {
                AIDebugLogger.Log("RoomDetection", $"방 유효성 검사 실패: {bedPosition}");
                return null;
            }

            // RoomInfo 생성
            RoomInfo roomInfo = CreateRoomInfo(roomData, bedPosition);
            
            AIDebugLogger.LogRoomAction("RoomDetection", "감지 완료", -1);
            return roomInfo;
        }

        /// <summary>
        /// FloodFill 알고리즘으로 방 영역 탐색 (최적화된 버전)
        /// </summary>
        private bool FloodFillRoomArea(Vector3Int startPos, RoomData roomData, 
            HashSet<Vector3Int> localVisited, HashSet<Vector3Int> globalVisited)
        {
            var queue = new Queue<Vector3Int>();
            var directions = GetCardinalDirections();
            
            queue.Enqueue(startPos);
            localVisited.Add(startPos);
            globalVisited.Add(startPos);
            
            int iterations = 0;
            
            while (queue.Count > 0 && iterations < settings.maxFloodFillIterations && 
                   roomData.FloorCells.Count < settings.maxRoomSize)
            {
                iterations++;
                Vector3Int current = queue.Dequeue();
                
                // 현재 셀 분석
                AnalyzeCell(current, roomData);
                
                // 인접 셀들 탐색
                foreach (var direction in directions)
                {
                    Vector3Int neighbor = current + direction;
                    
                    if (localVisited.Contains(neighbor) || globalVisited.Contains(neighbor))
                        continue;
                    
                    CellType cellType = gridManager.GetCellType(neighbor);
                    
                    // 벽이나 문이면 경계로 추가하고 탐색 중단
                    if (cellType == CellType.Wall)
                    {
                        roomData.WallPositions.Add(neighbor);
                        continue;
                    }
                    
                    if (cellType == CellType.Door)
                    {
                        roomData.DoorPositions.Add(neighbor);
                        continue;
                    }
                    
                    // 바닥이나 침대면 방 영역으로 확장
                    if (cellType == CellType.Floor || cellType == CellType.Bed)
                    {
                        localVisited.Add(neighbor);
                        globalVisited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
            
            // 반복 한계 체크
            if (iterations >= settings.maxFloodFillIterations)
            {
                AIDebugLogger.LogWarning("RoomDetection", $"최대 반복 횟수 초과: {startPos}");
                return false;
            }
            
            if (roomData.FloorCells.Count >= settings.maxRoomSize)
            {
                AIDebugLogger.LogWarning("RoomDetection", $"최대 방 크기 초과: {startPos}");
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// 셀 분석하여 방 데이터에 추가
        /// </summary>
        private void AnalyzeCell(Vector3Int position, RoomData roomData)
        {
            CellType cellType = gridManager.GetCellType(position);
            
            switch (cellType)
            {
                case CellType.Floor:
                    roomData.FloorCells.Add(position);
                    break;
                case CellType.Bed:
                    roomData.BedPositions.Add(position);
                    roomData.FloorCells.Add(position); // 침대도 바닥으로 처리
                    break;
                case CellType.Wall:
                    roomData.WallPositions.Add(position);
                    break;
                case CellType.Door:
                    roomData.DoorPositions.Add(position);
                    break;
            }
        }

        /// <summary>
        /// 방 유효성 검사
        /// </summary>
        private bool ValidateRoom(RoomData roomData)
        {
            bool hasEnoughWalls = roomData.WallPositions.Count >= settings.minWalls;
            bool hasEnoughDoors = roomData.DoorPositions.Count >= settings.minDoors;
            bool hasEnoughBeds = roomData.BedPositions.Count >= settings.minBeds;
            bool hasFloors = roomData.FloorCells.Count > 0;
            
            return hasEnoughWalls && hasEnoughDoors && hasEnoughBeds && hasFloors;
        }

        /// <summary>
        /// RoomInfo 객체 생성
        /// </summary>
        private RoomInfo CreateRoomInfo(RoomData roomData, Vector3Int centerPos)
        {
            var roomInfo = new RoomInfo();
            
            // 기본 정보 설정
            Vector3 worldCenter = gridManager.GridToWorldPosition(centerPos);
            roomInfo.center = worldCenter;
            roomInfo.roomId = $"Room_{centerPos.x}_{centerPos.z}";
            
            // 바운더리 계산
            roomInfo.bounds = CalculateRoomBounds(roomData.FloorCells);
            
            // 게임오브젝트 수집
            CollectRoomGameObjects(roomData, roomInfo);
            
            // 플로어 셀 저장
            roomInfo.floorCells = roomData.FloorCells.ToList();
            
            return roomInfo;
        }

        /// <summary>
        /// 방의 경계 계산
        /// </summary>
        private Bounds CalculateRoomBounds(HashSet<Vector3Int> floorCells)
        {
            if (floorCells.Count == 0)
                return new Bounds();
            
            Vector3Int min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            Vector3Int max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            
            foreach (var cell in floorCells)
            {
                if (cell.x < min.x) min.x = cell.x;
                if (cell.y < min.y) min.y = cell.y;
                if (cell.z < min.z) min.z = cell.z;
                
                if (cell.x > max.x) max.x = cell.x;
                if (cell.y > max.y) max.y = cell.y;
                if (cell.z > max.z) max.z = cell.z;
            }
            
            Vector3 worldMin = gridManager.GridToWorldPosition(min);
            Vector3 worldMax = gridManager.GridToWorldPosition(max);
            Vector3 center = (worldMin + worldMax) * 0.5f;
            Vector3 size = worldMax - worldMin + Vector3.one; // 1유닛 여유
            
            return new Bounds(center, size);
        }

        /// <summary>
        /// 방 게임오브젝트 수집
        /// </summary>
        private void CollectRoomGameObjects(RoomData roomData, RoomInfo roomInfo)
        {
            // 벽 오브젝트 수집
            foreach (var wallPos in roomData.WallPositions)
            {
                var wallObjects = gridManager.GetObjectsAt(wallPos);
                foreach (var obj in wallObjects)
                {
                    if (obj != null && !roomInfo.walls.Contains(obj))
                    {
                        roomInfo.walls.Add(obj);
                    }
                }
            }
            
            // 문 오브젝트 수집
            foreach (var doorPos in roomData.DoorPositions)
            {
                var doorObjects = gridManager.GetObjectsAt(doorPos);
                foreach (var obj in doorObjects)
                {
                    if (obj != null && !roomInfo.doors.Contains(obj))
                    {
                        roomInfo.doors.Add(obj);
                    }
                }
            }
            
            // 침대 오브젝트 수집
            foreach (var bedPos in roomData.BedPositions)
            {
                var bedObjects = gridManager.GetObjectsAt(bedPos);
                foreach (var obj in bedObjects)
                {
                    if (obj != null && !roomInfo.beds.Contains(obj))
                    {
                        roomInfo.beds.Add(obj);
                    }
                }
            }
        }

        /// <summary>
        /// 기본 4방향 반환 (성능 최적화)
        /// </summary>
        private Vector3Int[] GetCardinalDirections()
        {
            return new Vector3Int[]
            {
                Vector3Int.forward,  // +Z
                Vector3Int.back,     // -Z
                Vector3Int.right,    // +X
                Vector3Int.left      // -X
            };
        }
    }

    /// <summary>
    /// 방 감지 중 임시로 사용하는 데이터 구조
    /// </summary>
    public class RoomData
    {
        public HashSet<Vector3Int> FloorCells { get; } = new HashSet<Vector3Int>();
        public HashSet<Vector3Int> WallPositions { get; } = new HashSet<Vector3Int>();
        public HashSet<Vector3Int> DoorPositions { get; } = new HashSet<Vector3Int>();
        public HashSet<Vector3Int> BedPositions { get; } = new HashSet<Vector3Int>();
    }
}