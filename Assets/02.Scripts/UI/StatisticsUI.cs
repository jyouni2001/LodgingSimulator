using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace JY
{
    /// <summary>
    /// 통계 UI 시스템
    /// 일일/누적 통계와 그래프를 표시하는 UI 관리자
    /// </summary>
    public class StatisticsUI : MonoBehaviour
    {
        [Header("UI 패널")]
        [SerializeField] private GameObject statisticsPanel;
        [SerializeField] private Button toggleButton;
        [SerializeField] private Button closeButton;
        
        [Header("현재 일차 통계")]
        [SerializeField] private TextMeshProUGUI currentDayText;
        [SerializeField] private TextMeshProUGUI currentVisitorsText;
        [SerializeField] private TextMeshProUGUI currentRevenueText;
        [SerializeField] private TextMeshProUGUI currentReputationText;
        
        [Header("누적 통계")]
        [SerializeField] private TextMeshProUGUI totalDaysText;
        [SerializeField] private TextMeshProUGUI totalVisitorsText;
        [SerializeField] private TextMeshProUGUI totalRevenueText;
        [SerializeField] private TextMeshProUGUI totalReputationText;
        
        [Header("최고 기록")]
        [SerializeField] private TextMeshProUGUI bestDayText;
        [SerializeField] private TextMeshProUGUI bestVisitorsText;
        [SerializeField] private TextMeshProUGUI bestRevenueText;
        [SerializeField] private TextMeshProUGUI bestReputationText;
        
        [Header("평균 통계")]
        [SerializeField] private TextMeshProUGUI avgVisitorsText;
        [SerializeField] private TextMeshProUGUI avgRevenueText;
        [SerializeField] private TextMeshProUGUI avgReputationText;
        
        [Header("그래프 설정")]
        [SerializeField] private RectTransform graphContainer;
        [SerializeField] private GameObject graphPointPrefab;
        [SerializeField] private GameObject graphLinePrefab;
        [SerializeField] private Color revenueGraphColor = Color.green;
        [SerializeField] private Color visitorGraphColor = Color.blue;
        [SerializeField] private Color reputationGraphColor = Color.yellow;
        
        [Header("그래프 옵션")]
        [SerializeField] private int maxGraphDays = 30; // 최대 표시 일수
        [SerializeField] private float graphWidth = 600f;
        [SerializeField] private float graphHeight = 200f;
        [SerializeField] private bool showRevenueGraph = true;
        [SerializeField] private bool showVisitorGraph = true;
        [SerializeField] private bool showReputationGraph = false;
        
        [Header("UI 옵션")]
        [SerializeField] private bool autoRefresh = true;
        [SerializeField] private float refreshInterval = 2f;
        
        // 시스템 참조
        private StatisticsManager statisticsManager;
        
        // UI 상태
        private bool isVisible = false;
        private float lastRefreshTime;
        
        // 그래프 오브젝트들
        private List<GameObject> graphPoints = new List<GameObject>();
        private List<GameObject> graphLines = new List<GameObject>();
        
        #region Unity 생명주기
        
        void Start()
        {
            InitializeUI();
            SetupEventHandlers();
        }
        
        void Update()
        {
            if (autoRefresh && isVisible && Time.time - lastRefreshTime > refreshInterval)
            {
                RefreshUI();
                lastRefreshTime = Time.time;
            }
        }
        
        void OnDestroy()
        {
            CleanupEventHandlers();
        }
        
        #endregion
        
        #region 초기화
        
        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            // StatisticsManager 찾기
            statisticsManager = StatisticsManager.Instance;
            if (statisticsManager == null)
            {
                Debug.LogWarning("[StatisticsUI] StatisticsManager를 찾을 수 없습니다.");
                return;
            }
            
            // 초기 UI 상태 설정
            if (statisticsPanel != null)
            {
                statisticsPanel.SetActive(false);
            }
            
            // 버튼 이벤트 연결
            if (toggleButton != null)
            {
                toggleButton.onClick.AddListener(ToggleUI);
            }
            
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseUI);
            }
            
            Debug.Log("[StatisticsUI] 통계 UI 초기화 완료");
        }
        
        /// <summary>
        /// 이벤트 핸들러 설정
        /// </summary>
        private void SetupEventHandlers()
        {
            if (statisticsManager != null)
            {
                statisticsManager.OnDayStatisticsUpdated += OnStatisticsUpdated;
                statisticsManager.OnNewDayStarted += OnNewDayStarted;
            }
        }
        
        /// <summary>
        /// 이벤트 핸들러 정리
        /// </summary>
        private void CleanupEventHandlers()
        {
            if (statisticsManager != null)
            {
                statisticsManager.OnDayStatisticsUpdated -= OnStatisticsUpdated;
                statisticsManager.OnNewDayStarted -= OnNewDayStarted;
            }
        }
        
        #endregion
        
        #region UI 제어
        
        /// <summary>
        /// UI 토글
        /// </summary>
        public void ToggleUI()
        {
            if (isVisible)
            {
                CloseUI();
            }
            else
            {
                OpenUI();
            }
        }
        
        /// <summary>
        /// UI 열기
        /// </summary>
        public void OpenUI()
        {
            if (statisticsPanel != null)
            {
                statisticsPanel.SetActive(true);
                isVisible = true;
                RefreshUI();
                Debug.Log("[StatisticsUI] 통계 UI 열림");
            }
        }
        
        /// <summary>
        /// UI 닫기
        /// </summary>
        public void CloseUI()
        {
            if (statisticsPanel != null)
            {
                statisticsPanel.SetActive(false);
                isVisible = false;
                Debug.Log("[StatisticsUI] 통계 UI 닫힘");
            }
        }
        
        #endregion
        
        #region UI 업데이트
        
        /// <summary>
        /// UI 새로고침
        /// </summary>
        public void RefreshUI()
        {
            if (statisticsManager == null || !isVisible) return;
            
            UpdateCurrentDayStats();
            UpdateTotalStats();
            UpdateBestRecords();
            UpdateAverageStats();
            UpdateGraph();
        }
        
        /// <summary>
        /// 현재 일차 통계 업데이트
        /// </summary>
        private void UpdateCurrentDayStats()
        {
            var currentStats = statisticsManager.CurrentDayStats;
            
            if (currentDayText != null)
                currentDayText.text = $"{currentStats.day}일차";
            
            if (currentVisitorsText != null)
                currentVisitorsText.text = $"{currentStats.visitorCount:N0}명";
            
            if (currentRevenueText != null)
                currentRevenueText.text = $"{currentStats.revenue:N0}원";
            
            if (currentReputationText != null)
                currentReputationText.text = $"+{currentStats.reputationGained}";
        }
        
        /// <summary>
        /// 누적 통계 업데이트
        /// </summary>
        private void UpdateTotalStats()
        {
            var data = statisticsManager.Data;
            
            if (totalDaysText != null)
                totalDaysText.text = $"{data.TotalDays}일";
            
            if (totalVisitorsText != null)
                totalVisitorsText.text = $"{data.TotalVisitors:N0}명";
            
            if (totalRevenueText != null)
                totalRevenueText.text = $"{data.TotalRevenue:N0}원";
            
            if (totalReputationText != null)
                totalReputationText.text = $"+{data.TotalReputation}";
        }
        
        /// <summary>
        /// 최고 기록 업데이트
        /// </summary>
        private void UpdateBestRecords()
        {
            var data = statisticsManager.Data;
            
            if (bestDayText != null)
                bestDayText.text = $"{data.BestDay}일차";
            
            if (bestVisitorsText != null)
                bestVisitorsText.text = $"{data.BestDayVisitors:N0}명";
            
            if (bestRevenueText != null)
                bestRevenueText.text = $"{data.BestDayRevenue:N0}원";
            
            if (bestReputationText != null)
                bestReputationText.text = $"+{data.BestDayReputation}";
        }
        
        /// <summary>
        /// 평균 통계 업데이트
        /// </summary>
        private void UpdateAverageStats()
        {
            var avgStats = statisticsManager.Data.GetAverageStats();
            
            if (avgVisitorsText != null)
                avgVisitorsText.text = $"{avgStats.avgVisitors:F1}명";
            
            if (avgRevenueText != null)
                avgRevenueText.text = $"{avgStats.avgRevenue:N0}원";
            
            if (avgReputationText != null)
                avgReputationText.text = $"+{avgStats.avgReputation:F1}";
        }
        
        #endregion
        
        #region 그래프
        
        /// <summary>
        /// 그래프 업데이트
        /// </summary>
        private void UpdateGraph()
        {
            if (graphContainer == null) return;
            
            ClearGraph();
            
            var recentDays = statisticsManager.GetRecentDays(maxGraphDays);
            if (recentDays.Count < 2) return; // 데이터가 너무 적으면 그래프 그리지 않음
            
            if (showRevenueGraph)
            {
                DrawGraph(recentDays, day => day.revenue, revenueGraphColor, "수익");
            }
            
            if (showVisitorGraph)
            {
                DrawGraph(recentDays, day => day.visitorCount, visitorGraphColor, "방문객");
            }
            
            if (showReputationGraph)
            {
                DrawGraph(recentDays, day => day.reputationGained, reputationGraphColor, "명성도");
            }
        }
        
        /// <summary>
        /// 특정 데이터로 그래프 그리기
        /// </summary>
        private void DrawGraph(List<DailyStatistics> data, System.Func<DailyStatistics, int> valueSelector, Color color, string label)
        {
            if (data.Count < 2) return;
            
            var values = data.Select(valueSelector).ToList();
            float maxValue = values.Max();
            if (maxValue <= 0) return;
            
            Vector2 graphSize = new Vector2(graphWidth, graphHeight);
            float stepX = graphSize.x / (data.Count - 1);
            
            // 점들 그리기
            for (int i = 0; i < data.Count; i++)
            {
                float normalizedValue = values[i] / maxValue;
                Vector2 position = new Vector2(i * stepX, normalizedValue * graphSize.y);
                
                GameObject point = CreateGraphPoint(position, color);
                if (point != null)
                {
                    graphPoints.Add(point);
                }
                
                // 선 그리기 (이전 점과 연결)
                if (i > 0)
                {
                    float prevNormalizedValue = values[i - 1] / maxValue;
                    Vector2 prevPosition = new Vector2((i - 1) * stepX, prevNormalizedValue * graphSize.y);
                    
                    GameObject line = CreateGraphLine(prevPosition, position, color);
                    if (line != null)
                    {
                        graphLines.Add(line);
                    }
                }
            }
        }
        
        /// <summary>
        /// 그래프 점 생성
        /// </summary>
        private GameObject CreateGraphPoint(Vector2 position, Color color)
        {
            if (graphPointPrefab == null || graphContainer == null) return null;
            
            GameObject point = Instantiate(graphPointPrefab, graphContainer);
            
            RectTransform rectTransform = point.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = position;
            }
            
            Image image = point.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
            
            return point;
        }
        
        /// <summary>
        /// 그래프 선 생성
        /// </summary>
        private GameObject CreateGraphLine(Vector2 startPos, Vector2 endPos, Color color)
        {
            if (graphLinePrefab == null || graphContainer == null) return null;
            
            GameObject line = Instantiate(graphLinePrefab, graphContainer);
            
            RectTransform rectTransform = line.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector2 direction = endPos - startPos;
                float distance = direction.magnitude;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                
                rectTransform.anchoredPosition = startPos + direction * 0.5f;
                rectTransform.sizeDelta = new Vector2(distance, rectTransform.sizeDelta.y);
                rectTransform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            
            Image image = line.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
            
            return line;
        }
        
        /// <summary>
        /// 그래프 초기화
        /// </summary>
        private void ClearGraph()
        {
            foreach (var point in graphPoints)
            {
                if (point != null) DestroyImmediate(point);
            }
            graphPoints.Clear();
            
            foreach (var line in graphLines)
            {
                if (line != null) DestroyImmediate(line);
            }
            graphLines.Clear();
        }
        
        #endregion
        
        #region 이벤트 핸들러
        
        /// <summary>
        /// 통계 업데이트 이벤트 핸들러
        /// </summary>
        private void OnStatisticsUpdated(DailyStatistics dayStats)
        {
            if (isVisible)
            {
                RefreshUI();
            }
        }
        
        /// <summary>
        /// 새 날 시작 이벤트 핸들러
        /// </summary>
        private void OnNewDayStarted(int newDay)
        {
            if (isVisible)
            {
                RefreshUI();
            }
        }
        
        #endregion
        
        #region 공개 메서드
        
        /// <summary>
        /// 그래프 옵션 토글
        /// </summary>
        public void ToggleRevenueGraph()
        {
            showRevenueGraph = !showRevenueGraph;
            if (isVisible) UpdateGraph();
        }
        
        public void ToggleVisitorGraph()
        {
            showVisitorGraph = !showVisitorGraph;
            if (isVisible) UpdateGraph();
        }
        
        public void ToggleReputationGraph()
        {
            showReputationGraph = !showReputationGraph;
            if (isVisible) UpdateGraph();
        }
        
        #endregion
    }
}