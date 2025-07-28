using UnityEngine;
using System.Collections.Generic;
using System;

namespace JY
{
    /// <summary>
    /// 방 셀 데이터
    /// </summary>
    [Serializable]
    public class RoomCell
    {
        public Vector3Int gridPosition;
        public bool isFloor = false;
        public bool isWall = false;
        public bool isDoor = false;
        public bool isBed = false;
        public bool isSunbed = false;
        public bool isProcessed = false;
        
        public GameObject floorObject;
        public GameObject wallObject;
        public GameObject doorObject;
        public GameObject bedObject;
        public GameObject sunbedObject;
        
        public RoomCell(Vector3Int position)
        {
            gridPosition = position;
        }
        
        public bool HasAnyContent()
        {
            return isFloor || isWall || isDoor || isBed || isSunbed;
        }
        
        public bool IsRoomElement()
        {
            return isFloor || isWall || isDoor;
        }
        
        public void Reset()
        {
            isFloor = isWall = isDoor = isBed = isSunbed = isProcessed = false;
            floorObject = wallObject = doorObject = bedObject = sunbedObject = null;
        }
    }
    
    /// <summary>
    /// 방 정보 데이터
    /// </summary>
    [Serializable]
    public class RoomInfo
    {
        [Header("기본 정보")]
        public string roomId;
        public Vector3 center;
        public Bounds bounds;
        public int floorLevel = 1;
        
        [Header("구성 요소")]
        public List<Vector3Int> floorCells = new List<Vector3Int>();
        public List<GameObject> walls = new List<GameObject>();
        public List<GameObject> doors = new List<GameObject>();
        public List<GameObject> beds = new List<GameObject>();
        public List<GameObject> sunbeds = new List<GameObject>();
        
        [Header("방 속성")]
        public bool isValid = false;
        public bool isSunbedRoom = false;
        public float calculatedPrice = 0f;
        public float calculatedReputation = 0f;
        public float fixedPrice = 0f;
        public float fixedReputation = 0f;
        
        [Header("게임 오브젝트")]
        public GameObject gameObject;
        
        public RoomInfo()
        {
            roomId = System.Guid.NewGuid().ToString();
        }
        
        public RoomInfo(string id)
        {
            roomId = id;
        }
        
        /// <summary>
        /// 방 유효성 검사
        /// </summary>
        public bool ValidateRoom(int minWalls, int minDoors, int minBeds)
        {
            if (isSunbedRoom)
            {
                isValid = sunbeds.Count > 0;
            }
            else
            {
                isValid = walls.Count >= minWalls && 
                         doors.Count >= minDoors && 
                         beds.Count >= minBeds &&
                         floorCells.Count > 0;
            }
            
            return isValid;
        }
        
        /// <summary>
        /// 최종 가격 반환
        /// </summary>
        public float GetFinalPrice()
        {
            return fixedPrice > 0 ? fixedPrice : calculatedPrice;
        }
        
        /// <summary>
        /// 최종 명성도 반환
        /// </summary>
        public float GetFinalReputation()
        {
            return fixedReputation > 0 ? fixedReputation : calculatedReputation;
        }
        
        /// <summary>
        /// 방 크기 반환
        /// </summary>
        public int GetRoomSize()
        {
            return floorCells.Count;
        }
        
        /// <summary>
        /// 방 정보 요약
        /// </summary>
        public string GetSummary()
        {
            return $"방 {roomId}: " +
                   $"크기 {GetRoomSize()}, " +
                   $"벽 {walls.Count}, " +
                   $"문 {doors.Count}, " +
                   $"침대 {beds.Count}, " +
                   $"선베드 {sunbeds.Count}, " +
                   $"가격 {GetFinalPrice():F0}, " +
                   $"명성 {GetFinalReputation():F0}, " +
                   $"유효 {isValid}";
        }
    }
    
    /// <summary>
    /// 방 스캔 설정
    /// </summary>
    [Serializable]
    public class RoomScanSettings
    {
        [Header("인식 조건")]
        [Range(1, 20)] public int minWalls = RoomConstants.DEFAULT_MIN_WALLS;
        [Range(1, 10)] public int minDoors = RoomConstants.DEFAULT_MIN_DOORS;
        [Range(1, 10)] public int minBeds = RoomConstants.DEFAULT_MIN_BEDS;
        
        [Header("성능 설정")]
        [Range(100, 5000)] public int maxFloodFillIterations = RoomConstants.DEFAULT_MAX_FLOOD_FILL_ITERATIONS;
        [Range(50, 1000)] public int maxRoomSize = RoomConstants.DEFAULT_MAX_ROOM_SIZE;
        
        [Header("스캔 설정")]
        [Range(0.5f, 10f)] public float scanInterval = RoomConstants.DEFAULT_SCAN_INTERVAL;
        public LayerMask roomElementsLayer = -1;
        
        [Header("층별 설정")]
        [Range(0.1f, 10f)] public float floorHeightTolerance = FloorConstants.FLOOR_TOLERANCE;
        [Range(-2f, 2f)] public float floorDetectionOffset = FloorConstants.FLOOR_DETECTION_OFFSET;
        [Range(1, 10)] public int currentScanFloor = 1;
        [Range(1, 20)] public int maxFloors = 5;
        [Tooltip("모든 층 스캔 여부 (false면 currentScanFloor만 스캔)")]
        public bool scanAllFloors = false; // 기본값을 false로 변경하여 현재 활성 층만 스캔
        
        [Header("선베드 방 설정")]
        public bool enableSunbedRooms = true;
        [Range(0f, 10000f)] public float sunbedRoomPrice = RoomConstants.DEFAULT_SUNBED_ROOM_PRICE;
        [Range(0f, 1000f)] public float sunbedRoomReputation = RoomConstants.DEFAULT_SUNBED_ROOM_REPUTATION;
        
        [Header("디버그 설정")]
        public bool showDebugLogs = true;
        public bool showImportantLogsOnly = false;
        public bool showScanLogs = true;
    }
    
    /// <summary>
    /// 방 스캔 결과
    /// </summary>
    [Serializable]
    public class RoomScanResult
    {
        public List<RoomInfo> detectedRooms = new List<RoomInfo>();
        public List<RoomInfo> validRooms = new List<RoomInfo>();
        public List<RoomInfo> invalidRooms = new List<RoomInfo>();
        public int totalScannedCells = 0;
        public float scanDuration = 0f;
        public string scanStatus = "준비됨";
        public DateTime lastScanTime = DateTime.Now;
        
        public int ValidRoomCount => validRooms.Count;
        public int InvalidRoomCount => invalidRooms.Count;
        public int TotalRoomCount => detectedRooms.Count;
        
        public void Clear()
        {
            detectedRooms.Clear();
            validRooms.Clear();
            invalidRooms.Clear();
            totalScannedCells = 0;
            scanDuration = 0f;
            scanStatus = "초기화됨";
        }
        
        public string GetSummary()
        {
            return $"스캔 결과: 총 {TotalRoomCount}개 방 (유효 {ValidRoomCount}, 무효 {InvalidRoomCount}), " +
                   $"스캔 시간 {scanDuration:F2}초, 마지막 스캔 {lastScanTime:yyyy-MM-dd HH:mm:ss}";
        }
    }
} 