using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace JY
{
    /// <summary>
    /// 방 유효성 검사를 담당하는 클래스
    /// 방이 게임 규칙에 맞는지 확인하고 분류
    /// </summary>
    public class RoomValidator
    {
        private readonly RoomScanSettings settings;
        
        public RoomValidator(RoomScanSettings scanSettings)
        {
            settings = scanSettings;
        }
        
        /// <summary>
        /// 방 리스트 유효성 검사
        /// </summary>
        public RoomScanResult ValidateRooms(List<RoomInfo> detectedRooms)
        {
            var result = new RoomScanResult();
            result.detectedRooms = detectedRooms;
            result.scanStatus = "유효성 검사 중...";
            
            foreach (var room in detectedRooms)
            {
                if (ValidateRoom(room))
                {
                    result.validRooms.Add(room);
                }
                else
                {
                    result.invalidRooms.Add(room);
                }
            }
            
            result.scanStatus = "유효성 검사 완료";
            
            RoomUtilities.DebugLog($"유효성 검사 완료: 유효 {result.validRooms.Count}개, 무효 {result.invalidRooms.Count}개", 
                                 true, settings);
            
            return result;
        }
        
        /// <summary>
        /// 단일 방 유효성 검사
        /// </summary>
        public bool ValidateRoom(RoomInfo room)
        {
            if (room == null)
            {
                return false;
            }
            
            // 선베드 방 특별 처리
            if (room.isSunbedRoom)
            {
                return ValidateSunbedRoom(room);
            }
            
            // 일반 방 유효성 검사
            return ValidateRegularRoom(room);
        }
        
        /// <summary>
        /// 일반 방 유효성 검사
        /// </summary>
        private bool ValidateRegularRoom(RoomInfo room)
        {
            var validationResults = new List<(bool isValid, string reason)>();
            
            // 1. 기본 구조 검사
            validationResults.Add(ValidateBasicStructure(room));
            
            // 2. 크기 검사
            validationResults.Add(ValidateRoomSize(room));
            
            // 3. 방 구성 요소 검사
            validationResults.Add(ValidateRoomComponents(room));
            
            // 4. 접근성 검사
            validationResults.Add(ValidateAccessibility(room));
            
            // 5. 경계 검사
            validationResults.Add(ValidateBounds(room));
            
            // 모든 검사 결과 확인
            bool isValid = validationResults.All(result => result.isValid);
            
            // 실패 사유 로그
            if (!isValid)
            {
                var failedChecks = validationResults.Where(result => !result.isValid)
                                                  .Select(result => result.reason);
                string failureReasons = string.Join(", ", failedChecks);
                
                RoomUtilities.DebugLog($"방 {room.roomId} 유효성 검사 실패: {failureReasons}", false, settings);
            }
            else
            {
                RoomUtilities.DebugLog($"방 {room.roomId} 유효성 검사 통과", false, settings);
            }
            
            room.isValid = isValid;
            return isValid;
        }
        
        /// <summary>
        /// 선베드 방 유효성 검사
        /// </summary>
        private bool ValidateSunbedRoom(RoomInfo room)
        {
            bool isValid = room.sunbeds.Count > 0 && room.floorCells.Count > 0;
            
            if (!isValid)
            {
                RoomUtilities.DebugLog($"선베드 방 {room.roomId} 유효성 검사 실패: " +
                                     $"선베드 {room.sunbeds.Count}개, 바닥 셀 {room.floorCells.Count}개", false, settings);
            }
            else
            {
                RoomUtilities.DebugLog($"선베드 방 {room.roomId} 유효성 검사 통과", false, settings);
            }
            
            room.isValid = isValid;
            return isValid;
        }
        
        /// <summary>
        /// 기본 구조 검사
        /// </summary>
        private (bool isValid, string reason) ValidateBasicStructure(RoomInfo room)
        {
            if (room.floorCells.Count == 0)
            {
                return (false, "바닥 셀 없음");
            }
            
            if (room.bounds.size.magnitude <= 0)
            {
                return (false, "유효하지 않은 경계");
            }
            
            return (true, "기본 구조 검사 통과");
        }
        
        /// <summary>
        /// 방 크기 검사
        /// </summary>
        private (bool isValid, string reason) ValidateRoomSize(RoomInfo room)
        {
            int roomSize = room.GetRoomSize();
            
            if (roomSize < 1)
            {
                return (false, "방 크기 너무 작음");
            }
            
            if (roomSize > settings.maxRoomSize)
            {
                return (false, $"방 크기 너무 큼 ({roomSize} > {settings.maxRoomSize})");
            }
            
            return (true, "방 크기 검사 통과");
        }
        
        /// <summary>
        /// 방 구성 요소 검사
        /// </summary>
        private (bool isValid, string reason) ValidateRoomComponents(RoomInfo room)
        {
            // 벽 개수 검사
            if (room.walls.Count < settings.minWalls)
            {
                return (false, $"벽 부족 ({room.walls.Count} < {settings.minWalls})");
            }
            
            // 문 개수 검사
            if (room.doors.Count < settings.minDoors)
            {
                return (false, $"문 부족 ({room.doors.Count} < {settings.minDoors})");
            }
            
            // 침대 개수 검사
            if (room.beds.Count < settings.minBeds)
            {
                return (false, $"침대 부족 ({room.beds.Count} < {settings.minBeds})");
            }
            
            return (true, "구성 요소 검사 통과");
        }
        
        /// <summary>
        /// 접근성 검사 (문을 통한 진입 가능성)
        /// </summary>
        private (bool isValid, string reason) ValidateAccessibility(RoomInfo room)
        {
            if (room.doors.Count == 0)
            {
                return (false, "문이 없어 접근 불가능");
            }
            
            // 문이 방 경계에 있는지 확인
            bool hasAccessibleDoor = false;
            
            foreach (var door in room.doors)
            {
                if (door == null) continue;
                
                // 문이 방 영역과 연결되어 있는지 확인
                if (IsDoorAccessible(door, room))
                {
                    hasAccessibleDoor = true;
                    break;
                }
            }
            
            if (!hasAccessibleDoor)
            {
                return (false, "접근 가능한 문 없음");
            }
            
            return (true, "접근성 검사 통과");
        }
        
        /// <summary>
        /// 경계 검사
        /// </summary>
        private (bool isValid, string reason) ValidateBounds(RoomInfo room)
        {
            // 경계가 너무 작지 않은지 확인
            if (room.bounds.size.x < 0.5f || room.bounds.size.z < 0.5f)
            {
                return (false, "방 경계 너무 작음");
            }
            
            // 경계가 너무 크지 않은지 확인 (성능상 이유)
            if (room.bounds.size.x > 50f || room.bounds.size.z > 50f)
            {
                return (false, "방 경계 너무 큼");
            }
            
            return (true, "경계 검사 통과");
        }
        
        /// <summary>
        /// 문이 접근 가능한지 확인
        /// </summary>
        private bool IsDoorAccessible(GameObject door, RoomInfo room)
        {
            if (door == null) return false;
            
            Vector3 doorPosition = door.transform.position;
            
            // 문이 방 경계 근처에 있는지 확인
            return room.bounds.Contains(doorPosition) || 
                   IsPositionNearBounds(doorPosition, room.bounds, 2f);
        }
        
        /// <summary>
        /// 위치가 경계 근처에 있는지 확인
        /// </summary>
        private bool IsPositionNearBounds(Vector3 position, Bounds bounds, float tolerance)
        {
            Vector3 closestPoint = bounds.ClosestPoint(position);
            return Vector3.Distance(position, closestPoint) <= tolerance;
        }
        
        /// <summary>
        /// 방 품질 점수 계산 (정렬 및 우선순위 결정용)
        /// </summary>
        public float CalculateRoomQualityScore(RoomInfo room)
        {
            if (!room.isValid) return 0f;
            
            float score = 0f;
            
            // 기본 점수 (크기 기반)
            score += room.GetRoomSize() * 10f;
            
            // 구성 요소 보너스
            score += room.beds.Count * 50f;
            score += room.doors.Count * 20f;
            score += Mathf.Min(room.walls.Count, 10) * 5f; // 벽은 10개까지만 점수
            
            // 선베드 방 보너스
            if (room.isSunbedRoom)
            {
                score += 100f;
            }
            
            // 가격 기반 보너스 (비싸면 품질이 좋다고 가정)
            score += room.GetFinalPrice() * 0.1f;
            
            return score;
        }
        
        /// <summary>
        /// 방 리스트를 품질 순으로 정렬
        /// </summary>
        public List<RoomInfo> SortRoomsByQuality(List<RoomInfo> rooms)
        {
            return rooms.OrderByDescending(room => CalculateRoomQualityScore(room)).ToList();
        }
        
        /// <summary>
        /// 유효성 검사 설정 확인
        /// </summary>
        public bool ValidateSettings()
        {
            if (settings.minWalls < 0 || settings.minDoors < 0 || settings.minBeds < 0)
            {
                RoomUtilities.DebugLog("유효성 검사 설정 오류: 최소값이 음수입니다", true, settings);
                return false;
            }
            
            if (settings.maxRoomSize <= 0)
            {
                RoomUtilities.DebugLog("유효성 검사 설정 오류: 최대 방 크기가 0 이하입니다", true, settings);
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// 방 유효성 통계 반환
        /// </summary>
        public string GetValidationStatistics(RoomScanResult result)
        {
            if (result.TotalRoomCount == 0)
            {
                return "검사할 방이 없습니다.";
            }
            
            float validPercentage = (float)result.ValidRoomCount / result.TotalRoomCount * 100f;
            
            return $"유효성 검사 통계:\n" +
                   $"- 총 방 수: {result.TotalRoomCount}\n" +
                   $"- 유효한 방: {result.ValidRoomCount} ({validPercentage:F1}%)\n" +
                   $"- 무효한 방: {result.InvalidRoomCount}\n" +
                   $"- 평균 품질 점수: {result.validRooms.Select(CalculateRoomQualityScore).DefaultIfEmpty(0).Average():F1}";
        }
    }
} 