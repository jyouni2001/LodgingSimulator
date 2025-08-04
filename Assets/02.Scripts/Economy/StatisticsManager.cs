using UnityEngine;
using System;
using System.Collections.Generic;

namespace JY
{
    /// <summary>
    /// 통계 시스템 관리자
    /// 게임 내 모든 통계를 수집, 관리, 저장하는 싱글톤 클래스
    /// </summary>
    public class StatisticsManager : MonoBehaviour
    {
        [Header("통계 설정")]
        [SerializeField] private bool enableStatistics = true;
        [SerializeField] private bool autoSave = true;
        [SerializeField] private float autoSaveInterval = 60f; // 자동 저장 간격 (초)
        
        [Header("디버그 설정")]
        [SerializeField] private bool showDebugLogs = false;
        [SerializeField] private bool showImportantLogsOnly = true;
        
        [Header("현재 통계")]
        [SerializeField] private StatisticsData statisticsData = new StatisticsData();
        
        // 싱글톤
        public static StatisticsManager Instance { get; private set; }
        
        // 시스템 참조
        private TimeSystem timeSystem;
        private PaymentSystem paymentSystem;
        private ReputationSystem reputationSystem;
        private AISpawner aiSpawner;
        
        // 현재 상태
        private int currentDay = 1;
        private DailyStatistics currentDayStats;
        private float lastSaveTime;
        
        // 이벤트
        public event Action<DailyStatistics> OnDayStatisticsUpdated;
        public event Action<int> OnNewDayStarted;
        public event Action<StatisticsData> OnStatisticsSaved;
        public event Action<StatisticsData> OnStatisticsLoaded;
        
        // 프로퍼티
        public StatisticsData Data => statisticsData;
        public DailyStatistics CurrentDayStats => currentDayStats;
        public bool IsEnabled => enableStatistics;
        
        #region Unity 생명주기
        
        void Awake()
        {
            InitializeSingleton();
        }
        
        void Start()
        {
            if (enableStatistics)
            {
                InitializeSystems();
                LoadStatistics();
                StartCurrentDay();
            }
        }
        
        void Update()
        {
            if (!enableStatistics) return;
            
            // 자동 저장
            if (autoSave && Time.time - lastSaveTime > autoSaveInterval)
            {
                SaveStatistics();
                lastSaveTime = Time.time;
            }
        }
        
        void OnDestroy()
        {
            CleanupSystems();
            SaveStatistics();
        }
        
        #endregion
        
        #region 초기화
        
        /// <summary>
        /// 싱글톤 초기화
        /// </summary>
        private void InitializeSingleton()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            DebugLog("통계 매니저 초기화 완료", true);
        }
        
        /// <summary>
        /// 시스템 초기화
        /// </summary>
        private void InitializeSystems()
        {
            // TimeSystem 연결
            timeSystem = FindObjectOfType<TimeSystem>();
            if (timeSystem != null)
            {
                timeSystem.OnDayChanged += OnDayChanged;
                currentDay = timeSystem.CurrentDay;
                DebugLog("TimeSystem 연결 완료", showImportantLogsOnly);
            }
            else
            {
                DebugLog("TimeSystem을 찾을 수 없습니다", true);
            }
            
            // PaymentSystem 연결
            paymentSystem = FindObjectOfType<PaymentSystem>();
            if (paymentSystem != null)
            {
                paymentSystem.OnPaymentProcessed += OnPaymentProcessed;
                DebugLog("PaymentSystem 연결 완료", showImportantLogsOnly);
            }
            else
            {
                DebugLog("PaymentSystem을 찾을 수 없습니다", true);
            }
            
            // ReputationSystem 연결
            reputationSystem = FindObjectOfType<ReputationSystem>();
            if (reputationSystem != null)
            {
                reputationSystem.OnReputationChanged += OnReputationChanged;
                DebugLog("ReputationSystem 연결 완료", showImportantLogsOnly);
            }
            else
            {
                DebugLog("ReputationSystem을 찾을 수 없습니다", true);
            }
            
            // AISpawner 연결 (스폰된 AI를 방문객으로 카운트)
            aiSpawner = FindObjectOfType<AISpawner>();
            if (aiSpawner != null)
            {
                aiSpawner.OnAISpawned += OnAISpawned;
                DebugLog("AISpawner 연결 완료", showImportantLogsOnly);
            }
            else
            {
                DebugLog("AISpawner를 찾을 수 없습니다", true);
            }
        }
        
        /// <summary>
        /// 시스템 정리
        /// </summary>
        private void CleanupSystems()
        {
            if (timeSystem != null)
                timeSystem.OnDayChanged -= OnDayChanged;
            
            if (paymentSystem != null)
                paymentSystem.OnPaymentProcessed -= OnPaymentProcessed;
                
            if (reputationSystem != null)
                reputationSystem.OnReputationChanged -= OnReputationChanged;
                
            if (aiSpawner != null)
                aiSpawner.OnAISpawned -= OnAISpawned;
        }
        
        #endregion
        
        #region 일차 관리
        
        /// <summary>
        /// 현재 일차 시작
        /// </summary>
        private void StartCurrentDay()
        {
            currentDayStats = statisticsData.GetCurrentDayStats();
            if (currentDayStats.day != currentDay)
            {
                // 새로운 날 시작
                currentDayStats = statisticsData.StartNewDay(currentDay);
                OnNewDayStarted?.Invoke(currentDay);
            }
            
            DebugLog($"{currentDay}일차 통계 시작", true);
        }
        
        /// <summary>
        /// 일차 변경 이벤트 핸들러
        /// </summary>
        private void OnDayChanged(int newDay)
        {
            DebugLog($"일차 변경: {currentDay} → {newDay}", true);
            
            // 이전 일차 통계 마무리
            FinalizeDayStats();
            
            // 새 일차 시작
            currentDay = newDay;
            StartCurrentDay();
        }
        
        /// <summary>
        /// 일차 통계 마무리
        /// </summary>
        private void FinalizeDayStats()
        {
            DebugLog($"{currentDayStats.day}일차 통계 마무리: {currentDayStats.GetSummary()}", true);
            OnDayStatisticsUpdated?.Invoke(currentDayStats);
            SaveStatistics();
        }
        
        #endregion
        
        #region 통계 업데이트
        
        /// <summary>
        /// 방문객 추가
        /// </summary>
        public void AddVisitor()
        {
            if (!enableStatistics) return;
            
            statisticsData.AddVisitor(currentDay);
            currentDayStats = statisticsData.GetCurrentDayStats();
            
            DebugLog($"방문객 추가: {currentDay}일차 총 {currentDayStats.visitorCount}명", showDebugLogs);
            OnDayStatisticsUpdated?.Invoke(currentDayStats);
        }
        
        /// <summary>
        /// 결제 처리 이벤트 핸들러
        /// </summary>
        private void OnPaymentProcessed(string aiName, int amount, string roomId)
        {
            if (!enableStatistics) return;
            
            statisticsData.AddRevenue(currentDay, amount);
            currentDayStats = statisticsData.GetCurrentDayStats();
            
            DebugLog($"수익 추가: {amount}원, {currentDay}일차 총 {currentDayStats.revenue}원", showDebugLogs);
            OnDayStatisticsUpdated?.Invoke(currentDayStats);
        }
        
        /// <summary>
        /// 명성도 변경 이벤트 핸들러
        /// </summary>
        private void OnReputationChanged(int oldReputation, int newReputation)
        {
            if (!enableStatistics) return;
            
            int reputationGained = newReputation - oldReputation;
            if (reputationGained > 0)
            {
                statisticsData.AddReputation(currentDay, reputationGained);
                currentDayStats = statisticsData.GetCurrentDayStats();
                
                DebugLog($"명성도 추가: +{reputationGained}, {currentDay}일차 총 +{currentDayStats.reputationGained}", showDebugLogs);
                OnDayStatisticsUpdated?.Invoke(currentDayStats);
            }
        }
        
        /// <summary>
        /// AI 스폰 이벤트 핸들러 (방문객 카운트)
        /// </summary>
        private void OnAISpawned(GameObject aiGameObject)
        {
            if (!enableStatistics) return;
            
            AddVisitor();
            DebugLog($"새 방문객 스폰: {aiGameObject.name}, {currentDay}일차 총 {currentDayStats.visitorCount}명", showDebugLogs);
        }
        
        #endregion
        
        #region 공개 API
        
        /// <summary>
        /// 특정 일차 통계 가져오기
        /// </summary>
        public DailyStatistics? GetDayStatistics(int day)
        {
            return statisticsData.GetDayStats(day);
        }
        
        /// <summary>
        /// 최근 N일 통계 가져오기
        /// </summary>
        public List<DailyStatistics> GetRecentDays(int dayCount)
        {
            return statisticsData.GetRecentDays(dayCount);
        }
        
        /// <summary>
        /// 전체 통계 요약
        /// </summary>
        public string GetStatisticsSummary()
        {
            var avgStats = statisticsData.GetAverageStats();
            return $"총 {statisticsData.TotalDays}일간 운영\n" +
                   $"누적 방문객: {statisticsData.TotalVisitors:N0}명\n" +
                   $"누적 수익: {statisticsData.TotalRevenue:N0}원\n" +
                   $"누적 명성도: {statisticsData.TotalReputation:N0}\n" +
                   $"일평균 방문객: {avgStats.avgVisitors:F1}명\n" +
                   $"일평균 수익: {avgStats.avgRevenue:N0}원\n" +
                   $"최고 기록: {statisticsData.BestDay}일차 방문객 {statisticsData.BestDayVisitors}명";
        }
        
        /// <summary>
        /// 통계 초기화
        /// </summary>
        [ContextMenu("통계 초기화")]
        public void ResetStatistics()
        {
            statisticsData.Reset();
            currentDayStats = statisticsData.StartNewDay(currentDay);
            SaveStatistics();
            
            DebugLog("통계가 초기화되었습니다", true);
        }
        
        #endregion
        
        #region 저장/로드
        
        /// <summary>
        /// 통계 저장
        /// </summary>
        public void SaveStatistics()
        {
            if (!enableStatistics) return;
            
            try
            {
                string json = JsonUtility.ToJson(statisticsData, true);
                string filePath = Application.persistentDataPath + "/statistics.json";
                System.IO.File.WriteAllText(filePath, json);
                
                DebugLog($"통계 저장 완료: {filePath}", showImportantLogsOnly);
                OnStatisticsSaved?.Invoke(statisticsData);
            }
            catch (Exception e)
            {
                Debug.LogError($"통계 저장 실패: {e.Message}");
            }
        }
        
        /// <summary>
        /// 통계 로드
        /// </summary>
        public void LoadStatistics()
        {
            if (!enableStatistics) return;
            
            try
            {
                string filePath = Application.persistentDataPath + "/statistics.json";
                if (System.IO.File.Exists(filePath))
                {
                    string json = System.IO.File.ReadAllText(filePath);
                    statisticsData = JsonUtility.FromJson<StatisticsData>(json);
                    
                    DebugLog($"통계 로드 완료: 총 {statisticsData.TotalDays}일 데이터", true);
                    OnStatisticsLoaded?.Invoke(statisticsData);
                }
                else
                {
                    DebugLog("저장된 통계 파일이 없습니다. 새로 시작합니다.", true);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"통계 로드 실패: {e.Message}");
                statisticsData = new StatisticsData();
            }
        }
        
        #endregion
        
        #region 디버그
        
        /// <summary>
        /// 디버그 로그
        /// </summary>
        private void DebugLog(string message, bool forceLog = false)
        {
            if (showDebugLogs || forceLog)
            {
                Debug.Log($"[통계] {message}");
            }
        }
        
        /// <summary>
        /// 현재 통계 출력
        /// </summary>
        [ContextMenu("현재 통계 출력")]
        public void PrintCurrentStatistics()
        {
            Debug.Log("=== 현재 통계 ===");
            Debug.Log($"현재: {currentDayStats.GetSummary()}");
            Debug.Log(GetStatisticsSummary());
            Debug.Log("================");
        }
        
        #endregion
    }
}