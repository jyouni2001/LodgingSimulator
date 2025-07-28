using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace JY
{
    /// <summary>
    /// 방 데이터 관리를 담당하는 클래스 (인터페이스 구현)
    /// 방 정보의 저장, 검색, 업데이트를 관리
    /// </summary>
    public class RoomDataManager : IRoomDataManager
    {
        private readonly RoomScanSettings settings;
        private List<RoomInfo> allRooms = new List<RoomInfo>();
        private List<RoomInfo> validRooms = new List<RoomInfo>();
        private List<RoomInfo> invalidRooms = new List<RoomInfo>();
        
        // 이벤트
        public event Action<List<RoomInfo>> OnRoomsUpdated;
        public event Action<RoomInfo> OnRoomAdded;
        public event Action<RoomInfo> OnRoomRemoved;
        public event Action<RoomInfo> OnRoomValidationChanged;
        
        public RoomDataManager(RoomScanSettings scanSettings)
        {
            settings = scanSettings;
        }
        
        #region 방 데이터 관리
        
        /// <summary>
        /// 방 리스트 업데이트
        /// </summary>
        public void UpdateRooms(RoomScanResult scanResult)
        {
            if (scanResult == null)
            {
                RoomUtilities.DebugLog("스캔 결과가 null입니다.", true, settings);
                return;
            }
            
            // 기존 데이터 백업
            var oldRooms = new List<RoomInfo>(allRooms);
            
            // 새 데이터로 업데이트
            allRooms = scanResult.detectedRooms ?? new List<RoomInfo>();
            validRooms = scanResult.validRooms ?? new List<RoomInfo>();
            invalidRooms = scanResult.invalidRooms ?? new List<RoomInfo>();
            
            // 변경 사항 분석 및 이벤트 발생
            AnalyzeChanges(oldRooms, allRooms);
            
            OnRoomsUpdated?.Invoke(allRooms);
            
            RoomUtilities.DebugLog($"방 데이터 업데이트 완료: 총 {allRooms.Count}개 " +
                                 $"(유효 {validRooms.Count}, 무효 {invalidRooms.Count})", true, settings);
        }
        
        /// <summary>
        /// 단일 방 추가
        /// </summary>
        public void AddRoom(RoomInfo room)
        {
            if (room == null) return;
            
            // 중복 확인
            if (allRooms.Any(r => r.roomId == room.roomId))
            {
                RoomUtilities.DebugLog($"이미 존재하는 방 ID: {room.roomId}", false, settings);
                return;
            }
            
            allRooms.Add(room);
            
            if (room.isValid)
            {
                validRooms.Add(room);
            }
            else
            {
                invalidRooms.Add(room);
            }
            
            OnRoomAdded?.Invoke(room);
            OnRoomsUpdated?.Invoke(allRooms);
            
            RoomUtilities.DebugLog($"방 추가됨: {room.roomId}", false, settings);
        }
        
        /// <summary>
        /// 방 제거
        /// </summary>
        public bool RemoveRoom(string roomId)
        {
            var room = GetRoomById(roomId);
            if (room == null) return false;
            
            allRooms.Remove(room);
            validRooms.Remove(room);
            invalidRooms.Remove(room);
            
            OnRoomRemoved?.Invoke(room);
            OnRoomsUpdated?.Invoke(allRooms);
            
            RoomUtilities.DebugLog($"방 제거됨: {roomId}", false, settings);
            
            return true;
        }
        
        /// <summary>
        /// 방 유효성 상태 업데이트
        /// </summary>
        public void UpdateRoomValidation(RoomInfo room, bool isValid)
        {
            if (room == null) return;
            
            bool wasValid = room.isValid;
            room.isValid = isValid;
            
            // 리스트 간 이동
            if (wasValid != isValid)
            {
                if (isValid)
                {
                    invalidRooms.Remove(room);
                    if (!validRooms.Contains(room))
                    {
                        validRooms.Add(room);
                    }
                }
                else
                {
                    validRooms.Remove(room);
                    if (!invalidRooms.Contains(room))
                    {
                        invalidRooms.Add(room);
                    }
                }
                
                OnRoomValidationChanged?.Invoke(room);
                
                RoomUtilities.DebugLog($"방 {room.roomId} 유효성 변경: {wasValid} -> {isValid}", false, settings);
            }
        }
        
        #endregion
        
        #region 방 검색 및 조회
        
        /// <summary>
        /// ID로 방 찾기
        /// </summary>
        public RoomInfo GetRoomById(string roomId)
        {
            return allRooms.FirstOrDefault(room => room.roomId == roomId);
        }
        
        /// <summary>
        /// 위치 기반 방 찾기
        /// </summary>
        public RoomInfo GetRoomAtPosition(Vector3 position)
        {
            return allRooms.FirstOrDefault(room => room.bounds.Contains(position));
        }
        
        /// <summary>
        /// 층별 방 조회
        /// </summary>
        public List<RoomInfo> GetRoomsByFloor(int floorLevel)
        {
            return allRooms.Where(room => room.floorLevel == floorLevel).ToList();
        }
        
        /// <summary>
        /// 방 타입별 조회
        /// </summary>
        public List<RoomInfo> GetRoomsByType(bool isSunbedRoom)
        {
            return allRooms.Where(room => room.isSunbedRoom == isSunbedRoom).ToList();
        }
        
        /// <summary>
        /// 가격 범위로 방 조회
        /// </summary>
        public List<RoomInfo> GetRoomsByPriceRange(float minPrice, float maxPrice)
        {
            return validRooms.Where(room => 
            {
                float price = room.GetFinalPrice();
                return price >= minPrice && price <= maxPrice;
            }).ToList();
        }
        
        /// <summary>
        /// 명성도 범위로 방 조회
        /// </summary>
        public List<RoomInfo> GetRoomsByReputationRange(float minReputation, float maxReputation)
        {
            return validRooms.Where(room => 
            {
                float reputation = room.GetFinalReputation();
                return reputation >= minReputation && reputation <= maxReputation;
            }).ToList();
        }
        
        /// <summary>
        /// 모든 방 조회
        /// </summary>
        public List<RoomInfo> GetAllRooms()
        {
            return new List<RoomInfo>(allRooms);
        }
        
        /// <summary>
        /// 유효한 방들만 조회
        /// </summary>
        public List<RoomInfo> GetValidRooms()
        {
            return new List<RoomInfo>(validRooms);
        }
        
        /// <summary>
        /// 무효한 방들만 조회
        /// </summary>
        public List<RoomInfo> GetInvalidRooms()
        {
            return new List<RoomInfo>(invalidRooms);
        }
        
        /// <summary>
        /// 방 필터링
        /// </summary>
        public List<RoomInfo> FilterRooms(Func<RoomInfo, bool> predicate)
        {
            return allRooms.Where(predicate).ToList();
        }
        
        #endregion
        
        #region 통계 및 분석
        
        /// <summary>
        /// 방 통계 정보
        /// </summary>
        public RoomStatistics GetRoomStatistics()
        {
            var stats = new RoomStatistics();
            
            stats.TotalRoomCount = allRooms.Count;
            stats.ValidRoomCount = validRooms.Count;
            stats.InvalidRoomCount = invalidRooms.Count;
            
            if (validRooms.Count > 0)
            {
                stats.AveragePrice = validRooms.Average(room => room.GetFinalPrice());
                stats.AverageReputation = validRooms.Average(room => room.GetFinalReputation());
                stats.AverageSize = validRooms.Average(room => room.GetRoomSize());
                
                stats.MinPrice = validRooms.Min(room => room.GetFinalPrice());
                stats.MaxPrice = validRooms.Max(room => room.GetFinalPrice());
                
                stats.MinReputation = validRooms.Min(room => room.GetFinalReputation());
                stats.MaxReputation = validRooms.Max(room => room.GetFinalReputation());
            }
            
            // 층별 통계
            stats.FloorDistribution = allRooms.GroupBy(room => room.floorLevel)
                                            .ToDictionary(g => g.Key, g => g.Count());
            
            // 타입별 통계
            stats.StandardRoomCount = allRooms.Count(room => !room.isSunbedRoom);
            stats.SunbedRoomCount = allRooms.Count(room => room.isSunbedRoom);
            
            return stats;
        }
        
        /// <summary>
        /// 방 품질 분석
        /// </summary>
        public List<RoomInfo> GetTopQualityRooms(int count = 10)
        {
            var validator = new RoomValidator(settings);
            return validRooms.OrderByDescending(room => validator.CalculateRoomQualityScore(room))
                           .Take(count)
                           .ToList();
        }
        
        /// <summary>
        /// 방 문제점 분석
        /// </summary>
        public Dictionary<string, int> AnalyzeRoomIssues()
        {
            var issues = new Dictionary<string, int>();
            
            foreach (var room in invalidRooms)
            {
                if (room.walls.Count < settings.minWalls)
                    issues["벽 부족"] = issues.GetValueOrDefault("벽 부족", 0) + 1;
                    
                if (room.doors.Count < settings.minDoors)
                    issues["문 부족"] = issues.GetValueOrDefault("문 부족", 0) + 1;
                    
                if (room.beds.Count < settings.minBeds && !room.isSunbedRoom)
                    issues["침대 부족"] = issues.GetValueOrDefault("침대 부족", 0) + 1;
                    
                if (room.GetRoomSize() == 0)
                    issues["바닥 없음"] = issues.GetValueOrDefault("바닥 없음", 0) + 1;
                    
                if (room.bounds.size.magnitude <= 0)
                    issues["경계 오류"] = issues.GetValueOrDefault("경계 오류", 0) + 1;
            }
            
            return issues;
        }
        
        #endregion
        
        #region 데이터 변경 분석
        
        /// <summary>
        /// 방 데이터 변경 사항 분석
        /// </summary>
        private void AnalyzeChanges(List<RoomInfo> oldRooms, List<RoomInfo> newRooms)
        {
            var oldIds = new HashSet<string>(oldRooms.Select(r => r.roomId));
            var newIds = new HashSet<string>(newRooms.Select(r => r.roomId));
            
            // 새로 추가된 방들
            var addedIds = newIds.Except(oldIds);
            foreach (var roomId in addedIds)
            {
                var room = newRooms.First(r => r.roomId == roomId);
                OnRoomAdded?.Invoke(room);
            }
            
            // 제거된 방들
            var removedIds = oldIds.Except(newIds);
            foreach (var roomId in removedIds)
            {
                var room = oldRooms.First(r => r.roomId == roomId);
                OnRoomRemoved?.Invoke(room);
            }
            
            RoomUtilities.DebugLog($"방 변경 분석: 추가 {addedIds.Count()}개, 제거 {removedIds.Count()}개", 
                                 false, settings);
        }
        
        #endregion
        
        #region 데이터 정리
        
        /// <summary>
        /// 모든 방 데이터 초기화
        /// </summary>
        public void ClearAllRooms()
        {
            allRooms.Clear();
            validRooms.Clear();
            invalidRooms.Clear();
            
            OnRoomsUpdated?.Invoke(allRooms);
            
            RoomUtilities.DebugLog("모든 방 데이터 초기화됨", true, settings);
        }
        
        /// <summary>
        /// 무효한 방들만 제거
        /// </summary>
        public void RemoveInvalidRooms()
        {
            var removedCount = invalidRooms.Count;
            
            foreach (var room in invalidRooms.ToList())
            {
                allRooms.Remove(room);
                OnRoomRemoved?.Invoke(room);
            }
            
            invalidRooms.Clear();
            OnRoomsUpdated?.Invoke(allRooms);
            
            RoomUtilities.DebugLog($"{removedCount}개 무효한 방 제거됨", true, settings);
        }
        
        /// <summary>
        /// 중복 방 제거
        /// </summary>
        public void RemoveDuplicateRooms()
        {
            var uniqueRooms = allRooms.GroupBy(room => room.roomId)
                                    .Select(group => group.First())
                                    .ToList();
            
            int removedCount = allRooms.Count - uniqueRooms.Count;
            
            if (removedCount > 0)
            {
                allRooms = uniqueRooms;
                validRooms = allRooms.Where(room => room.isValid).ToList();
                invalidRooms = allRooms.Where(room => !room.isValid).ToList();
                
                OnRoomsUpdated?.Invoke(allRooms);
                
                RoomUtilities.DebugLog($"{removedCount}개 중복 방 제거됨", true, settings);
            }
        }
        
        #endregion
        
        #region 메모리 관리 & 정리
        
        /// <summary>
        /// 모든 이벤트 구독 해제 및 메모리 정리
        /// </summary>
        public void Dispose()
        {
            // 모든 이벤트 구독자 해제
            OnRoomsUpdated = null;
            OnRoomAdded = null;
            OnRoomRemoved = null;
            OnRoomValidationChanged = null;
            
            // GameObject 참조 정리
            CleanupGameObjectReferences();
            
            // 리스트 정리
            allRooms?.Clear();
            validRooms?.Clear();
            invalidRooms?.Clear();
            
            RoomUtilities.DebugLog("RoomDataManager 메모리 정리 완료", true, settings);
        }
        
        /// <summary>
        /// 모든 방의 GameObject 참조 정리
        /// </summary>
        private void CleanupGameObjectReferences()
        {
            foreach (var room in allRooms)
            {
                if (room?.gameObject != null)
                {
                    try
                    {
                        UnityEngine.Object.DestroyImmediate(room.gameObject);
                    }
                    catch (System.Exception ex)
                    {
                        RoomUtilities.DebugLog($"GameObject 정리 중 오류: {ex.Message}", true, settings);
                    }
                    finally
                    {
                        room.gameObject = null;
                    }
                }
            }
        }
        
        /// <summary>
        /// 특정 이벤트만 해제
        /// </summary>
        public void UnsubscribeEvents(System.Object subscriber)
        {
            // C#에서는 특정 구독자만 해제하기 어려우므로 로깅만 수행
            RoomUtilities.DebugLog($"이벤트 구독 해제 요청: {subscriber?.GetType().Name}", false, settings);
        }
        
        /// <summary>
        /// 메모리 사용량 체크 (디버그용)
        /// </summary>
        public void CheckMemoryUsage()
        {
            long memoryBefore = System.GC.GetTotalMemory(false);
            System.GC.Collect();
            long memoryAfter = System.GC.GetTotalMemory(true);
            
            RoomUtilities.DebugLog($"메모리 정리: {memoryBefore / 1024}KB → {memoryAfter / 1024}KB " +
                                 $"(절약: {(memoryBefore - memoryAfter) / 1024}KB)", true, settings);
        }
        
        #endregion
    }
    
    /// <summary>
    /// 방 통계 정보
    /// </summary>
    [Serializable]
    public class RoomStatistics
    {
        public int TotalRoomCount;
        public int ValidRoomCount;
        public int InvalidRoomCount;
        public int StandardRoomCount;
        public int SunbedRoomCount;
        
        public float AveragePrice;
        public float AverageReputation;
        public float AverageSize;
        
        public float MinPrice;
        public float MaxPrice;
        public float MinReputation;
        public float MaxReputation;
        
        public Dictionary<int, int> FloorDistribution = new Dictionary<int, int>();
        
        public override string ToString()
        {
            return $"방 통계:\n" +
                   $"- 총 방 수: {TotalRoomCount} (유효 {ValidRoomCount}, 무효 {InvalidRoomCount})\n" +
                   $"- 방 타입: 일반 {StandardRoomCount}, 선베드 {SunbedRoomCount}\n" +
                   $"- 평균 가격: {AveragePrice:F0} (범위: {MinPrice:F0} ~ {MaxPrice:F0})\n" +
                   $"- 평균 명성: {AverageReputation:F0} (범위: {MinReputation:F0} ~ {MaxReputation:F0})\n" +
                   $"- 평균 크기: {AverageSize:F1} 셀";
        }
    }
} 