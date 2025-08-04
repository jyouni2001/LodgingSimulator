using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace JY.RoomDetection
{
    /// <summary>
    /// 선베드 방 처리를 담당하는 클래스
    /// 선베드 오브젝트를 찾아서 특별한 방으로 생성
    /// </summary>
    public class SunbedRoomProcessor
    {
        [System.Serializable]
        public class SunbedSettings
        {
            [Header("선베드 방 설정")]
            public bool enableSunbedRooms = true;
            public float sunbedRoomPrice = 100f;
            public float sunbedRoomReputation = 50f;
            public float detectionRadius = 5f;
        }

        private SunbedSettings settings;
        private RoomGridManager gridManager;

        public SunbedRoomProcessor(SunbedSettings sunbedSettings, RoomGridManager roomGridManager)
        {
            settings = sunbedSettings;
            gridManager = roomGridManager;
        }

        /// <summary>
        /// 씬에서 선베드 방들을 찾아서 생성
        /// </summary>
        public List<RoomInfo> FindSunbedRooms(List<RoomInfo> existingRooms)
        {
            if (!settings.enableSunbedRooms)
            {
                return new List<RoomInfo>();
            }

            AIDebugLogger.Log("SunbedProcessor", "선베드 방 탐색 시작", LogCategory.Room);

            var sunbedRooms = new List<RoomInfo>();
            var sunbedObjects = GameObject.FindGameObjectsWithTag("Sunbed");

            foreach (var sunbed in sunbedObjects)
            {
                // 기존 방과 겹치는지 확인
                if (IsOverlappingWithExistingRooms(sunbed.transform.position, existingRooms))
                {
                    AIDebugLogger.Log("SunbedProcessor", $"선베드 {sunbed.name}은 기존 방과 겹쳐서 제외", LogCategory.Room);
                    continue;
                }

                RoomInfo sunbedRoom = CreateSunbedRoom(sunbed);
                if (sunbedRoom != null)
                {
                    sunbedRooms.Add(sunbedRoom);
                    AIDebugLogger.LogRoomAction("SunbedProcessor", $"선베드 방 생성: {sunbed.name}", -1);
                }
            }

            AIDebugLogger.Log("SunbedProcessor", $"선베드 방 탐색 완료: {sunbedRooms.Count}개 생성", LogCategory.Room);
            return sunbedRooms;
        }

        /// <summary>
        /// 선베드 오브젝트로부터 방 정보 생성
        /// </summary>
        private RoomInfo CreateSunbedRoom(GameObject sunbed)
        {
            var roomInfo = new RoomInfo();
            
            // 기본 정보 설정
            Vector3 sunbedPos = sunbed.transform.position;
            roomInfo.center = sunbedPos;
            roomInfo.roomId = $"SunbedRoom_{sunbedPos.x:F0}_{sunbedPos.z:F0}";
            
            // 선베드 방으로 설정
            roomInfo.SetAsSunbedRoom(settings.sunbedRoomPrice, settings.sunbedRoomReputation);
            
            // 침대 목록에 선베드 추가
            roomInfo.beds.Add(sunbed);
            
            // 주변 오브젝트들 수집
            CollectSurroundingObjects(sunbedPos, roomInfo);
            
            // 바운더리 계산 (선베드 중심으로 고정 크기)
            Vector3 boundsSize = Vector3.one * settings.detectionRadius;
            roomInfo.bounds = new Bounds(sunbedPos, boundsSize);
            
            // 가상의 플로어 셀 생성 (선베드 위치 기준)
            Vector3Int gridPos = gridManager.WorldToGridPosition(sunbedPos, CellType.Bed);
            roomInfo.floorCells.Add(gridPos);
            
            return roomInfo;
        }

        /// <summary>
        /// 선베드 주변의 오브젝트들 수집
        /// </summary>
        private void CollectSurroundingObjects(Vector3 center, RoomInfo roomInfo)
        {
            // 탐지 반경 내의 모든 콜라이더 찾기
            Collider[] nearbyObjects = Physics.OverlapSphere(center, settings.detectionRadius);
            
            foreach (var collider in nearbyObjects)
            {
                GameObject obj = collider.gameObject;
                string tag = obj.tag;
                
                // 태그에 따라 분류
                switch (tag)
                {
                    case "Wall":
                        if (!roomInfo.walls.Contains(obj))
                            roomInfo.walls.Add(obj);
                        break;
                    case "Door":
                        if (!roomInfo.doors.Contains(obj))
                            roomInfo.doors.Add(obj);
                        break;
                    case "Bed":
                        if (obj != roomInfo.beds[0] && !roomInfo.beds.Contains(obj)) // 선베드 자신 제외
                            roomInfo.beds.Add(obj);
                        break;
                }
            }
            
            AIDebugLogger.Log("SunbedProcessor", 
                $"선베드 {roomInfo.roomId} 주변 오브젝트: 벽 {roomInfo.walls.Count}개, 문 {roomInfo.doors.Count}개", 
                LogCategory.Room);
        }

        /// <summary>
        /// 기존 방들과 겹치는지 확인
        /// </summary>
        private bool IsOverlappingWithExistingRooms(Vector3 sunbedPosition, List<RoomInfo> existingRooms)
        {
            foreach (var room in existingRooms)
            {
                if (room.ContainsPosition(sunbedPosition))
                {
                    return true;
                }
                
                // 중심점 거리로도 확인
                float distance = Vector3.Distance(sunbedPosition, room.center);
                if (distance < settings.detectionRadius)
                {
                    return true;
                }
            }
            
            return false;
        }
    }
}