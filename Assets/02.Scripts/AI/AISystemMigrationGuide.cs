using UnityEngine;
using JY.AI.Services;

namespace JY
{
    /// <summary>
    /// AI 시스템 마이그레이션 가이드 및 도우미 클래스
    /// 기존 시스템에서 새 시스템으로 전환을 돕는 유틸리티
    /// </summary>
    public class AISystemMigrationGuide : MonoBehaviour
    {
        [Header("마이그레이션 설정")]
        [SerializeField] private bool enableOldSystem = true;
        [SerializeField] private bool enableNewSystem = false;
        [SerializeField] private bool showMigrationLogs = true;

        [Header("시스템 상태")]
        [SerializeField] private int oldAICount = 0;
        [SerializeField] private int newAICount = 0;

        void Start()
        {
            if (showMigrationLogs)
            {
                Debug.Log("=== AI 시스템 마이그레이션 가이드 ===");
                Debug.Log("1단계: AIServiceLocator 설정 확인");
                CheckServiceLocatorSetup();
                
                Debug.Log("2단계: 기존 시스템 상태 확인");
                CheckOldSystemStatus();
                
                Debug.Log("3단계: 새 시스템 준비 상태 확인");
                CheckNewSystemStatus();
                
                Debug.Log("========================================");
            }
        }

        /// <summary>
        /// 서비스 로케이터 설정 확인
        /// </summary>
        private void CheckServiceLocatorSetup()
        {
            var serviceLocator = AIServiceLocator.Instance;
            if (serviceLocator == null)
            {
                Debug.LogError("❌ AIServiceLocator가 씬에 없습니다!");
                Debug.Log("해결방법: 빈 GameObject를 만들고 AIServiceLocator 컴포넌트를 추가하세요.");
                return;
            }

            if (serviceLocator.AreAllServicesRegistered())
            {
                Debug.Log("✅ 모든 서비스가 등록되어 있습니다.");
                Debug.Log($"서비스 상태: {serviceLocator.GetServiceStatus()}");
            }
            else
            {
                Debug.LogWarning("⚠️ 일부 서비스가 등록되지 않았습니다.");
                Debug.Log($"서비스 상태: {serviceLocator.GetServiceStatus()}");
                Debug.Log("해결방법: AIServiceLocator의 'Initialize Services' 버튼을 클릭하세요.");
            }
        }

        /// <summary>
        /// 기존 시스템 상태 확인
        /// </summary>
        private void CheckOldSystemStatus()
        {
            // 기존 AIAgent 개수 확인
            var oldAgents = FindObjectsOfType<AIAgent>();
            oldAICount = oldAgents.Length;
            Debug.Log($"기존 AIAgent 개수: {oldAICount}개");

            // 기존 RoomDetector 확인
            var oldRoomDetectors = FindObjectsOfType<RoomDetector>();
            Debug.Log($"기존 RoomDetector 개수: {oldRoomDetectors.Length}개");

            if (oldRoomDetectors.Length > 0)
            {
                Debug.Log("⚠️ 기존 RoomDetector가 발견되었습니다. 나중에 비활성화해야 합니다.");
            }
        }

        /// <summary>
        /// 새 시스템 준비 상태 확인
        /// </summary>
        private void CheckNewSystemStatus()
        {
            // 새 AIAgentRefactored 개수 확인
            var newAgents = FindObjectsOfType<AIAgentRefactored>();
            newAICount = newAgents.Length;
            Debug.Log($"새 AIAgentRefactored 개수: {newAICount}개");

            // 새 RoomDetectorSimplified 확인
            var newRoomDetectors = FindObjectsOfType<JY.RoomDetection.RoomDetectorSimplified>();
            Debug.Log($"새 RoomDetectorSimplified 개수: {newRoomDetectors.Length}개");

            if (newRoomDetectors.Length == 0)
            {
                Debug.LogWarning("⚠️ 새 RoomDetectorSimplified가 없습니다. 나중에 추가해야 합니다.");
            }
        }

        /// <summary>
        /// 1단계: 서비스 로케이터 자동 설정
        /// </summary>
        [ContextMenu("1단계: 서비스 로케이터 설정")]
        public void Step1_SetupServiceLocator()
        {
            Debug.Log("=== 1단계: 서비스 로케이터 설정 시작 ===");

            // 이미 있는지 확인
            var existing = FindObjectOfType<AIServiceLocator>();
            if (existing != null)
            {
                Debug.Log("✅ AIServiceLocator가 이미 존재합니다.");
                existing.InitializeServices();
                return;
            }

            // 새로 생성
            GameObject serviceLocatorObj = new GameObject("AIServiceLocator");
            var serviceLocator = serviceLocatorObj.AddComponent<AIServiceLocator>();
            
            Debug.Log("✅ AIServiceLocator가 생성되었습니다.");
            serviceLocator.InitializeServices();
            
            Debug.Log("=== 1단계 완료 ===");
        }

        /// <summary>
        /// 2단계: 새 RoomDetector 설정
        /// </summary>
        [ContextMenu("2단계: 새 RoomDetector 설정")]
        public void Step2_SetupNewRoomDetector()
        {
            Debug.Log("=== 2단계: 새 RoomDetector 설정 시작 ===");

            // 기존 RoomDetector 비활성화 (삭제하지는 않음)
            var oldDetectors = FindObjectsOfType<RoomDetector>();
            foreach (var detector in oldDetectors)
            {
                detector.gameObject.SetActive(false);
                Debug.Log($"기존 RoomDetector '{detector.name}' 비활성화됨");
            }

            // 새 RoomDetectorSimplified 생성
            var existing = FindObjectOfType<JY.RoomDetection.RoomDetectorSimplified>();
            if (existing == null)
            {
                GameObject newDetectorObj = new GameObject("RoomDetectorSimplified");
                var newDetector = newDetectorObj.AddComponent<JY.RoomDetection.RoomDetectorSimplified>();
                Debug.Log("✅ 새 RoomDetectorSimplified가 생성되었습니다.");
            }
            else
            {
                Debug.Log("✅ RoomDetectorSimplified가 이미 존재합니다.");
            }

            Debug.Log("=== 2단계 완료 ===");
        }

        /// <summary>
        /// 3단계: AI 프리팹 업데이트
        /// </summary>
        [ContextMenu("3단계: AI 프리팹 업데이트 가이드")]
        public void Step3_UpdateAIPrefabs()
        {
            Debug.Log("=== 3단계: AI 프리팹 업데이트 가이드 ===");
            Debug.Log("수동으로 해야 할 작업들:");
            Debug.Log("1. AI 프리팹을 열어서 AIAgent 컴포넌트를 제거");
            Debug.Log("2. AIAgentRefactored 컴포넌트를 추가");
            Debug.Log("3. 필요한 설정값들을 복사");
            Debug.Log("4. AISpawner의 aiPrefab 참조를 업데이트된 프리팹으로 변경");
            Debug.Log("=================================");
        }

        /// <summary>
        /// 4단계: 점진적 테스트
        /// </summary>
        [ContextMenu("4단계: 시스템 테스트")]
        public void Step4_TestSystem()
        {
            Debug.Log("=== 4단계: 시스템 테스트 ===");
            
            CheckServiceLocatorSetup();
            CheckNewSystemStatus();

            var serviceLocator = AIServiceLocator.Instance;
            if (serviceLocator != null && serviceLocator.AreAllServicesRegistered())
            {
                Debug.Log("✅ 새 시스템이 테스트 준비 완료!");
                Debug.Log("이제 게임을 실행해서 AI 동작을 확인해보세요.");
            }
            else
            {
                Debug.LogError("❌ 시스템 설정이 완료되지 않았습니다.");
            }
            
            Debug.Log("=== 4단계 완료 ===");
        }

        /// <summary>
        /// 전체 마이그레이션 실행
        /// </summary>
        [ContextMenu("🚀 전체 마이그레이션 실행")]
        public void RunFullMigration()
        {
            Debug.Log("🚀 전체 AI 시스템 마이그레이션 시작!");
            
            Step1_SetupServiceLocator();
            Step2_SetupNewRoomDetector();
            Step3_UpdateAIPrefabs();
            Step4_TestSystem();
            
            Debug.Log("🎉 마이그레이션 완료! 이제 AI 프리팹을 수동으로 업데이트하세요.");
        }
    }
}