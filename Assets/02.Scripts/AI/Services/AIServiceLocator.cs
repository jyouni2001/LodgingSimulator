using UnityEngine;
using JY.AI.Interfaces;

namespace JY.AI.Services
{
    /// <summary>
    /// AI 시스템의 서비스 로케이터 패턴 구현
    /// 의존성 주입을 통해 결합도를 낮춤
    /// </summary>
    public class AIServiceLocator : MonoBehaviour
    {
        [Header("서비스 설정")]
        [SerializeField] private bool autoInitializeOnStart = true;
        
        // 서비스 인스턴스들
        private static ITimeProvider timeProvider;
        private static IQueueManager queueManager;
        private static IRoomProvider roomProvider;
        private static IPaymentProcessor paymentProcessor;
        private static IObjectPoolManager poolManager;

        // 싱글톤 인스턴스
        public static AIServiceLocator Instance { get; private set; }

        #region Unity 생명주기
        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            if (autoInitializeOnStart)
            {
                InitializeServices();
            }
        }
        #endregion

        #region 서비스 초기화
        /// <summary>
        /// 모든 서비스를 자동으로 초기화
        /// </summary>
        public void InitializeServices()
        {
            AIDebugLogger.Log("ServiceLocator", "서비스 초기화 시작", LogCategory.General, true);

            // TimeSystem 초기화
            var timeSystem = TimeSystem.Instance;
            if (timeSystem != null)
            {
                timeProvider = new TimeSystemAdapter(timeSystem);
                AIDebugLogger.Log("ServiceLocator", "TimeProvider 초기화 완료", LogCategory.General);
            }

            // CounterManager 초기화
            var counterManager = FindObjectOfType<CounterManager>();
            if (counterManager != null)
            {
                queueManager = new CounterManagerAdapter(counterManager);
                AIDebugLogger.Log("ServiceLocator", "QueueManager 초기화 완료", LogCategory.General);
            }

            // RoomManager 초기화
            var roomManager = FindObjectOfType<RoomManager>();
            if (roomManager != null)
            {
                roomProvider = new RoomManagerAdapter(roomManager);
                AIDebugLogger.Log("ServiceLocator", "RoomProvider 초기화 완료", LogCategory.General);
            }

            // PaymentSystem 초기화
            var paymentSystem = PaymentSystem.Instance;
            if (paymentSystem != null)
            {
                paymentProcessor = new PaymentSystemAdapter(paymentSystem);
                AIDebugLogger.Log("ServiceLocator", "PaymentProcessor 초기화 완료", LogCategory.General);
            }

            // AISpawner 초기화
            var aiSpawner = AISpawner.Instance;
            if (aiSpawner != null)
            {
                poolManager = new AISpawnerAdapter(aiSpawner);
                AIDebugLogger.Log("ServiceLocator", "PoolManager 초기화 완료", LogCategory.General);
            }

            AIDebugLogger.Log("ServiceLocator", "서비스 초기화 완료", LogCategory.General, true);
        }

        /// <summary>
        /// 수동으로 서비스 등록
        /// </summary>
        public void RegisterService<T>(T service) where T : class
        {
            if (service is ITimeProvider time)
                timeProvider = time;
            else if (service is IQueueManager queue)
                queueManager = queue;
            else if (service is IRoomProvider room)
                roomProvider = room;
            else if (service is IPaymentProcessor payment)
                paymentProcessor = payment;
            else if (service is IObjectPoolManager pool)
                poolManager = pool;
            
            AIDebugLogger.Log("ServiceLocator", $"서비스 등록: {typeof(T).Name}", LogCategory.General);
        }
        #endregion

        #region 서비스 접근자들
        /// <summary>
        /// 시간 제공자 반환
        /// </summary>
        public static ITimeProvider GetTimeProvider()
        {
            if (timeProvider == null)
            {
                AIDebugLogger.LogWarning("ServiceLocator", "TimeProvider가 등록되지 않았습니다");
            }
            return timeProvider;
        }

        /// <summary>
        /// 대기열 관리자 반환
        /// </summary>
        public static IQueueManager GetQueueManager()
        {
            if (queueManager == null)
            {
                AIDebugLogger.LogWarning("ServiceLocator", "QueueManager가 등록되지 않았습니다");
            }
            return queueManager;
        }

        /// <summary>
        /// 방 제공자 반환
        /// </summary>
        public static IRoomProvider GetRoomProvider()
        {
            if (roomProvider == null)
            {
                AIDebugLogger.LogWarning("ServiceLocator", "RoomProvider가 등록되지 않았습니다");
            }
            return roomProvider;
        }

        /// <summary>
        /// 결제 처리자 반환
        /// </summary>
        public static IPaymentProcessor GetPaymentProcessor()
        {
            if (paymentProcessor == null)
            {
                AIDebugLogger.LogWarning("ServiceLocator", "PaymentProcessor가 등록되지 않았습니다");
            }
            return paymentProcessor;
        }

        /// <summary>
        /// 오브젝트 풀 관리자 반환
        /// </summary>
        public static IObjectPoolManager GetPoolManager()
        {
            if (poolManager == null)
            {
                AIDebugLogger.LogWarning("ServiceLocator", "PoolManager가 등록되지 않았습니다");
            }
            return poolManager;
        }
        #endregion

        #region 유틸리티
        /// <summary>
        /// 모든 서비스가 등록되었는지 확인
        /// </summary>
        public bool AreAllServicesRegistered()
        {
            return timeProvider != null && 
                   queueManager != null && 
                   roomProvider != null && 
                   paymentProcessor != null && 
                   poolManager != null;
        }

        /// <summary>
        /// 서비스 상태 정보 반환
        /// </summary>
        public string GetServiceStatus()
        {
            return $"서비스 상태: " +
                   $"Time({timeProvider != null}), " +
                   $"Queue({queueManager != null}), " +
                   $"Room({roomProvider != null}), " +
                   $"Payment({paymentProcessor != null}), " +
                   $"Pool({poolManager != null})";
        }

        /// <summary>
        /// 서비스 초기화 (Context Menu)
        /// </summary>
        [ContextMenu("서비스 초기화")]
        public void InitializeServicesManual()
        {
            InitializeServices();
        }
        #endregion
    }
}