using System;
using System.Collections.Generic;
using UnityEngine;

namespace JY
{
    /// <summary>
    /// 일일 통계 데이터
    /// </summary>
    [System.Serializable]
    public struct DailyStatistics
    {
        public int day;                 // 일차
        public int visitorCount;        // 방문객 수
        public int revenue;             // 수익 (원)
        public int reputationGained;    // 얻은 명성도
        public DateTime date;           // 실제 날짜 (저장용)
        
        public DailyStatistics(int day)
        {
            this.day = day;
            this.visitorCount = 0;
            this.revenue = 0;
            this.reputationGained = 0;
            this.date = DateTime.Now;
        }
        
        /// <summary>
        /// 방문객 추가
        /// </summary>
        public void AddVisitor()
        {
            visitorCount++;
        }
        
        /// <summary>
        /// 수익 추가
        /// </summary>
        public void AddRevenue(int amount)
        {
            revenue += amount;
        }
        
        /// <summary>
        /// 명성도 추가
        /// </summary>
        public void AddReputation(int amount)
        {
            reputationGained += amount;
        }
        
        /// <summary>
        /// 통계 요약 문자열
        /// </summary>
        public string GetSummary()
        {
            return $"{day}일차: 방문객 {visitorCount}명, 수익 {revenue:N0}원, 명성도 +{reputationGained}";
        }
    }
    
    /// <summary>
    /// 전체 통계 데이터 관리 클래스
    /// </summary>
    [System.Serializable]
    public class StatisticsData
    {
        [Header("일일 통계")]
        [SerializeField] private List<DailyStatistics> dailyStats = new List<DailyStatistics>();
        
        [Header("누적 통계")]
        [SerializeField] private int totalVisitors = 0;
        [SerializeField] private int totalRevenue = 0;
        [SerializeField] private int totalReputation = 0;
        
        [Header("최고 기록")]
        [SerializeField] private int bestDayVisitors = 0;
        [SerializeField] private int bestDayRevenue = 0;
        [SerializeField] private int bestDayReputation = 0;
        [SerializeField] private int bestDay = 0;
        
        // 프로퍼티
        public List<DailyStatistics> DailyStats => dailyStats;
        public int TotalVisitors => totalVisitors;
        public int TotalRevenue => totalRevenue;
        public int TotalReputation => totalReputation;
        public int BestDayVisitors => bestDayVisitors;
        public int BestDayRevenue => bestDayRevenue;
        public int BestDayReputation => bestDayReputation;
        public int BestDay => bestDay;
        public int TotalDays => dailyStats.Count;
        
        /// <summary>
        /// 새로운 날 시작
        /// </summary>
        public DailyStatistics StartNewDay(int day)
        {
            var newDayStats = new DailyStatistics(day);
            dailyStats.Add(newDayStats);
            return newDayStats;
        }
        
        /// <summary>
        /// 현재 일일 통계 가져오기
        /// </summary>
        public DailyStatistics GetCurrentDayStats()
        {
            if (dailyStats.Count == 0)
                return new DailyStatistics(1);
            
            return dailyStats[dailyStats.Count - 1];
        }
        
        /// <summary>
        /// 특정 일차 통계 가져오기
        /// </summary>
        public DailyStatistics? GetDayStats(int day)
        {
            foreach (var stats in dailyStats)
            {
                if (stats.day == day)
                    return stats;
            }
            return null;
        }
        
        /// <summary>
        /// 일일 통계 업데이트 및 누적 통계 반영
        /// </summary>
        public void UpdateDayStats(int day, int visitors, int revenue, int reputation)
        {
            // 해당 일차 통계 찾기
            for (int i = 0; i < dailyStats.Count; i++)
            {
                if (dailyStats[i].day == day)
                {
                    var stats = dailyStats[i];
                    
                    // 이전 값과의 차이 계산
                    int visitorDiff = visitors - stats.visitorCount;
                    int revenueDiff = revenue - stats.revenue;
                    int reputationDiff = reputation - stats.reputationGained;
                    
                    // 일일 통계 업데이트
                    stats.visitorCount = visitors;
                    stats.revenue = revenue;
                    stats.reputationGained = reputation;
                    dailyStats[i] = stats;
                    
                    // 누적 통계 업데이트
                    totalVisitors += visitorDiff;
                    totalRevenue += revenueDiff;
                    totalReputation += reputationDiff;
                    
                    // 최고 기록 업데이트
                    UpdateBestRecords(stats);
                    break;
                }
            }
        }
        
        /// <summary>
        /// 방문객 추가
        /// </summary>
        public void AddVisitor(int day)
        {
            UpdateDayStatsByType(day, 1, 0, 0);
        }
        
        /// <summary>
        /// 수익 추가
        /// </summary>
        public void AddRevenue(int day, int amount)
        {
            UpdateDayStatsByType(day, 0, amount, 0);
        }
        
        /// <summary>
        /// 명성도 추가
        /// </summary>
        public void AddReputation(int day, int amount)
        {
            UpdateDayStatsByType(day, 0, 0, amount);
        }
        
        /// <summary>
        /// 타입별 일일 통계 업데이트
        /// </summary>
        private void UpdateDayStatsByType(int day, int visitorAdd, int revenueAdd, int reputationAdd)
        {
            // 해당 일차 통계 찾기 또는 생성
            int index = -1;
            for (int i = 0; i < dailyStats.Count; i++)
            {
                if (dailyStats[i].day == day)
                {
                    index = i;
                    break;
                }
            }
            
            if (index == -1)
            {
                // 새로운 일차 생성
                var newStats = new DailyStatistics(day);
                dailyStats.Add(newStats);
                index = dailyStats.Count - 1;
            }
            
            // 통계 업데이트
            var stats = dailyStats[index];
            stats.visitorCount += visitorAdd;
            stats.revenue += revenueAdd;
            stats.reputationGained += reputationAdd;
            dailyStats[index] = stats;
            
            // 누적 통계 업데이트
            totalVisitors += visitorAdd;
            totalRevenue += revenueAdd;
            totalReputation += reputationAdd;
            
            // 최고 기록 업데이트
            UpdateBestRecords(stats);
        }
        
        /// <summary>
        /// 최고 기록 업데이트
        /// </summary>
        private void UpdateBestRecords(DailyStatistics stats)
        {
            if (stats.visitorCount > bestDayVisitors)
            {
                bestDayVisitors = stats.visitorCount;
                bestDay = stats.day;
            }
            
            if (stats.revenue > bestDayRevenue)
            {
                bestDayRevenue = stats.revenue;
            }
            
            if (stats.reputationGained > bestDayReputation)
            {
                bestDayReputation = stats.reputationGained;
            }
        }
        
        /// <summary>
        /// 최근 N일 통계 가져오기
        /// </summary>
        public List<DailyStatistics> GetRecentDays(int dayCount)
        {
            var recentDays = new List<DailyStatistics>();
            int startIndex = Mathf.Max(0, dailyStats.Count - dayCount);
            
            for (int i = startIndex; i < dailyStats.Count; i++)
            {
                recentDays.Add(dailyStats[i]);
            }
            
            return recentDays;
        }
        
        /// <summary>
        /// 통계 초기화
        /// </summary>
        public void Reset()
        {
            dailyStats.Clear();
            totalVisitors = 0;
            totalRevenue = 0;
            totalReputation = 0;
            bestDayVisitors = 0;
            bestDayRevenue = 0;
            bestDayReputation = 0;
            bestDay = 0;
        }
        
        /// <summary>
        /// 평균 통계 계산
        /// </summary>
        public (float avgVisitors, float avgRevenue, float avgReputation) GetAverageStats()
        {
            if (dailyStats.Count == 0)
                return (0f, 0f, 0f);
            
            float avgVisitors = (float)totalVisitors / dailyStats.Count;
            float avgRevenue = (float)totalRevenue / dailyStats.Count;
            float avgReputation = (float)totalReputation / dailyStats.Count;
            
            return (avgVisitors, avgRevenue, avgReputation);
        }
    }
}