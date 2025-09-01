using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JY;
using UnityEngine.Animations;

namespace JY
{
public interface IRoomDetector
{
    GameObject[] GetDetectedRooms();
    void DetectRooms();
}

public class AIAgent : MonoBehaviour
{
    #region 비공개 변수
    private NavMeshAgent agent;                    // AI 이동 제어를 위한 네비메시 에이전트
    private RoomManager roomManager;               // 룸 매니저 참조
    private Transform counterPosition;             // 카운터 위치
    private static List<RoomInfo> roomList = new List<RoomInfo>();  // 동적 룸 정보 리스트
    private Transform spawnPoint;                  // AI 생성/소멸 지점
    private int currentRoomIndex = -1;            // 현재 사용 중인 방 인덱스 (-1은 미사용)
    private AISpawner spawner;                    // AI 스포너 참조
    private float arrivalDistance = 0.5f;         // 도착 판정 거리

    private bool isInQueue = false;               // 대기열에 있는지 여부
    private Vector3 targetQueuePosition;          // 대기열 목표 위치
    private bool isWaitingForService = false;     // 서비스 대기 중인지 여부

    private AIState currentState = AIState.MovingToQueue;  // 현재 AI 상태
    private float counterWaitTime = 5f;           // 카운터 처리 시간
    private string currentDestination = "대기열로 이동 중";  // 현재 목적지 (UI 표시용)
    private bool isBeingServed = false;           // 서비스 받고 있는지 여부

    private static readonly object lockObject = new object();  // 스레드 동기화용 잠금 객체
    private Coroutine wanderingCoroutine;         // 배회 코루틴 참조
    private Coroutine roomUseCoroutine;           // 방 사용 코루틴 참조
    private Coroutine roomWanderingCoroutine;     // 방 내부 배회 코루틴 참조

    private Coroutine useWanderingCoroutine;  // 방 외부 배회 코루틴 참조
    private int maxRetries = 3;                   // 위치 찾기 최대 시도 횟수

    [SerializeField] private CounterManager counterManager; // CounterManager 참조
    private TimeSystem timeSystem;                // 시간 시스템 참조
    private int lastBehaviorUpdateHour = -1;      // 마지막 행동 업데이트 시간
    private bool isScheduledForDespawn = false;   // 11시 디스폰 예정인지 여부
    
    [Header("UI 디버그")]
    [Tooltip("모든 AI 머리 위에 행동 상태 텍스트 표시")]
    [SerializeField] private bool debugUIEnabled = true;
    
    // 모든 AI가 공유하는 static 변수
    private static bool globalShowDebugUI = true;
    #endregion

    #region 룸 정보 클래스
    private class RoomInfo
    {
        public Transform transform;               // 룸의 Transform
        public bool isOccupied;                   // 룸 사용 여부
        public float size;                        // 룸 크기
        public GameObject gameObject;             // 룸 게임 오브젝트
        public string roomId;                     // 룸 고유 ID
        public Bounds bounds;                     // 룸의 Bounds

        public RoomInfo(GameObject roomObj)
        {
            gameObject = roomObj;
            transform = roomObj.transform;
            isOccupied = false;

            var collider = roomObj.GetComponent<Collider>();
            size = collider != null ? collider.bounds.size.magnitude * 0.3f : 2f;
            var roomContents = roomObj.GetComponent<RoomContents>();
            bounds = roomContents != null ? roomContents.roomBounds : (collider != null ? collider.bounds : new Bounds(transform.position, Vector3.one * 2f));
            if (collider == null)
            {
                Debug.LogWarning($"룸 {roomObj.name}에 Collider가 없습니다. 기본 크기(2) 사용.");
            }

            Vector3 pos = roomObj.transform.position;
            roomId = $"Room_{pos.x:F0}_{pos.z:F0}";
            Debug.Log($"룸 ID 생성: {roomId} at {pos}, Bounds: {bounds}");
        }
    }
    #endregion

    #region AI 상태 열거형
    private enum AIState
    {
        Wandering,           // 외부 배회
        MovingToQueue,       // 대기열로 이동
        WaitingInQueue,      // 대기열에서 대기
        MovingToRoom,        // 배정된 방으로 이동
        UsingRoom,           // 방 사용
        UseWandering,        // 방 사용 중 배회
        ReportingRoom,       // 방 사용 완료 보고
        ReturningToSpawn,    // 스폰 지점으로 복귀 (디스폰)
        RoomWandering,       // 방 내부 배회
        ReportingRoomQueue   // 방 사용 완료 보고를 위해 대기열로 이동
    }
    #endregion

    #region 이벤트
    public delegate void RoomsUpdatedHandler(GameObject[] rooms);
    private static event RoomsUpdatedHandler OnRoomsUpdated;
    #endregion

    #region 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeStatics()
    {
        roomList = new List<RoomInfo>();
        OnRoomsUpdated -= UpdateRoomList;
        OnRoomsUpdated = null;
        globalShowDebugUI = true;
    }

    void Start()
    {
        if (!InitializeComponents()) return;
        InitializeRoomsIfEmpty();
        timeSystem = TimeSystem.Instance;
        
        // Inspector 설정을 전역 설정에 반영
        globalShowDebugUI = debugUIEnabled;
        
        DetermineInitialBehavior();
    }

    private bool InitializeComponents()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError($"AI {gameObject.name}: NavMeshAgent 컴포넌트가 없습니다.");
            Destroy(gameObject);
            return false;
        }

        GameObject spawn = GameObject.FindGameObjectWithTag("Spawn");
        if (spawn == null)
        {
            Debug.LogError($"AI {gameObject.name}: Spawn 오브젝트를 찾을 수 없습니다.");
            Destroy(gameObject);
            return false;
        }

        roomManager = FindFirstObjectByType<RoomManager>();
        spawnPoint = spawn.transform;

        GameObject counter = GameObject.FindGameObjectWithTag("Counter");
        counterPosition = counter != null ? counter.transform : null;

        if (counterManager == null)
        {
            counterManager = FindFirstObjectByType<CounterManager>();
            if (counterManager == null)
            {
                Debug.LogWarning($"AI {gameObject.name}: CounterManager를 찾을 수 없습니다.");
                counterPosition = null;
            }
        }

        if (NavMesh.GetAreaFromName("Ground") == 0)
        {
            Debug.LogWarning($"AI {gameObject.name}: Ground NavMesh 영역이 설정되지 않았습니다.");
        }

        return true;
    }

    private void InitializeRoomsIfEmpty()
    {
        lock (lockObject)
        {
            if (roomList.Count == 0)
            {
                InitializeRooms();
                if (OnRoomsUpdated == null)
                {
                    OnRoomsUpdated += UpdateRoomList;
                }
            }
        }
    }

    private void DetermineInitialBehavior()
    {
        DetermineBehaviorByTime();
    }
    #endregion

    #region 룸 관리
    private void InitializeRooms()
    {
        roomList.Clear();
        Debug.Log($"AI {gameObject.name}: 룸 초기화 시작");

        var roomDetectors = GameObject.FindObjectsByType<RoomDetector>(FindObjectsSortMode.None);
        if (roomDetectors.Length > 0)
        {
            foreach (var detector in roomDetectors)
            {
                detector.ScanForRooms();
                detector.OnRoomsUpdated += rooms =>
                {
                    if (rooms != null && rooms.Length > 0)
                    {
                        UpdateRoomList(rooms);
                    }
                };
            }
            Debug.Log($"AI {gameObject.name}: RoomDetector로 룸 감지 시작.");
        }
        else
        {
            GameObject[] taggedRooms = GameObject.FindGameObjectsWithTag("Room");
            foreach (GameObject room in taggedRooms)
            {
                if (!roomList.Any(r => r.gameObject == room))
                {
                    roomList.Add(new RoomInfo(room));
                }
            }
            Debug.Log($"AI {gameObject.name}: 태그로 {roomList.Count}개 룸 발견.");
        }

        if (roomList.Count == 0)
        {
            Debug.LogWarning($"AI {gameObject.name}: 룸을 찾을 수 없습니다!");
        }
        else
        {
            Debug.Log($"AI {gameObject.name}: {roomList.Count}개 룸 초기화 완료.");
        }
    }

    public static void UpdateRoomList(GameObject[] newRooms)
    {
        if (newRooms == null || newRooms.Length == 0) return;

        lock (lockObject)
        {
            HashSet<string> processedRoomIds = new HashSet<string>();
            List<RoomInfo> updatedRoomList = new List<RoomInfo>();

            foreach (GameObject room in newRooms)
            {
                if (room != null)
                {
                    RoomInfo newRoom = new RoomInfo(room);
                    if (!processedRoomIds.Contains(newRoom.roomId))
                    {
                        processedRoomIds.Add(newRoom.roomId);
                        var existingRoom = roomList.FirstOrDefault(r => r.roomId == newRoom.roomId);
                        if (existingRoom != null)
                        {
                            newRoom.isOccupied = existingRoom.isOccupied;
                            updatedRoomList.Add(newRoom);
                        }
                        else
                        {
                            updatedRoomList.Add(newRoom);
                        }
                    }
                }
            }

            if (updatedRoomList.Count > 0)
            {
                roomList = updatedRoomList;
                Debug.Log($"룸 리스트 업데이트 완료. 총 룸 수: {roomList.Count}");
            }
        }
    }

    public static void NotifyRoomsUpdated(GameObject[] rooms)
    {
        OnRoomsUpdated?.Invoke(rooms);
    }
    #endregion

    #region 시간 기반 행동 결정
    private void DetermineBehaviorByTime()
    {
        if (timeSystem == null)
        {
            Debug.LogWarning($"AI {gameObject.name}: TimeSystem이 없습니다. 기본 행동으로 전환.");
            FallbackBehavior();
            return;
        }

        int hour = timeSystem.CurrentHour;
        int minute = timeSystem.CurrentMinute;

        // 17:00에 방 사용 중이 아닌 모든 에이전트 강제 디스폰
        if (hour == 17 && minute == 0)
        {
            Handle17OClockForcedDespawn();
            return;
        }

        if (hour >= 0 && hour < 9)
        {
            // 0:00 ~ 9:00
            if (currentRoomIndex != -1)
            {
                TransitionToState(AIState.RoomWandering);
                Debug.Log($"AI {gameObject.name}: 0~9시, 방 내부 배회.");
            }
            else
            {
                FallbackBehavior();
            }
        }
        else if (hour >= 9 && hour < 11)
        {
            // 9:00 ~ 11:00
            if (currentRoomIndex != -1)
            {
                TransitionToState(AIState.ReportingRoomQueue);
                Debug.Log($"AI {gameObject.name}: 9~11시, 방 사용 완료 보고 대기열로 이동.");
            }
            else
            {
                FallbackBehavior();
            }
        }
        else if (hour >= 11 && hour < 17)
        {
            // 11:00 ~ 17:00
            if (currentRoomIndex == -1)
            {
                float randomValue = Random.value;
                if (randomValue < 0.2f)
                {
                    TransitionToState(AIState.MovingToQueue);
                    Debug.Log($"AI {gameObject.name}: 11~17시, 방 없음, 대기열로 이동 (20%).");
                }
                else if (randomValue < 0.8f)
                {
                    TransitionToState(AIState.Wandering);
                    Debug.Log($"AI {gameObject.name}: 11~17시, 방 없음, 외부 배회 (60%).");
                }
                else
                {
                    TransitionToState(AIState.ReturningToSpawn);
                    Debug.Log($"AI {gameObject.name}: 11~17시, 방 없음, 디스폰 (20%).");
                }
            }
            else
            {
                float randomValue = Random.value;
                if (randomValue < 0.5f)
                {
                    TransitionToState(AIState.UseWandering);
                    Debug.Log($"AI {gameObject.name}: 11~17시, 방 있음, 방 외부 배회 (50%).");
                }
                else
                {
                    TransitionToState(AIState.RoomWandering);
                    Debug.Log($"AI {gameObject.name}: 11~17시, 방 있음, 방 내부 배회 (50%).");
                }
            }
        }
        else
        {
            // 17:00 ~ 0:00
            if (currentRoomIndex != -1)
            {
                float randomValue = Random.value;
                if (randomValue < 0.5f)
                {
                    TransitionToState(AIState.UseWandering);
                    Debug.Log($"AI {gameObject.name}: 17~0시, 방 있음, 외부 배회 (50%).");
                }
                else
                {
                    TransitionToState(AIState.RoomWandering);
                    Debug.Log($"AI {gameObject.name}: 17~0시, 방 있음, 방 내부 배회 (50%).");
                }
            }
            else
            {
                FallbackBehavior();
            }
        }

        lastBehaviorUpdateHour = hour;
    }

    /// <summary>
    /// 17:00에 방 사용 중이 아닌 모든 AI를 강제로 디스폰시킵니다.
    /// </summary>
    private void Handle17OClockForcedDespawn()
    {
        // 방 사용 중인 AI는 디스폰하지 않음
        if (IsInRoomRelatedState())
        {
            Debug.Log($"AI {gameObject.name}: 17:00, 방 사용 중이므로 디스폰하지 않음 (상태: {currentState}).");
            return;
        }

        Debug.Log($"AI {gameObject.name}: 17:00, 강제 디스폰 시작 (현재 상태: {currentState}).");

        // 모든 코루틴 강제 종료
        CleanupCoroutines();

        // 대기열에서 강제 제거
        if (isInQueue && counterManager != null)
        {
            counterManager.LeaveQueue(this);
            isInQueue = false;
            isWaitingForService = false;
            Debug.Log($"AI {gameObject.name}: 17:00, 대기열에서 강제 제거됨.");
            
            // 대기열 강제 정리는 마지막에 한 번만 호출 (중복 호출 방지)
            counterManager.ForceCleanupQueue();
        }

        // 강제 디스폰
        TransitionToState(AIState.ReturningToSpawn);
        agent.SetDestination(spawnPoint.position);
        Debug.Log($"AI {gameObject.name}: 17:00, 강제 디스폰 실행.");
    }

    /// <summary>
    /// 방 관련 상태인지 확인합니다.
    /// </summary>
    private bool IsInRoomRelatedState()
    {
        return (currentState == AIState.UsingRoom || 
                currentState == AIState.UseWandering || 
                currentState == AIState.RoomWandering ||
                currentState == AIState.MovingToRoom) && 
                currentRoomIndex != -1;
    }

    /// <summary>
    /// 11시 체크아웃 완료 후 디스폰을 처리합니다.
    /// </summary>
    private void HandleCheckoutDespawn()
    {
        // 방이 없고 배회 중인 AI들을 11시에 디스폰
        if (currentState == AIState.Wandering && currentRoomIndex == -1)
        {
            Debug.Log($"AI {gameObject.name}: 11시, 체크아웃 완료 후 디스폰.");
            TransitionToState(AIState.ReturningToSpawn);
            agent.SetDestination(spawnPoint.position);
        }
    }

    /// <summary>
    /// 중요한 상태인지 확인합니다 (행동 재결정을 방해하면 안 되는 상태).
    /// </summary>
    private bool IsInCriticalState()
    {
        return currentState == AIState.WaitingInQueue || 
               currentState == AIState.MovingToQueue || 
               currentState == AIState.MovingToRoom || 
               currentState == AIState.ReportingRoom ||
               currentState == AIState.ReportingRoomQueue ||
               currentState == AIState.ReturningToSpawn ||
               isInQueue || isWaitingForService ||
               isScheduledForDespawn; // 11시 디스폰 예정인 AI는 행동 재결정하지 않음
    }

    /// <summary>
    /// 방 사용 중인 AI의 시간별 내부/외부 배회를 재결정합니다.
    /// </summary>
    private void RedetermineRoomBehavior()
    {
        if (!IsInRoomRelatedState()) return;

        int hour = timeSystem.CurrentHour;
        
        // 0시에는 무조건 내부 배회
        if (hour == 0)
        {
            if (currentState == AIState.UseWandering)
            {
                Debug.Log($"AI {gameObject.name}: 0시, 방 내부 배회로 전환.");
                TransitionToState(AIState.RoomWandering);
            }
        }
        // 11-17시, 17-24시에는 50/50 확률로 재결정
        else if ((hour >= 11 && hour < 17) || (hour >= 17 && hour < 24))
        {
            float randomValue = Random.value;
            if (randomValue < 0.5f)
            {
                if (currentState != AIState.UseWandering)
                {
                    Debug.Log($"AI {gameObject.name}: {hour}시, 방 외부 배회로 전환 (50%).");
                    TransitionToState(AIState.UseWandering);
                }
            }
            else
            {
                if (currentState != AIState.RoomWandering)
                {
                    Debug.Log($"AI {gameObject.name}: {hour}시, 방 내부 배회로 전환 (50%).");
                    TransitionToState(AIState.RoomWandering);
                }
            }
        }
    }

    private void FallbackBehavior()
    {
        if (counterPosition == null || counterManager == null)
        {
            float randomValue = Random.value;
            if (randomValue < 0.5f)
            {
                TransitionToState(AIState.Wandering);
                Debug.Log($"AI {gameObject.name}: 카운터 없음, 배회 (50%).");
            }
            else
            {
                TransitionToState(AIState.ReturningToSpawn);
                Debug.Log($"AI {gameObject.name}: 카운터 없음, 디스폰 (50%).");
            }
        }
        else
        {
            float randomValue = Random.value;
            if (randomValue < 0.4f)
            {
                TransitionToState(AIState.Wandering);
                Debug.Log($"AI {gameObject.name}: 기본 행동, 배회 (40%).");
            }
            else
            {
                TransitionToState(AIState.MovingToQueue);
                Debug.Log($"AI {gameObject.name}: 기본 행동, 대기열로 이동 (60%).");
            }
        }
    }
    #endregion

    #region 업데이트 및 상태 머신
    void Update()
    {
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"AI {gameObject.name}: NavMesh 벗어남");
            ReturnToPool();
            return;
        }
        
        // Inspector에서 설정이 변경되면 전역 설정 업데이트
        if (globalShowDebugUI != debugUIEnabled)
        {
            globalShowDebugUI = debugUIEnabled;
        }

        // 시간 기반 행동 갱신
        if (timeSystem != null)
        {
            int hour = timeSystem.CurrentHour;
            int minute = timeSystem.CurrentMinute;

            // 17:00에 방 사용 중이 아닌 모든 에이전트 강제 디스폰
            if (hour == 17 && minute == 0)
            {
                Handle17OClockForcedDespawn();
                return;
            }

            // 11시 체크아웃 완료 후 디스폰 체크
            if (hour == 11 && minute == 0 && lastBehaviorUpdateHour != hour)
            {
                HandleCheckoutDespawn();
                lastBehaviorUpdateHour = hour;
                return;
            }

            // 매시간 행동 재결정 (모든 AI 포함)
            if (minute == 0 && hour != lastBehaviorUpdateHour)
            {
                // 디스폰 예정 AI는 행동 재결정하지 않음
                if (isScheduledForDespawn)
                {
                    Debug.Log($"[AIAgent] {gameObject.name}: 11시 디스폰 예정이므로 행동 재결정 생략");
                    lastBehaviorUpdateHour = hour;
                }
                // 중요한 상태가 아닌 경우에만 행동 재결정
                else if (!IsInCriticalState())
                {
                    Debug.Log($"[AIAgent] {gameObject.name}: {hour}시 행동 재결정 시작");
                    DetermineBehaviorByTime();
                    lastBehaviorUpdateHour = hour;
                }
                // 방 사용 중인 AI도 매시간 내부/외부 배회 재결정
                else if (IsInRoomRelatedState())
                {
                    Debug.Log($"[AIAgent] {gameObject.name}: {hour}시 방 배회 재결정");
                    RedetermineRoomBehavior();
                    lastBehaviorUpdateHour = hour;
                }
                else
                {
                    Debug.Log($"[AIAgent] {gameObject.name}: {hour}시 중요한 상태로 행동 재결정 생략 (상태: {currentState})");
                    lastBehaviorUpdateHour = hour;
                }
            }
        }

        switch (currentState)
        {
            case AIState.Wandering:
                break;
            case AIState.MovingToQueue:
            case AIState.WaitingInQueue:
            case AIState.ReportingRoomQueue:
                break;
            case AIState.MovingToRoom:
                if (currentRoomIndex != -1 && currentRoomIndex < roomList.Count)
                {
                    Bounds roomBounds = roomList[currentRoomIndex].bounds;
                    if (!agent.pathPending && agent.remainingDistance < arrivalDistance && roomBounds.Contains(transform.position))
                    {
                        Debug.Log($"AI {gameObject.name}: 룸 {currentRoomIndex + 1}번 도착.");
                        StartCoroutine(UseRoom());
                    }
                }
                break;
            case AIState.UsingRoom:
            case AIState.RoomWandering:
                break;
            case AIState.ReportingRoom:
                break;
            case AIState.ReturningToSpawn:
                if (!agent.pathPending && agent.remainingDistance < arrivalDistance)
                {
                    Debug.Log($"AI {gameObject.name}: 스폰 지점 도착, 디스폰.");
                    ReturnToPool();
                }
                break;
        }
    }
    #endregion

    #region 대기열 동작
    private IEnumerator QueueBehavior()
    {
        Debug.Log($"[AIAgent] {gameObject.name}: QueueBehavior 시작 - 상태: {currentState}, 방 인덱스: {currentRoomIndex}");
        
        if (counterManager == null || counterPosition == null)
        {
            Debug.LogWarning($"[AIAgent] {gameObject.name}: CounterManager 또는 CounterPosition이 없음");
            float randomValue = Random.value;
            if (randomValue < 0.5f)
            {
                TransitionToState(AIState.Wandering);
                wanderingCoroutine = StartCoroutine(WanderingBehavior());
            }
            else
            {
                TransitionToState(AIState.ReturningToSpawn);
                agent.SetDestination(spawnPoint.position);
            }
            yield break;
        }

        Debug.Log($"[AIAgent] {gameObject.name}: 대기열 진입 시도");
        if (!counterManager.TryJoinQueue(this))
        {
            Debug.Log($"[AIAgent] {gameObject.name}: 대기열 진입 실패 - 상태: {currentState}");
            
            // ReportingRoomQueue 상태인 경우 재시도
            if (currentState == AIState.ReportingRoomQueue)
            {
                Debug.Log($"[AIAgent] {gameObject.name}: ReportingRoomQueue 상태이므로 재시도");
                yield return new WaitForSeconds(Random.Range(2f, 5f));
                StartCoroutine(QueueBehavior());
                yield break;
            }
            
            if (currentRoomIndex == -1)
            {
                Debug.Log($"[AIAgent] {gameObject.name}: 방 없음, 대안 행동 선택");
                float randomValue = Random.value;
                if (randomValue < 0.5f)
                {
                    TransitionToState(AIState.Wandering);
                    wanderingCoroutine = StartCoroutine(WanderingBehavior());
                }
                else
                {
                    TransitionToState(AIState.ReturningToSpawn);
                    agent.SetDestination(spawnPoint.position);
                }
            }
            else
            {
                Debug.Log($"[AIAgent] {gameObject.name}: 방 있음, 재시도");
                yield return new WaitForSeconds(Random.Range(1f, 3f));
                StartCoroutine(QueueBehavior());
            }
            yield break;
        }

        Debug.Log($"[AIAgent] {gameObject.name}: 대기열 진입 성공");
        isInQueue = true;
        TransitionToState(currentState == AIState.ReportingRoomQueue ? AIState.ReportingRoomQueue : AIState.WaitingInQueue);

        while (isInQueue)
        {
            // 17시 체크 - 대기열에서도 즉시 디스폰
            if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
            {
                Debug.Log($"AI {gameObject.name}: 대기열 대기 중 17시 감지, 즉시 강제 디스폰.");
                Handle17OClockForcedDespawn();
                yield break;
            }

            if (!agent.pathPending && agent.remainingDistance < arrivalDistance)
            {
                if (counterManager.CanReceiveService(this))
                {
                    Debug.Log($"[AIAgent] {gameObject.name}: 서비스 시작");
                    counterManager.StartService(this);
                    isWaitingForService = true;

                    while (isWaitingForService)
                    {
                        // 서비스 대기 중에도 17시 체크
                        if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
                        {
                            Debug.Log($"AI {gameObject.name}: 서비스 대기 중 17시 감지, 즉시 강제 디스폰.");
                            Handle17OClockForcedDespawn();
                            yield break;
                        }
                        yield return new WaitForSeconds(0.1f);
                    }

                    if (currentState == AIState.ReportingRoomQueue)
                    {
                        Debug.Log($"[AIAgent] {gameObject.name}: ReportingRoomQueue 서비스 완료, ReportRoomVacancy 시작");
                        StartCoroutine(ReportRoomVacancy());
                    }
                    else if (currentRoomIndex != -1)
                    {
                        roomList[currentRoomIndex].isOccupied = false;
                        currentRoomIndex = -1;
                        TransitionToState(AIState.ReturningToSpawn);
                        agent.SetDestination(spawnPoint.position);
                    }
                    else
                    {
                        if (TryAssignRoom())
                        {
                            TransitionToState(AIState.MovingToRoom);
                            agent.SetDestination(roomList[currentRoomIndex].transform.position);
                        }
                        else
                        {
                            float randomValue = Random.value;
                            if (randomValue < 0.5f)
                            {
                                TransitionToState(AIState.Wandering);
                                wanderingCoroutine = StartCoroutine(WanderingBehavior());
                            }
                            else
                            {
                                TransitionToState(AIState.ReturningToSpawn);
                                agent.SetDestination(spawnPoint.position);
                            }
                        }
                    }
                    break;
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private bool TryAssignRoom()
    {
        lock (lockObject)
        {
            var availableRooms = roomList.Select((room, index) => new { room, index })
                                         .Where(r => !r.room.isOccupied)
                                         .Select(r => r.index)
                                         .ToList();

            if (availableRooms.Count == 0)
            {
                Debug.Log($"AI {gameObject.name}: 사용 가능한 룸 없음.");
                return false;
            }

            int selectedRoomIndex = availableRooms[Random.Range(0, availableRooms.Count)];
            if (!roomList[selectedRoomIndex].isOccupied)
            {
                roomList[selectedRoomIndex].isOccupied = true;
                currentRoomIndex = selectedRoomIndex;
                Debug.Log($"AI {gameObject.name}: 룸 {selectedRoomIndex + 1}번 배정됨.");
                return true;
            }

            Debug.Log($"AI {gameObject.name}: 룸 {selectedRoomIndex + 1}번 이미 사용 중.");
            return false;
        }
    }
    #endregion

    #region 상태 전환
    private void TransitionToState(AIState newState)
    {
        CleanupCoroutines();
        if (currentState == AIState.UsingRoom)
        {
            isBeingServed = false;
        }

        currentState = newState;
        currentDestination = GetStateDescription(newState);
        Debug.Log($"AI {gameObject.name}: 상태 변경: {newState}");

        switch (newState)
        {
            case AIState.Wandering:
                wanderingCoroutine = StartCoroutine(WanderingBehavior());
                break;
            case AIState.MovingToQueue:
            case AIState.ReportingRoomQueue:
                StartCoroutine(QueueBehavior());
                break;
            case AIState.MovingToRoom:
                if (currentRoomIndex != -1)
                {
                    agent.SetDestination(roomList[currentRoomIndex].transform.position);
                }
                break;
            case AIState.ReturningToSpawn:
                agent.SetDestination(spawnPoint.position);
                break;
            case AIState.RoomWandering:
                roomWanderingCoroutine = StartCoroutine(RoomWanderingBehavior());
                break;
            case AIState.UseWandering:
                useWanderingCoroutine = StartCoroutine(UseWanderingBehavior());
                break;
        }
    }

    private string GetStateDescription(AIState state)
    {
        return state switch
        {
            AIState.Wandering => "배회 중",
            AIState.MovingToQueue => "대기열로 이동 중",
            AIState.WaitingInQueue => "대기열에서 대기 중",
            AIState.MovingToRoom => $"룸 {currentRoomIndex + 1}번으로 이동 중",
            AIState.UsingRoom => "룸 사용 중",
            AIState.ReportingRoom => "룸 사용 완료 보고 중",
            AIState.ReturningToSpawn => "퇴장 중",
            AIState.RoomWandering => $"룸 {currentRoomIndex + 1}번 내부 배회 중",
            AIState.ReportingRoomQueue => "사용 완료 보고 대기열로 이동 중",
            AIState.UseWandering => $"룸 {currentRoomIndex + 1}번 사용 중 외부 배회",
            _ => "알 수 없는 상태"
        };
    }
    #endregion

    #region 룸 사용
    private IEnumerator UseRoom()
    {
        float roomUseTime = Random.Range(25f, 35f);
        float elapsedTime = 0f;

        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            Debug.LogError($"AI {gameObject.name}: 잘못된 룸 인덱스 {currentRoomIndex}.");
            StartCoroutine(ReportRoomVacancy());
            yield break;
        }

        var room = roomList[currentRoomIndex].gameObject.GetComponent<RoomContents>();
        if (roomManager != null && room != null)
        {
            roomManager.ReportRoomUsage(gameObject.name, room);
        }

        // TransitionToState(AIState.UsingRoom);
        TransitionToState(AIState.UseWandering);

        Debug.Log($"AI {gameObject.name}: 룸 {currentRoomIndex + 1}번 사용 시작.");
        while (elapsedTime < roomUseTime && agent.isOnNavMesh)
        {
            yield return new WaitForSeconds(Random.Range(2f, 5f));
            elapsedTime += Random.Range(2f, 5f);
        }

        Debug.Log($"AI {gameObject.name}: 룸 {currentRoomIndex + 1}번 사용 완료.");
        DetermineBehaviorByTime();
    }

    private IEnumerator ReportRoomVacancy()
    {
        TransitionToState(AIState.ReportingRoom);
        int reportingRoomIndex = currentRoomIndex;
        Debug.Log($"[AIAgent] AI {gameObject.name}: 룸 {reportingRoomIndex + 1}번 사용 완료 보고 시작.");

        lock (lockObject)
        {
            if (reportingRoomIndex >= 0 && reportingRoomIndex < roomList.Count)
            {
                roomList[reportingRoomIndex].isOccupied = false;
                currentRoomIndex = -1;
                Debug.Log($"[AIAgent] 룸 {reportingRoomIndex + 1}번 비워짐.");
            }
        }

        var roomManager = FindFirstObjectByType<RoomManager>();
        if (roomManager != null)
        {
            Debug.Log($"[AIAgent] RoomManager 발견. ProcessRoomPayment 호출 - AI: {gameObject.name}");
            int amount = roomManager.ProcessRoomPayment(gameObject.name);
            Debug.Log($"[AIAgent] AI {gameObject.name}: 룸 결제 완료, 금액: {amount}원");
        }
        else
        {
            Debug.LogError($"[AIAgent] AI {gameObject.name}: RoomManager를 찾을 수 없습니다!");
        }

        if (timeSystem.CurrentHour >= 9 && timeSystem.CurrentHour < 11)
        {
            // 9-11시 체크아웃 후에는 배회하다가 11시에 디스폰 예정
            Debug.Log($"AI {gameObject.name}: 9-11시 체크아웃 완료, 11시 디스폰 예정으로 설정.");
            isScheduledForDespawn = true; // 디스폰 예정 플래그 설정
            TransitionToState(AIState.Wandering);
            wanderingCoroutine = StartCoroutine(WanderingBehaviorWithDespawn());
        }
        else
        {
            DetermineBehaviorByTime();
        }

        yield break;
    }
    #endregion

    #region 배회 동작
    private IEnumerator WanderingBehavior()
    {
        float wanderingTime = Random.Range(20f, 40f); // 배회 시간 증가
        float elapsedTime = 0f;
        
        Debug.Log($"AI {gameObject.name}: 넓은 범위 배회 시작 (시간: {wanderingTime:F1}초)");

        while (currentState == AIState.Wandering && elapsedTime < wanderingTime)
        {
            // 17시 체크 - 배회 중에도 즉시 디스폰
            if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
            {
                Debug.Log($"AI {gameObject.name}: 배회 중 17시 감지, 즉시 강제 디스폰.");
                Handle17OClockForcedDespawn();
                yield break;
            }

            // 새로운 넓은 범위 배회
            Vector3 currentPos = transform.position;
            float wanderDistance = Random.Range(25f, 50f); // 더 넓은 범위
            Vector3 randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0;
            randomDirection.Normalize();
            
            Vector3 targetPoint = currentPos + randomDirection * wanderDistance;
            
            int groundMask = NavMesh.GetAreaFromName("Ground");
            if (groundMask == 0) groundMask = NavMesh.AllAreas;
            
            bool foundValidPosition = false;
            
            // 방을 피해서 배회 위치 찾기
            if (TryGetWanderingPositionAvoidingRooms(targetPoint, 15f, groundMask, out Vector3 validPosition))
            {
                agent.SetDestination(validPosition);
                foundValidPosition = true;
                Debug.Log($"AI {gameObject.name}: 넓은 범위 배회 성공 - 거리: {Vector3.Distance(currentPos, validPosition):F1}");
            }
            else
            {
                // 방을 피하지 못한 경우 기본 방식으로 시도
                WanderOnGround();
                foundValidPosition = true;
                Debug.Log($"AI {gameObject.name}: 기본 배회 사용");
            }
            
            if (foundValidPosition)
            {
                // 목적지까지 이동 대기 (타임아웃 추가)
                float moveTimeout = 15f;
                float moveTimer = 0f;
                
                while (agent.pathPending || agent.remainingDistance > arrivalDistance)
                {
                    if (moveTimer >= moveTimeout)
                    {
                        Debug.Log($"AI {gameObject.name}: 이동 타임아웃, 새로운 목적지 설정");
                        break;
                    }
                    
                    // 17시 체크
                    if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
                    {
                        Debug.Log($"AI {gameObject.name}: 이동 중 17시 감지, 즉시 강제 디스폰.");
                        Handle17OClockForcedDespawn();
                        yield break;
                    }
                    
                    yield return new WaitForSeconds(0.5f);
                    moveTimer += 0.5f;
                }
                
                // 도착 후 대기
                float waitTime = Random.Range(5f, 12f);
                
                // 대기 시간을 쪼개서 17시 체크를 더 자주 함
                float remainingWait = waitTime;
                while (remainingWait > 0 && currentState == AIState.Wandering)
                {
                    yield return new WaitForSeconds(1f);
                    remainingWait -= 1f;
                    
                    // 대기 중에도 17시 체크
                    if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
                    {
                        Debug.Log($"AI {gameObject.name}: 배회 대기 중 17시 감지, 즉시 강제 디스폰.");
                        Handle17OClockForcedDespawn();
                        yield break;
                    }
                }
                
                elapsedTime += waitTime + moveTimer;
            }
            else
            {
                // 위치를 찾지 못한 경우 짧게 대기 후 재시도
                yield return new WaitForSeconds(2f);
                elapsedTime += 2f;
            }
        }

        // 배회 완료 후에도 17시 체크
        if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
        {
            Handle17OClockForcedDespawn();
            yield break;
        }

        Debug.Log($"AI {gameObject.name}: 배회 완료, 다음 행동 결정");
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// 9-11시 체크아웃 후 배회하다가 11시에 디스폰하는 특별한 배회
    /// </summary>
    private IEnumerator WanderingBehaviorWithDespawn()
    {
        Debug.Log($"AI {gameObject.name}: 9-11시 체크아웃 후 배회 시작, 11시에 디스폰 예정.");
        
        while (currentState == AIState.Wandering)
        {
            // 11시가 되면 즉시 디스폰
            if (timeSystem != null && timeSystem.CurrentHour >= 11)
            {
                Debug.Log($"AI {gameObject.name}: 11시 도달, 체크아웃 완료 AI 디스폰.");
                isScheduledForDespawn = false; // 플래그 리셋
                TransitionToState(AIState.ReturningToSpawn);
                agent.SetDestination(spawnPoint.position);
                yield break;
            }

            // 17시 체크도 유지
            if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
            {
                Debug.Log($"AI {gameObject.name}: 배회 중 17시 감지, 즉시 강제 디스폰.");
                isScheduledForDespawn = false; // 플래그 리셋
                Handle17OClockForcedDespawn();
                yield break;
            }

            WanderOnGround();
            float waitTime = Random.Range(3f, 7f);
            
            // 대기 시간을 쪼개서 11시와 17시 체크를 더 자주 함
            float remainingWait = waitTime;
            while (remainingWait > 0 && currentState == AIState.Wandering)
            {
                yield return new WaitForSeconds(1f);
                remainingWait -= 1f;
                
                // 11시 체크
                if (timeSystem != null && timeSystem.CurrentHour >= 11)
                {
                    Debug.Log($"AI {gameObject.name}: 대기 중 11시 도달, 체크아웃 완료 AI 디스폰.");
                    isScheduledForDespawn = false; // 플래그 리셋
                    TransitionToState(AIState.ReturningToSpawn);
                    agent.SetDestination(spawnPoint.position);
                    yield break;
                }
                
                // 17시 체크
                if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
                {
                    Debug.Log($"AI {gameObject.name}: 대기 중 17시 감지, 즉시 강제 디스폰.");
                    isScheduledForDespawn = false; // 플래그 리셋
                    Handle17OClockForcedDespawn();
                    yield break;
                }
            }
        }
    }

    private IEnumerator UseWanderingBehavior()
    {
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            Debug.LogError($"AI {gameObject.name}: 잘못된 룸 인덱스 {currentRoomIndex}.");
            DetermineBehaviorByTime();
            yield break;
        }

        Debug.Log($"AI {gameObject.name}: 방 {currentRoomIndex + 1}번 외부 배회 시작");

        while (currentState == AIState.UseWandering && agent.isOnNavMesh)
        {
            // 17시 체크는 하지 않음 - 방 사용 중인 AI는 계속 작동
            
            // 방을 피해서 외부 배회
            Vector3 currentPos = transform.position;
            float wanderDistance = Random.Range(15f, 25f);
            Vector3 randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0;
            randomDirection.Normalize();
            
            Vector3 targetPoint = currentPos + randomDirection * wanderDistance;
            
            int groundMask = NavMesh.GetAreaFromName("Ground");
            if (groundMask == 0) groundMask = NavMesh.AllAreas;
            
            if (TryGetWanderingPositionAvoidingRooms(targetPoint, 10f, groundMask, out Vector3 validPosition))
            {
                agent.SetDestination(validPosition);
                Debug.Log($"AI {gameObject.name}: 방 외부 배회 - 거리: {Vector3.Distance(currentPos, validPosition):F1}");
                
                // 목적지까지 이동 대기
                yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance < arrivalDistance);
            }
            else
            {
                Debug.Log($"AI {gameObject.name}: 방 외부 배회 위치를 찾지 못함, 기본 배회 사용");
                WanderOnGround();
            }

            float waitTime = Random.Range(4f, 10f);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private IEnumerator RoomWanderingBehavior()
    {
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            Debug.LogError($"AI {gameObject.name}: 잘못된 룸 인덱스 {currentRoomIndex}.");
            DetermineBehaviorByTime();
            yield break;
        }

        float wanderingTime = Random.Range(15f, 30f);
        float elapsedTime = 0f;
        
        Debug.Log($"AI {gameObject.name}: 방 {currentRoomIndex + 1}번 내부 배회 시작");

        while (currentState == AIState.RoomWandering && elapsedTime < wanderingTime && agent.isOnNavMesh)
        {
            // 17시 체크는 하지 않음 - 방 사용 중인 AI는 계속 작동
            
            // 방 내부에서만 배회
            if (TryGetRoomWanderingPosition(currentRoomIndex, out Vector3 roomPosition))
            {
                agent.SetDestination(roomPosition);
                Debug.Log($"AI {gameObject.name}: 방 내부 배회 - 위치: {roomPosition}");
                
                // 목적지까지 이동 대기
                yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance < arrivalDistance);
            }
            else
            {
                // 방 내부 위치를 찾지 못한 경우 기존 방식 사용
                Vector3 roomCenter = roomList[currentRoomIndex].transform.position;
                float roomSize = roomList[currentRoomIndex].size * 0.5f; // 방 크기를 줄여서 확실히 내부에 위치
                if (TryGetValidPosition(roomCenter, roomSize, NavMesh.AllAreas, out Vector3 fallbackPos))
                {
                    agent.SetDestination(fallbackPos);
                }
            }

            float waitTime = Random.Range(3f, 6f);
            yield return new WaitForSeconds(waitTime);
            elapsedTime += waitTime;
        }

        Debug.Log($"AI {gameObject.name}: 방 내부 배회 완료");
        DetermineBehaviorByTime();
    }

    private void WanderOnGround()
    {
        // 더 넓은 범위로 배회 (20-40 유닛 범위)
        float wanderDistance = Random.Range(20f, 40f);
        Vector3 randomDirection = Random.insideUnitSphere;
        randomDirection.y = 0; // Y축 고정
        randomDirection.Normalize();
        
        Vector3 randomPoint = transform.position + randomDirection * wanderDistance;
        
        int groundMask = NavMesh.GetAreaFromName("Ground");
        if (groundMask == 0)
        {
            Debug.LogError($"AI {gameObject.name}: Ground NavMesh 영역 설정되지 않음.");
            return;
        }

        // 방을 피해서 배회하도록 수정
        if (TryGetWanderingPositionAvoidingRooms(randomPoint, 15f, groundMask, out Vector3 validPosition))
        {
            agent.SetDestination(validPosition);
            Debug.Log($"AI {gameObject.name}: 넓은 범위 배회 - 거리: {Vector3.Distance(transform.position, validPosition):F1}");
        }
        else
        {
            // 방을 피하지 못한 경우 기본 방식으로 시도
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 15f, groundMask))
            {
                agent.SetDestination(hit.position);
            }
        }
    }
    
    /// <summary>
    /// 방들을 피해서 배회 위치를 찾는 메서드
    /// </summary>
    private bool TryGetWanderingPositionAvoidingRooms(Vector3 targetPoint, float searchRadius, int layerMask, out Vector3 result)
    {
        result = targetPoint;
        
        for (int i = 0; i < maxRetries * 2; i++) // 더 많이 시도
        {
            Vector3 testPoint = targetPoint + Random.insideUnitSphere * searchRadius;
            testPoint.y = targetPoint.y; // Y축 고정
            
            if (NavMesh.SamplePosition(testPoint, out NavMeshHit hit, searchRadius, layerMask))
            {
                // 방과의 거리 체크
                bool tooCloseToRoom = false;
                foreach (var room in roomList)
                {
                    if (room != null && room.gameObject != null)
                    {
                        float distanceToRoom = Vector3.Distance(hit.position, room.transform.position);
                        // 방 크기의 1.5배 이상 떨어져 있어야 함
                        if (distanceToRoom < room.size * 1.5f)
                        {
                            tooCloseToRoom = true;
                            break;
                        }
                    }
                }
                
                if (!tooCloseToRoom)
                {
                    result = hit.position;
                    return true;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 방 내부에서만 배회하는 위치를 찾는 메서드
    /// </summary>
    private bool TryGetRoomWanderingPosition(int roomIndex, out Vector3 result)
    {
        result = Vector3.zero;
        
        if (roomIndex < 0 || roomIndex >= roomList.Count)
            return false;
            
        var room = roomList[roomIndex];
        if (room == null || room.gameObject == null)
            return false;
            
        Bounds roomBounds = room.bounds;
        Vector3 roomCenter = roomBounds.center;
        
        for (int i = 0; i < maxRetries * 3; i++)
        {
            // 방 내부의 랜덤한 위치 생성
            Vector3 randomPoint = new Vector3(
                Random.Range(roomBounds.min.x + 1f, roomBounds.max.x - 1f),
                roomCenter.y,
                Random.Range(roomBounds.min.z + 1f, roomBounds.max.z - 1f)
            );
            
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                // 방 경계 내부인지 확인
                if (roomBounds.Contains(hit.position))
                {
                    result = hit.position;
                    return true;
                }
            }
        }
        
        return false;
    }
    #endregion

    #region 유틸리티 메서드
    private bool TryGetValidPosition(Vector3 center, float radius, int layerMask, out Vector3 result)
    {
        result = center;
        float searchRadius = radius * 0.8f;

        for (int i = 0; i < maxRetries; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * searchRadius;
            Vector3 randomPoint = center + new Vector3(randomCircle.x, 0, randomCircle.y);

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, searchRadius, layerMask))
            {
                if (Vector3.Distance(hit.position, center) <= searchRadius)
                {
                    result = hit.position;
                    return true;
                }
            }
        }
        return false;
    }

    public void SetSpawner(AISpawner spawnerRef)
    {
        spawner = spawnerRef;
    }

    private void ReturnToPool()
    {
        CleanupCoroutines();
        CleanupResources();

        if (spawner != null)
        {
            spawner.ReturnToPool(gameObject);
        }
        else
        {
            Debug.LogWarning($"AI {gameObject.name}: 스포너 참조 없음, 오브젝트 파괴.");
            Destroy(gameObject);
        }
    }
    #endregion

    #region 정리
    void OnDisable()
    {
        CleanupCoroutines();
        CleanupResources();
    }

    void OnDestroy()
    {
        CleanupCoroutines();
        CleanupResources();
    }

    private void CleanupCoroutines()
    {
        if (wanderingCoroutine != null)
        {
            StopCoroutine(wanderingCoroutine);
            wanderingCoroutine = null;
        }
        if (roomUseCoroutine != null)
        {
            StopCoroutine(roomUseCoroutine);
            roomUseCoroutine = null;
        }
        if (roomWanderingCoroutine != null)
        {
            StopCoroutine(roomWanderingCoroutine);
            roomWanderingCoroutine = null;
        }
        if (useWanderingCoroutine != null) // 새로 추가
        {
            StopCoroutine(useWanderingCoroutine);
            useWanderingCoroutine = null;
        }
    }

    private void CleanupResources()
    {
        //Debug.Log($"[AIAgent] {gameObject.name}: CleanupResources 시작");
        
        if (currentRoomIndex != -1)
        {
            lock (lockObject)
            {
                if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
                {
                    roomList[currentRoomIndex].isOccupied = false;
                    //Debug.Log($"AI {gameObject.name} 정리: 룸 {currentRoomIndex + 1}번 반환.");
                }
                currentRoomIndex = -1;
            }
        }

        isBeingServed = false;
        isInQueue = false;
        isWaitingForService = false;
        isScheduledForDespawn = false; // 디스폰 예정 플래그 리셋

        if (counterManager != null)
        {
            counterManager.LeaveQueue(this);
            //Debug.Log($"[AIAgent] {gameObject.name}: 대기열에서 제거 완료");
        }
        
        //Debug.Log($"[AIAgent] {gameObject.name}: CleanupResources 완료");
    }
    #endregion

    #region UI
    void OnGUI()
    {
        // 디버그 UI가 활성화된 경우에만 표시
        if (!globalShowDebugUI) return;
        
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        if (screenPos.z > 0)
        {
            Vector2 guiPosition = new Vector2(screenPos.x, Screen.height - screenPos.y);
            GUI.Label(new Rect(guiPosition.x - 50, guiPosition.y - 50, 100, 40), currentDestination);
        }
    }
    #endregion

    #region 공개 메서드
    public void InitializeAI()
    {
        currentState = AIState.MovingToQueue;
        currentDestination = "대기열로 이동 중";
        isBeingServed = false;
        isInQueue = false;
        isWaitingForService = false;
        currentRoomIndex = -1;
        lastBehaviorUpdateHour = -1;

        if (agent != null)
        {
            agent.ResetPath();
            DetermineInitialBehavior();
        }
    }

    void OnEnable()
    {
        InitializeAI();
    }

    public void SetQueueDestination(Vector3 position)
    {
        targetQueuePosition = position;
        if (agent != null)
        {
            agent.SetDestination(position);
        }
    }

    public void OnServiceComplete()
    {
        isWaitingForService = false;
        isInQueue = false;
        if (counterManager != null)
        {
            counterManager.LeaveQueue(this);
        }
    }
    #endregion
}
}