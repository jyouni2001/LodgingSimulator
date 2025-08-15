using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;

namespace JY
{
    /// <summary>
    /// 일별 데이터 클래스
    /// </summary>
    [System.Serializable]
    public class DayData
    {
        public int day;
        public float gold;
        public float fame;
        public float visitors;
        
        public DayData(int day, float gold, float fame, float visitors)
        {
            this.day = day;
            this.gold = gold;
            this.fame = fame;
            this.visitors = visitors;
        }
    }

    /// <summary>
    /// 대시보드 차트 UI 컨트롤러
    /// 하루별 골드, 명성도, 방문객 수를 점선 차트로 표시
    /// </summary>
    public class DashboardChart : MonoBehaviour
    {
        [Header("차트 설정")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform chartContent;
        [SerializeField] private RectTransform chartArea;
        
        [Header("축 설정")]
        [SerializeField] private Transform xAxisParent;
        [SerializeField] private Transform yAxisParent;
        [SerializeField] private GameObject axisLabelPrefab;
        
        [Header("라인 설정")]
        [SerializeField] private LineRenderer goldLineRenderer;
        [SerializeField] private LineRenderer fameLineRenderer;
        [SerializeField] private LineRenderer visitorsLineRenderer;
        
        [Header("포인트 설정")]
        [SerializeField] private Transform pointsParent;
        [SerializeField] private GameObject pointPrefab;
        
        [Header("차트 크기 설정")]
        [SerializeField] private float chartWidth = 1000f;
        [SerializeField] private float chartHeight = 400f;
        [SerializeField] private float daySpacing = 100f;
        [SerializeField] private int visibleDays = 5;
        
        [Header("색상 설정")]
        [SerializeField] private Color goldColor = Color.blue;
        [SerializeField] private Color fameColor = Color.green;
        [SerializeField] private Color visitorsColor = Color.red;
        
        [Header("라인 설정")]
        [SerializeField] private Material dashedLineMaterial;
        [SerializeField] private float lineWidth = 3f;
        
        // 데이터
        private List<DayData> chartData = new List<DayData>();
        private float maxGold, maxFame, maxVisitors;
        
        // UI 요소들
        private List<GameObject> xAxisLabels = new List<GameObject>();
        private List<GameObject> yAxisLabels = new List<GameObject>();
        private List<GameObject> dataPoints = new List<GameObject>();

        void Start()
        {
            InitializeChart();
            LoadSampleData();
            UpdateChart();
        }

        /// <summary>
        /// 차트 초기화
        /// </summary>
        private void InitializeChart()
        {
            // ScrollRect 설정
            if (scrollRect != null)
            {
                scrollRect.horizontal = true;
                scrollRect.vertical = false;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
            }

            // LineRenderer 초기 설정
            SetupLineRenderer(goldLineRenderer, goldColor);
            SetupLineRenderer(fameLineRenderer, fameColor);
            SetupLineRenderer(visitorsLineRenderer, visitorsColor);

            Debug.Log("[DashboardChart] 차트 초기화 완료");
        }

        /// <summary>
        /// LineRenderer 설정
        /// </summary>
        private void SetupLineRenderer(LineRenderer lineRenderer, Color color)
        {
            if (lineRenderer == null) return;

            lineRenderer.material = dashedLineMaterial;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.useWorldSpace = false;
            lineRenderer.sortingOrder = 1;
        }

        /// <summary>
        /// 샘플 데이터 로드
        /// </summary>
        private void LoadSampleData()
        {
            chartData = new List<DayData>
            {
                new DayData(1, 100, 50, 200),
                new DayData(2, 150, 70, 250),
                new DayData(3, 120, 60, 180),
                new DayData(4, 200, 90, 300),
                new DayData(5, 180, 85, 280),
                new DayData(6, 250, 110, 350),
                new DayData(7, 300, 130, 400),
                new DayData(8, 280, 125, 380),
                new DayData(9, 350, 150, 450),
                new DayData(10, 400, 170, 500),
                new DayData(11, 380, 165, 480),
                new DayData(12, 450, 190, 550),
                new DayData(13, 500, 210, 600),
                new DayData(14, 480, 205, 580),
                new DayData(15, 550, 230, 650)
            };

            Debug.Log($"[DashboardChart] 샘플 데이터 로드 완료: {chartData.Count}일");
        }

        /// <summary>
        /// 외부에서 데이터 설정
        /// </summary>
        public void SetChartData(List<DayData> newData)
        {
            chartData = new List<DayData>(newData);
            UpdateChart();
            Debug.Log($"[DashboardChart] 차트 데이터 업데이트: {chartData.Count}일");
        }

        /// <summary>
        /// 차트 업데이트
        /// </summary>
        public void UpdateChart()
        {
            if (chartData == null || chartData.Count == 0)
            {
                Debug.LogWarning("[DashboardChart] 차트 데이터가 없습니다.");
                return;
            }

            CalculateMaxValues();
            SetupChartDimensions();
            ClearPreviousChart();
            DrawChart();
            CreateAxisLabels();
            SetInitialScrollPosition();

            Debug.Log("[DashboardChart] 차트 업데이트 완료");
        }

        /// <summary>
        /// 최대값 계산
        /// </summary>
        private void CalculateMaxValues()
        {
            maxGold = chartData.Max(d => d.gold);
            maxFame = chartData.Max(d => d.fame);
            maxVisitors = chartData.Max(d => d.visitors);

            Debug.Log($"[DashboardChart] 최대값 - 골드: {maxGold}, 명성도: {maxFame}, 방문객: {maxVisitors}");
        }

        /// <summary>
        /// 차트 크기 설정
        /// </summary>
        private void SetupChartDimensions()
        {
            if (chartContent != null)
            {
                float totalWidth = (chartData.Count - 1) * daySpacing + 200f; // 여백 추가
                chartContent.sizeDelta = new Vector2(totalWidth, chartHeight);
            }
        }

        /// <summary>
        /// 이전 차트 요소들 정리
        /// </summary>
        private void ClearPreviousChart()
        {
            // 축 라벨 정리
            foreach (var label in xAxisLabels)
            {
                if (label != null) DestroyImmediate(label);
            }
            xAxisLabels.Clear();

            foreach (var label in yAxisLabels)
            {
                if (label != null) DestroyImmediate(label);
            }
            yAxisLabels.Clear();

            // 데이터 포인트 정리
            foreach (var point in dataPoints)
            {
                if (point != null) DestroyImmediate(point);
            }
            dataPoints.Clear();
        }

        /// <summary>
        /// 차트 그리기
        /// </summary>
        private void DrawChart()
        {
            DrawLine(goldLineRenderer, d => d.gold, maxGold);
            DrawLine(fameLineRenderer, d => d.fame, maxFame);
            DrawLine(visitorsLineRenderer, d => d.visitors, maxVisitors);
        }

        /// <summary>
        /// 라인 그리기
        /// </summary>
        private void DrawLine(LineRenderer lineRenderer, System.Func<DayData, float> valueSelector, float maxValue)
        {
            if (lineRenderer == null) return;

            Vector3[] positions = new Vector3[chartData.Count];
            
            for (int i = 0; i < chartData.Count; i++)
            {
                float x = i * daySpacing;
                float y = (valueSelector(chartData[i]) / maxValue) * chartHeight;
                positions[i] = new Vector3(x, y, 0);

                // 데이터 포인트 생성
                CreateDataPoint(new Vector2(x, y), lineRenderer.startColor);
            }

            lineRenderer.positionCount = positions.Length;
            lineRenderer.SetPositions(positions);
        }

        /// <summary>
        /// 데이터 포인트 생성
        /// </summary>
        private void CreateDataPoint(Vector2 position, Color color)
        {
            if (pointPrefab == null || pointsParent == null) return;

            GameObject point = Instantiate(pointPrefab, pointsParent);
            RectTransform pointRect = point.GetComponent<RectTransform>();
            
            if (pointRect != null)
            {
                pointRect.anchoredPosition = position;
            }

            // 색상 설정
            Image pointImage = point.GetComponent<Image>();
            if (pointImage != null)
            {
                pointImage.color = color;
            }

            dataPoints.Add(point);
        }

        /// <summary>
        /// 축 라벨 생성
        /// </summary>
        private void CreateAxisLabels()
        {
            CreateXAxisLabels();
            CreateYAxisLabels();
        }

        /// <summary>
        /// X축 라벨 생성 (일자)
        /// </summary>
        private void CreateXAxisLabels()
        {
            if (axisLabelPrefab == null || xAxisParent == null) return;

            for (int i = 0; i < chartData.Count; i++)
            {
                GameObject label = Instantiate(axisLabelPrefab, xAxisParent);
                RectTransform labelRect = label.GetComponent<RectTransform>();
                TextMeshProUGUI labelText = label.GetComponent<TextMeshProUGUI>();

                if (labelRect != null)
                {
                    float x = i * daySpacing;
                    labelRect.anchoredPosition = new Vector2(x, -30f);
                }

                if (labelText != null)
                {
                    labelText.text = $"Day {chartData[i].day}";
                    labelText.fontSize = 12f;
                    labelText.color = Color.black;
                    labelText.alignment = TextAlignmentOptions.Center;
                }

                xAxisLabels.Add(label);
            }
        }

        /// <summary>
        /// Y축 라벨 생성 (값)
        /// </summary>
        private void CreateYAxisLabels()
        {
            if (axisLabelPrefab == null || yAxisParent == null) return;

            // 골드 축
            CreateYAxisLabelsForValue("Gold", maxGold, goldColor, -100f);
            // 명성도 축  
            CreateYAxisLabelsForValue("Fame", maxFame, fameColor, -200f);
            // 방문객 축
            CreateYAxisLabelsForValue("Visitors", maxVisitors, visitorsColor, -300f);
        }

        /// <summary>
        /// 특정 값에 대한 Y축 라벨 생성
        /// </summary>
        private void CreateYAxisLabelsForValue(string labelName, float maxValue, Color color, float xOffset)
        {
            int labelCount = 5;
            for (int i = 0; i <= labelCount; i++)
            {
                GameObject label = Instantiate(axisLabelPrefab, yAxisParent);
                RectTransform labelRect = label.GetComponent<RectTransform>();
                TextMeshProUGUI labelText = label.GetComponent<TextMeshProUGUI>();

                if (labelRect != null)
                {
                    float y = (i / (float)labelCount) * chartHeight;
                    labelRect.anchoredPosition = new Vector2(xOffset, y);
                }

                if (labelText != null)
                {
                    float value = (i / (float)labelCount) * maxValue;
                    labelText.text = $"{value:F0}";
                    labelText.fontSize = 10f;
                    labelText.color = color;
                    labelText.alignment = TextAlignmentOptions.Center;
                }

                yAxisLabels.Add(label);
            }

            // 축 제목
            GameObject titleLabel = Instantiate(axisLabelPrefab, yAxisParent);
            RectTransform titleRect = titleLabel.GetComponent<RectTransform>();
            TextMeshProUGUI titleText = titleLabel.GetComponent<TextMeshProUGUI>();

            if (titleRect != null)
            {
                titleRect.anchoredPosition = new Vector2(xOffset, chartHeight + 20f);
            }

            if (titleText != null)
            {
                titleText.text = labelName;
                titleText.fontSize = 14f;
                titleText.color = color;
                titleText.fontStyle = FontStyles.Bold;
                titleText.alignment = TextAlignmentOptions.Center;
            }

            yAxisLabels.Add(titleLabel);
        }

        /// <summary>
        /// 초기 스크롤 위치 설정 (1~5일 보이게)
        /// </summary>
        private void SetInitialScrollPosition()
        {
            if (scrollRect != null)
            {
                scrollRect.horizontalNormalizedPosition = 0f;
            }
        }

        /// <summary>
        /// 특정 일자로 스크롤
        /// </summary>
        public void ScrollToDay(int day)
        {
            if (scrollRect == null || chartData.Count == 0) return;

            int dayIndex = chartData.FindIndex(d => d.day == day);
            if (dayIndex == -1) return;

            float totalWidth = chartContent.sizeDelta.x;
            float viewportWidth = scrollRect.viewport.rect.width;
            float targetX = dayIndex * daySpacing;
            
            float normalizedPosition = Mathf.Clamp01(targetX / (totalWidth - viewportWidth));
            scrollRect.horizontalNormalizedPosition = normalizedPosition;

            Debug.Log($"[DashboardChart] {day}일로 스크롤 이동");
        }

        /// <summary>
        /// 데이터 추가
        /// </summary>
        public void AddDayData(DayData newData)
        {
            chartData.Add(newData);
            UpdateChart();
            
            // 새로 추가된 일자로 스크롤
            ScrollToDay(newData.day);
            
            Debug.Log($"[DashboardChart] 새 데이터 추가: Day {newData.day}");
        }

        /// <summary>
        /// 차트 데이터 초기화
        /// </summary>
        public void ClearChart()
        {
            chartData.Clear();
            ClearPreviousChart();
            
            // LineRenderer 초기화
            if (goldLineRenderer != null) goldLineRenderer.positionCount = 0;
            if (fameLineRenderer != null) fameLineRenderer.positionCount = 0;
            if (visitorsLineRenderer != null) visitorsLineRenderer.positionCount = 0;
            
            Debug.Log("[DashboardChart] 차트 데이터 초기화 완료");
        }

        /// <summary>
        /// 현재 차트 데이터 반환
        /// </summary>
        public List<DayData> GetChartData()
        {
            return new List<DayData>(chartData);
        }

        /// <summary>
        /// 차트 가시성 토글
        /// </summary>
        public void ToggleChart(bool visible)
        {
            gameObject.SetActive(visible);
        }

        #region Unity Editor 전용 메서드
        
        [ContextMenu("테스트 데이터 로드")]
        private void LoadTestData()
        {
            LoadSampleData();
            UpdateChart();
        }

        [ContextMenu("차트 초기화")]
        private void ResetChart()
        {
            ClearChart();
        }

        #endregion

        void OnValidate()
        {
            // Inspector에서 값 변경 시 실시간 업데이트
            if (Application.isPlaying && chartData.Count > 0)
            {
                UpdateChart();
            }
        }
    }
}
