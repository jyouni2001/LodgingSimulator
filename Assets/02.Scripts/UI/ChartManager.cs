using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace JY
{
    /// <summary>
    /// 차트 매니저 - 게임 데이터를 차트로 표시하는 통합 관리자
    /// </summary>
    public class ChartManager : MonoBehaviour
    {
        [Header("차트 컴포넌트")]
        [SerializeField] private DashboardChart dashboardChart;
        [SerializeField] private Button toggleChartButton;
        [SerializeField] private TextMeshProUGUI toggleButtonText;
        
        [Header("데이터 수집 설정")]
        [SerializeField] private bool autoCollectData = true;
        [SerializeField] private float dataCollectionInterval = 60f; // 1분마다 데이터 수집
        
        [Header("게임 시스템 참조")]
        [SerializeField] private PlayerWallet playerWallet;
        [SerializeField] private ReputationSystem reputationSystem;
        [SerializeField] private AISpawner aiSpawner;
        
        // 데이터 관리
        private List<DayData> gameData = new List<DayData>();
        private int currentDay = 1;
        private float lastCollectionTime;
        private bool chartVisible = false;
        
        // 일일 누적 데이터
        private float dailyGoldEarned = 0f;
        private float dailyFameEarned = 0f;
        private int dailyVisitors = 0;
        private float dayStartGold = 0f;
        private float dayStartFame = 0f;

        void Start()
        {
            InitializeChartManager();
            SetupUI();
            
            if (autoCollectData)
            {
                StartDataCollection();
            }
        }

        void Update()
        {
            if (autoCollectData && Time.time - lastCollectionTime >= dataCollectionInterval)
            {
                CollectCurrentData();
                lastCollectionTime = Time.time;
            }
        }

        /// <summary>
        /// 차트 매니저 초기화
        /// </summary>
        private void InitializeChartManager()
        {
            // 게임 시스템 참조 자동 찾기
            if (playerWallet == null)
                playerWallet = FindFirstObjectByType<PlayerWallet>();
            
            if (reputationSystem == null)
                reputationSystem = FindFirstObjectByType<ReputationSystem>();
            
            if (aiSpawner == null)
                aiSpawner = FindFirstObjectByType<AISpawner>();

            // 차트 초기 상태 설정
            if (dashboardChart != null)
            {
                dashboardChart.ToggleChart(chartVisible);
            }

            // 시작 시점의 골드와 명성도 기록
            if (playerWallet != null)
                dayStartGold = playerWallet.money;
            
            if (reputationSystem != null)
                dayStartFame = reputationSystem.CurrentReputation;

            Debug.Log("[ChartManager] 차트 매니저 초기화 완료");
        }

        /// <summary>
        /// UI 설정
        /// </summary>
        private void SetupUI()
        {
            if (toggleChartButton != null)
            {
                toggleChartButton.onClick.AddListener(ToggleChart);
                UpdateToggleButtonText();
            }
        }

        /// <summary>
        /// 데이터 수집 시작
        /// </summary>
        private void StartDataCollection()
        {
            lastCollectionTime = Time.time;
            Debug.Log("[ChartManager] 자동 데이터 수집 시작");
        }

        /// <summary>
        /// 현재 데이터 수집
        /// </summary>
        private void CollectCurrentData()
        {
            float currentGold = playerWallet != null ? playerWallet.money : 0f;
            float currentFame = reputationSystem != null ? reputationSystem.CurrentReputation : 0f;
            
            // 일일 증가량 계산
            dailyGoldEarned = currentGold - dayStartGold;
            dailyFameEarned = currentFame - dayStartFame;
            
            // 방문객 수는 AI 스포너에서 가져오기 (예시)
            dailyVisitors = GetDailyVisitorCount();

            Debug.Log($"[ChartManager] 데이터 수집 - Day {currentDay}: Gold +{dailyGoldEarned:F0}, Fame +{dailyFameEarned:F0}, Visitors {dailyVisitors}");
        }

        /// <summary>
        /// 일일 방문객 수 계산 (예시 구현)
        /// </summary>
        private int GetDailyVisitorCount()
        {
            // 실제 구현에서는 AISpawner나 다른 시스템에서 방문객 수를 가져와야 함
            // 여기서는 예시로 랜덤 값 사용
            if (aiSpawner != null)
            {
                // AISpawner에 일일 방문객 수를 추적하는 메서드가 있다면 사용
                return Random.Range(150, 300);
            }
            
            return Random.Range(100, 250);
        }

        /// <summary>
        /// 하루 종료 시 데이터 저장
        /// </summary>
        public void EndDay()
        {
            CollectCurrentData();
            
            DayData dayData = new DayData(currentDay, dailyGoldEarned, dailyFameEarned, dailyVisitors);
            gameData.Add(dayData);
            
            // 차트 업데이트
            if (dashboardChart != null)
            {
                dashboardChart.AddDayData(dayData);
            }
            
            // 다음 날 준비
            currentDay++;
            dayStartGold = playerWallet != null ? playerWallet.money : 0f;
            dayStartFame = reputationSystem != null ? reputationSystem.CurrentReputation : 0f;
            dailyGoldEarned = 0f;
            dailyFameEarned = 0f;
            dailyVisitors = 0;
            
            Debug.Log($"[ChartManager] Day {currentDay - 1} 데이터 저장 완료. 다음 날: Day {currentDay}");
        }

        /// <summary>
        /// 차트 표시/숨김 토글
        /// </summary>
        public void ToggleChart()
        {
            chartVisible = !chartVisible;
            
            if (dashboardChart != null)
            {
                dashboardChart.ToggleChart(chartVisible);
            }
            
            UpdateToggleButtonText();
            Debug.Log($"[ChartManager] 차트 {(chartVisible ? "표시" : "숨김")}");
        }

        /// <summary>
        /// 토글 버튼 텍스트 업데이트
        /// </summary>
        private void UpdateToggleButtonText()
        {
            if (toggleButtonText != null)
            {
                toggleButtonText.text = chartVisible ? "차트 숨기기" : "차트 보기";
            }
        }

        /// <summary>
        /// 수동으로 데이터 추가
        /// </summary>
        public void AddManualData(int day, float gold, float fame, float visitors)
        {
            DayData newData = new DayData(day, gold, fame, visitors);
            gameData.Add(newData);
            
            if (dashboardChart != null)
            {
                dashboardChart.AddDayData(newData);
            }
            
            Debug.Log($"[ChartManager] 수동 데이터 추가: Day {day}");
        }

        /// <summary>
        /// 차트 데이터 초기화
        /// </summary>
        public void ResetChartData()
        {
            gameData.Clear();
            currentDay = 1;
            dailyGoldEarned = 0f;
            dailyFameEarned = 0f;
            dailyVisitors = 0;
            
            if (dashboardChart != null)
            {
                dashboardChart.ClearChart();
            }
            
            Debug.Log("[ChartManager] 차트 데이터 초기화 완료");
        }

        /// <summary>
        /// 특정 일자로 스크롤
        /// </summary>
        public void ScrollToDay(int day)
        {
            if (dashboardChart != null)
            {
                dashboardChart.ScrollToDay(day);
            }
        }

        /// <summary>
        /// 현재 차트 데이터 반환
        /// </summary>
        public List<DayData> GetCurrentData()
        {
            return new List<DayData>(gameData);
        }

        /// <summary>
        /// 데이터 수집 간격 설정
        /// </summary>
        public void SetDataCollectionInterval(float interval)
        {
            dataCollectionInterval = interval;
            Debug.Log($"[ChartManager] 데이터 수집 간격 변경: {interval}초");
        }

        /// <summary>
        /// 자동 데이터 수집 토글
        /// </summary>
        public void ToggleAutoCollection(bool enabled)
        {
            autoCollectData = enabled;
            Debug.Log($"[ChartManager] 자동 데이터 수집 {(enabled ? "활성화" : "비활성화")}");
        }

        #region Unity Editor 전용 메서드
        
        [ContextMenu("테스트 데이터 생성")]
        private void GenerateTestData()
        {
            ResetChartData();
            
            for (int i = 1; i <= 15; i++)
            {
                float gold = Random.Range(100f, 500f);
                float fame = Random.Range(50f, 200f);
                float visitors = Random.Range(150f, 600f);
                
                AddManualData(i, gold, fame, visitors);
            }
            
            Debug.Log("[ChartManager] 테스트 데이터 생성 완료");
        }

        [ContextMenu("하루 종료 시뮬레이션")]
        private void SimulateEndDay()
        {
            EndDay();
        }

        #endregion

        void OnDestroy()
        {
            if (toggleChartButton != null)
            {
                toggleChartButton.onClick.RemoveListener(ToggleChart);
            }
        }
    }
}
