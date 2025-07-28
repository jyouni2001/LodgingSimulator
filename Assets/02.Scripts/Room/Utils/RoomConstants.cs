using UnityEngine;

namespace JY
{
    /// <summary>
    /// 방 시스템에서 사용하는 상수들
    /// </summary>
    public static class RoomConstants
    {
        // 방 인식 조건
        public const int DEFAULT_MIN_WALLS = 3;
        public const int DEFAULT_MIN_DOORS = 1;
        public const int DEFAULT_MIN_BEDS = 1;
        
        // 성능 및 안전 설정
        public const int DEFAULT_MAX_FLOOD_FILL_ITERATIONS = 1000;
        public const int DEFAULT_MAX_ROOM_SIZE = 200;
        
        // 스캔 설정
        public const float DEFAULT_SCAN_INTERVAL = 2f;
        
        // 선베드 방 설정
        public const float DEFAULT_SUNBED_ROOM_PRICE = 100f;
        public const float DEFAULT_SUNBED_ROOM_REPUTATION = 50f;
        
        // 태그 이름들
        public const string TAG_FLOOR = "Floor";
        public const string TAG_WALL = "Wall";
        public const string TAG_DOOR = "Door";
        public const string TAG_BED = "Bed";
        public const string TAG_SUNBED = "Sunbed";
        public const string TAG_ROOM = "Room";
        
        // 레이어 마스크
        public const string LAYER_ROOM_ELEMENTS = "RoomElements";
        
        // 방향 벡터 (8방향)
        public static readonly Vector3Int[] DIRECTIONS_8 = new Vector3Int[]
        {
            new Vector3Int(0, 0, 1),   // 북
            new Vector3Int(1, 0, 1),   // 북동
            new Vector3Int(1, 0, 0),   // 동
            new Vector3Int(1, 0, -1),  // 남동
            new Vector3Int(0, 0, -1),  // 남
            new Vector3Int(-1, 0, -1), // 남서
            new Vector3Int(-1, 0, 0),  // 서
            new Vector3Int(-1, 0, 1)   // 북서
        };
        
        // 방향 벡터 (4방향)
        public static readonly Vector3Int[] DIRECTIONS_4 = new Vector3Int[]
        {
            new Vector3Int(0, 0, 1),   // 북
            new Vector3Int(1, 0, 0),   // 동
            new Vector3Int(0, 0, -1),  // 남
            new Vector3Int(-1, 0, 0)   // 서
        };
    }
} 