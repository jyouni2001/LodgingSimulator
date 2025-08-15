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

    // 코루틴 관리
    private Coroutine wanderingCoroutine;         // 배회 코루틴 참조
    private Coroutine roomUseCoroutine;           // 방 사용 코루틴 참조
    private Coroutine roomWanderingCoroutine;     // 방 내부 배회 코루틴 참조
    private Coroutine useWanderingCoroutine;      // 방 외부 배회 코루틴 참조
    private Coroutine queueBehaviorCoroutine;     // 대기열 행동 코루틴 참조
    
    private int maxRetries = 3;                   // 위치 찾기 최대 시도 횟수

    [SerializeField] private CounterManager counterManager; // CounterManager 참조
    private TimeSystem timeSystem;                // 시간 시스템 참조
    private int lastBehaviorUpdateHour = -1;      // 마지막 행동 업데이트 시간
    private bool isScheduledForDespawn = false;   // 11시 디스폰 예정인지 여부
    
    // 체크아웃 시간 분산을 위한 개인별 체크아웃 시간
    private float personalCheckoutTime = -1f;     // 개인별 체크아웃 시간 (9:00-10:00 사이)
    
    [Header("UI 디버그")]
    [Tooltip("모든 AI 머리 위에 행동 상태 텍스트 표시")]
    [SerializeField] private bool debugUIEnabled = true;
    
    // 디버그 UI 표시 여부
    private bool globalShowDebugUI = true;
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

    #region 초기화

    void Start()
    {
        if (!InitializeComponents()) return;
        timeSystem = TimeSystem.Instance;
        
        // Inspector 설정을 전역 설정에 반영
        globalShowDebugUI = debugUIEnabled;
        
        // 개인별 체크아웃 시간 초기화 (9:00-10:00 사이 랜덤)
        InitializePersonalCheckoutTime();
        
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

        roomManager = RoomManager.Instance;
        if (roomManager == null)
        {
            Debug.LogError($"AI {gameObject.name}: RoomManager.Instance를 찾을 수 없습니다.");
            Destroy(gameObject);
            return false;
        }
        
        spawnPoint = spawn.transform;

        // 동적으로 카운터 찾기 - 런타임에 설치된 카운터 감지
        FindNearestCounter();
        Debug.Log($"AI {gameObject.name}: InitializeComponents 완료");

        if (NavMesh.GetAreaFromName("Ground") == 0)
        {
            Debug.LogWarning($"AI {gameObject.name}: Ground NavMesh 영역이 설정되지 않았습니다.");
        }

        return true;
    }
    
    /// <summary>
    /// 가장 가까운 카운터를 찾는 메서드 (동적으로 설치된 카운터 감지)
    /// </summary>
    private void FindNearestCounter()
    {
        // Counter 태그를 가진 모든 GameObject 찾기
        GameObject[] counters = GameObject.FindGameObjectsWithTag("Counter");
        
        Debug.Log($"AI {gameObject.name}: Counter 태그 오브젝트 {counters.Length}개 발견");
        
        if (counters.Length == 0)
        {
            Debug.LogWarning($"AI {gameObject.name}: Counter 태그를 가진 오브젝트를 찾을 수 없습니다.");
            counterPosition = null;
            return;
        }
        
        // 가장 가까운 카운터 찾기
        GameObject nearestCounter = null;
        float minDistance = float.MaxValue;
        
        foreach (GameObject counter in counters)
        {
            // CounterManager 컴포넌트가 있는지 확인
            CounterManager counterMgr = counter.GetComponent<CounterManager>();
            if (counterMgr == null) continue;
            
            float distance = Vector3.Distance(transform.position, counter.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestCounter = counter;
            }
        }
        
        if (nearestCounter != null)
        {
            counterPosition = nearestCounter.transform;
            counterManager = nearestCounter.GetComponent<CounterManager>();
            Debug.Log($"AI {gameObject.name}: 가장 가까운 카운터 발견 - {nearestCounter.name} (거리: {minDistance:F1})");
            Debug.Log($"AI {gameObject.name}: 카운터 설정 완료 - Position: {counterPosition != null}, Manager: {counterManager != null}");
        }
        else
        {
            Debug.LogWarning($"AI {gameObject.name}: 유효한 CounterManager를 가진 카운터를 찾을 수 없습니다.");
            counterPosition = null;
        }
    }
    
    /// <summary>
    /// 개인별 체크아웃 시간을 초기화합니다 (9:00-10:00 사이 랜덤)
    /// </summary>
    private void InitializePersonalCheckoutTime()
    {
        // 9:00(540분)부터 10:00(600분) 사이의 랜덤한 시간
        float randomMinutes = Random.Range(0f, 60f);
        personalCheckoutTime = 540f + randomMinutes; // 9시 + 랜덤 분
    }
    
    /// <summary>
    /// 현재 시간이 개인별 체크아웃 시간인지 확인합니다
    /// </summary>
    private bool IsPersonalCheckoutTime()
    {
        if (timeSystem == null || personalCheckoutTime < 0) return false;
        
        float currentTimeInMinutes = timeSystem.CurrentHour * 60f + timeSystem.CurrentMinute;
        return Mathf.Abs(currentTimeInMinutes - personalCheckoutTime) < 1f; // 1분 오차 허용
    }

    private void DetermineInitialBehavior()
    {
        Debug.Log($"AI {gameObject.name}: 초기 행동 결정 시작");
        DetermineBehaviorByTime();
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
        
        Debug.Log($"AI {gameObject.name}: 시간 기반 행동 결정 - {timeSystem.CurrentHour}시 {timeSystem.CurrentMinute}분");

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
            }
            else
            {
                FallbackBehavior();
            }
        }
        else if (hour >= 9 && hour < 11)
        {
            // 9:00 ~ 11:00 - 개인별 체크아웃 시간 적용
            if (currentRoomIndex != -1)
            {
                // 개인별 체크아웃 시간이 되었는지 확인
                if (IsPersonalCheckoutTime())
                {
                    TransitionToState(AIState.ReportingRoomQueue);
                }
                else
                {
                    // 아직 체크아웃 시간이 아니므로 방에서 대기
                    TransitionToState(AIState.RoomWandering);
                }
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
                }
                else if (randomValue < 0.8f)
                {
                    TransitionToState(AIState.Wandering);
                }
                else
                {
                    TransitionToState(AIState.ReturningToSpawn);
                }
            }
            else
            {
                float randomValue = Random.value;
                if (randomValue < 0.5f)
                {
                    TransitionToState(AIState.UseWandering);
                }
                else
                {
                    TransitionToState(AIState.RoomWandering);
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
                }
                else
                {
                    TransitionToState(AIState.RoomWandering);
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
            // 방 사용 중이므로 디스폰하지 않음
            return;
        }

        // 17:00 강제 디스폰 시작

        // 모든 코루틴 강제 종료
        CleanupCoroutines();

        // 대기열에서 강제 제거
        if (isInQueue && counterManager != null)
        {
            counterManager.LeaveQueue(this);
            isInQueue = false;
            isWaitingForService = false;
            // 대기열에서 제거
            
            // 대기열 강제 정리는 마지막에 한 번만 호출 (중복 호출 방지)
            counterManager.ForceCleanupQueue();
        }

        // 강제 디스폰
        TransitionToState(AIState.ReturningToSpawn);
        agent.SetDestination(spawnPoint.position);
        // 강제 디스폰 실행
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
            // 11시 체크아웃 완료 후 디스폰
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
                // 0시 방 내부 배회로 전환
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
                    // 방 외부 배회로 전환
                    TransitionToState(AIState.UseWandering);
                }
            }
            else
            {
                if (currentState != AIState.RoomWandering)
                {
                    // 방 내부 배회로 전환
                    TransitionToState(AIState.RoomWandering);
                }
            }
        }
    }

    private void FallbackBehavior()
    {
        Debug.Log($"AI {gameObject.name}: FallbackBehavior 시작");
        
        // 카운터 재검색 시도
        if (counterPosition == null || counterManager == null)
        {
            Debug.Log($"AI {gameObject.name}: 카운터 재검색 시도");
            FindNearestCounter();
        }
        
        if (counterPosition == null || counterManager == null)
        {
            // 카운터가 없으면 배회 위주 행동
            Debug.Log($"AI {gameObject.name}: 카운터 없음 - 배회/복귀 선택");
            float randomValue = Random.value;
            if (randomValue < 0.7f)
            {
                TransitionToState(AIState.Wandering);
            }
            else
            {
                TransitionToState(AIState.ReturningToSpawn);
            }
        }
        else
        {
            // 카운터가 있으면 체크인 시도
            Debug.Log($"AI {gameObject.name}: 카운터 있음 - 대기열/배회 선택");
            float randomValue = Random.value;
            if (randomValue < 0.6f)
            {
                TransitionToState(AIState.MovingToQueue);
            }
            else
            {
                TransitionToState(AIState.Wandering);
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
                    // 11시 디스폰 예정이므로 행동 재결정 생략
                    lastBehaviorUpdateHour = hour;
                }
                // 중요한 상태가 아닌 경우에만 행동 재결정
                else if (!IsInCriticalState())
                {
                    // 행동 재결정 시작
                    DetermineBehaviorByTime();
                    lastBehaviorUpdateHour = hour;
                }
                // 방 사용 중인 AI도 매시간 내부/외부 배회 재결정
                else if (IsInRoomRelatedState())
                {
                    // 방 배회 재결정
                    RedetermineRoomBehavior();
                    lastBehaviorUpdateHour = hour;
                }
                else
                {
                    // 중요한 상태로 행동 재결정 생략
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
                if (currentRoomIndex != -1 && roomManager != null)
                {
                    Bounds roomBounds = roomManager.GetRoomBounds(currentRoomIndex);
                    if (!agent.pathPending && agent.remainingDistance < arrivalDistance && roomBounds.Contains(transform.position))
                    {
                        // 룸 도착
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
                    // 스폰 지점 도착, 디스폰
                    ReturnToPool();
                }
                break;
        }
    }
    #endregion

    #region 대기열 동작
    private IEnumerator QueueBehavior()
    {
        Debug.Log($"[AIAgent] {gameObject.name}: QueueBehavior 시작");
        
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

        // 대기열 진입 시도
        if (!counterManager.TryJoinQueue(this))
        {
            Debug.Log($"[AIAgent] {gameObject.name}: 대기열 진입 실패");
            
            // ReportingRoomQueue 상태인 경우 재시도
            if (currentState == AIState.ReportingRoomQueue)
            {
                Debug.Log($"[AIAgent] {gameObject.name}: ReportingRoomQueue 재시도 대기");
                yield return new WaitForSeconds(Random.Range(2f, 5f));
                StartCoroutine(QueueBehavior());
                yield break;
            }
            
            if (currentRoomIndex == -1)
            {
                // 방 없음, 대안 행동 선택
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
                // 방 있음, 재시도
                Debug.Log($"[AIAgent] {gameObject.name}: 방 있음, 재시도 대기");
                yield return new WaitForSeconds(Random.Range(1f, 3f));
                StartCoroutine(QueueBehavior());
            }
            yield break;
        }

        // 대기열 진입 성공
        isInQueue = true;
        TransitionToState(currentState == AIState.ReportingRoomQueue ? AIState.ReportingRoomQueue : AIState.WaitingInQueue);
        Debug.Log($"[AIAgent] {gameObject.name}: 대기열 진입 성공, 상태: {currentState}");

        // 대기열 타임아웃 설정 (최대 30초)
        float queueTimeout = 30f;
        float queueTimer = 0f;
        float lastProgressTime = Time.time;
        
        while (isInQueue && queueTimer < queueTimeout)
        {
            // 17시 체크 - 대기열에서도 즉시 디스폰
            if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
            {
                Debug.Log($"[AIAgent] {gameObject.name}: 17시 감지, 대기열에서 강제 디스폰");
                Handle17OClockForcedDespawn();
                yield break;
            }

            // 카운터 도착 판정 (단순화)
            float distanceToTarget = Vector3.Distance(transform.position, targetQueuePosition);
            bool arrivedAtQueue = distanceToTarget < arrivalDistance;
            
            if (arrivedAtQueue)
            {
                Debug.Log($"[AIAgent] {gameObject.name}: 대기열 위치 도착 (거리: {distanceToTarget:F2})");
                
                if (counterManager.CanReceiveService(this))
                {
                    Debug.Log($"[AIAgent] {gameObject.name}: 서비스 시작 가능");
                    // 서비스 시작
                    counterManager.StartService(this);
                    isWaitingForService = true;
                    lastProgressTime = Time.time; // 진행 시간 업데이트

                    // 서비스 대기 타임아웃 설정 (최대 10초)
                    float serviceWaitTimeout = 10f;
                    float serviceWaitTimer = 0f;

                    while (isWaitingForService && serviceWaitTimer < serviceWaitTimeout)
                    {
                        // 서비스 대기 중에도 17시 체크
                        if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
                        {
                            Debug.Log($"[AIAgent] {gameObject.name}: 서비스 중 17시 감지, 강제 디스폰");
                            Handle17OClockForcedDespawn();
                            yield break;
                        }
                        yield return new WaitForSeconds(0.1f);
                        serviceWaitTimer += 0.1f;
                    }

                    // 서비스 대기 타임아웃 처리
                    if (serviceWaitTimer >= serviceWaitTimeout && isWaitingForService)
                    {
                        Debug.LogWarning($"[AIAgent] {gameObject.name}: 서비스 대기 타임아웃 ({serviceWaitTimeout}초), 대기열에서 나감");
                        isWaitingForService = false;
                        
                        // 대기열에서 강제로 나가기
                        if (counterManager != null)
                        {
                            counterManager.LeaveQueue(this);
                        }
                        isInQueue = false;
                        
                        // 배회 상태로 전환
                        TransitionToState(AIState.Wandering);
                        wanderingCoroutine = StartCoroutine(WanderingBehavior());
                        yield break;
                    }

                    Debug.Log($"[AIAgent] {gameObject.name}: 서비스 완료, 상태: {currentState}");

                    if (currentState == AIState.ReportingRoomQueue)
                    {
                        // ReportingRoomQueue 서비스 완료
                        StartCoroutine(ReportRoomVacancy());
                    }
                    else if (currentRoomIndex >= 0)
                    {
                        // RoomManager를 통해 방 해제
                        if (roomManager != null)
                        {
                            roomManager.ReleaseRoom(gameObject.name);
                        }
                        currentRoomIndex = -1;
                        TransitionToState(AIState.ReturningToSpawn);
                        agent.SetDestination(spawnPoint.position);
                    }
                    else
                    {
                        // 방 배정 시도
                        if (TryAssignRoom())
                        {
                            Debug.Log($"[AIAgent] {gameObject.name}: 방 배정 성공, 방 {currentRoomIndex}번으로 이동");
                            TransitionToState(AIState.MovingToRoom);
                            if (roomManager != null)
                            {
                                Transform roomTransform = roomManager.GetRoomTransform(currentRoomIndex);
                                if (roomTransform != null)
                                {
                                    agent.SetDestination(roomTransform.position);
                                }
                            }
                        }
                        else
                        {
                            Debug.Log($"[AIAgent] {gameObject.name}: 방 배정 실패, 대안 행동 선택");
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
                else
                {
                    // 서비스 받을 수 없음 - 올바른 위치 확인 및 재배치
                    int queuePosition = counterManager.GetQueuePosition(this);
                    Debug.Log($"[AIAgent] {gameObject.name}: 대기열에서 대기 중 (위치: {queuePosition})");
                    
                    // 내 순서에 맞는 올바른 위치 확인
                    if (queuePosition > 0)
                    {
                        Vector3 correctPosition = counterManager.GetCorrectQueuePosition(queuePosition);
                        float distanceToCorrectPos = Vector3.Distance(transform.position, correctPosition);
                        
                        // 올바른 위치에서 너무 멀리 떨어져 있으면 재배치
                        if (distanceToCorrectPos > arrivalDistance * 1.5f)
                        {
                            Debug.Log($"[AIAgent] {gameObject.name}: 올바른 대기열 위치로 재배치 (현재 거리: {distanceToCorrectPos:F2})");
                            agent.SetDestination(correctPosition);
                            targetQueuePosition = correctPosition;
                            lastProgressTime = Time.time; // 진행 시간 리셋
                        }
                    }
                }
            }
            else
            {
                // 아직 대기열 위치에 도착하지 못함
                Debug.Log($"[AIAgent] {gameObject.name}: 대기열 위치로 이동 중 (거리: {distanceToTarget:F2}, remaining: {agent.remainingDistance:F2})");
                
                // 경로가 막혔거나 너무 오래 걸리는 경우 재설정
                if (Time.time - lastProgressTime > 10f)
                {
                    Debug.LogWarning($"[AIAgent] {gameObject.name}: 대기열 이동 시간 초과, 경로 재설정");
                    agent.SetDestination(targetQueuePosition);
                    lastProgressTime = Time.time;
                }
            }
            
            yield return new WaitForSeconds(0.1f);
            queueTimer += 0.1f;
        }
        
        // 타임아웃 발생 시 대기열에서 나가고 배회
        if (queueTimer >= queueTimeout)
        {
            Debug.LogWarning($"[AIAgent] {gameObject.name}: 대기열 타임아웃 ({queueTimeout}초), 배회로 전환");
            
            // 대기열에서 강제로 나가기
            if (counterManager != null)
            {
                counterManager.LeaveQueue(this);
            }
            isInQueue = false;
            isWaitingForService = false;
            
            // 배회 상태로 전환
            TransitionToState(AIState.Wandering);
            wanderingCoroutine = StartCoroutine(WanderingBehavior());
        }
    }

    private bool TryAssignRoom()
    {
        if (roomManager == null) return false;
        
        int assignedRoomIndex = roomManager.TryAssignRoom(gameObject.name);
        if (assignedRoomIndex >= 0)
        {
            currentRoomIndex = assignedRoomIndex;
            return true;
        }
        
        return false;
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

        switch (newState)
        {
            case AIState.Wandering:
                wanderingCoroutine = StartCoroutine(WanderingBehavior());
                break;
            case AIState.MovingToQueue:
            case AIState.ReportingRoomQueue:
                queueBehaviorCoroutine = StartCoroutine(QueueBehavior());
                break;
            case AIState.MovingToRoom:
                if (currentRoomIndex != -1 && roomManager != null)
                {
                    Transform roomTransform = roomManager.GetRoomTransform(currentRoomIndex);
                    if (roomTransform != null)
                    {
                        agent.SetDestination(roomTransform.position);
                    }
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

        if (currentRoomIndex < 0 || roomManager == null)
        {
            Debug.LogError($"AI {gameObject.name}: 잘못된 룸 인덱스 {currentRoomIndex} 또는 RoomManager 없음.");
            StartCoroutine(ReportRoomVacancy());
            yield break;
        }

        GameObject roomGameObject = roomManager.GetRoomGameObject(currentRoomIndex);
        if (roomGameObject != null)
        {
            var room = roomGameObject.GetComponent<RoomContents>();
            if (room != null)
            {
                roomManager.ReportRoomUsage(gameObject.name, room);
            }
        }

        // TransitionToState(AIState.UsingRoom);
        TransitionToState(AIState.UseWandering);

        // 룸 사용 시작
        while (elapsedTime < roomUseTime && agent.isOnNavMesh)
        {
            yield return new WaitForSeconds(Random.Range(2f, 5f));
            elapsedTime += Random.Range(2f, 5f);
        }

        // 룸 사용 완료
        DetermineBehaviorByTime();
    }

    private IEnumerator ReportRoomVacancy()
    {
        TransitionToState(AIState.ReportingRoom);
        
        // RoomManager를 통해 방 해제
        if (roomManager != null && currentRoomIndex >= 0)
        {
            roomManager.ReleaseRoom(gameObject.name);
            int amount = roomManager.ProcessRoomPayment(gameObject.name);
            currentRoomIndex = -1;
        }
        else
        {
            Debug.LogError($"[AIAgent] AI {gameObject.name}: RoomManager를 찾을 수 없거나 잘못된 방 인덱스입니다!");
        }

        if (timeSystem.CurrentHour >= 9 && timeSystem.CurrentHour < 11)
        {
            // 9-11시 체크아웃 후에는 배회하다가 11시에 디스폰 예정
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
        
        // 넓은 범위 배회 시작

        while (currentState == AIState.Wandering && elapsedTime < wanderingTime)
        {
            // 17시 체크 - 배회 중에도 즉시 디스폰
            if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
            {
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
                // 넓은 범위 배회 성공
            }
            else
            {
                // 방을 피하지 못한 경우 기본 방식으로 시도
                WanderOnGround();
                foundValidPosition = true;
                // 기본 배회 사용
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
                        // 이동 타임아웃, 새로운 목적지 설정
                        break;
                    }
                    
                    // 17시 체크
                    if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
                    {
                        // 17시 감지, 즉시 강제 디스폰
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
                        // 17시 감지, 즉시 강제 디스폰
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

        // 배회 완료, 다음 행동 결정
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// 9-11시 체크아웃 후 배회하다가 11시에 디스폰하는 특별한 배회
    /// </summary>
    private IEnumerator WanderingBehaviorWithDespawn()
    {
        // 9-11시 체크아웃 후 배회 시작
        
        while (currentState == AIState.Wandering)
        {
            // 11시가 되면 즉시 디스폰
            if (timeSystem != null && timeSystem.CurrentHour >= 11)
            {
                // 11시 도달, 체크아웃 완료 AI 디스폰
                isScheduledForDespawn = false; // 플래그 리셋
                TransitionToState(AIState.ReturningToSpawn);
                agent.SetDestination(spawnPoint.position);
                yield break;
            }

            // 17시 체크도 유지
            if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
            {
                // 17시 감지, 즉시 강제 디스폰
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
                    // 11시 도달, 체크아웃 완료 AI 디스폰
                    isScheduledForDespawn = false; // 플래그 리셋
                    TransitionToState(AIState.ReturningToSpawn);
                    agent.SetDestination(spawnPoint.position);
                    yield break;
                }
                
                // 17시 체크
                if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
                {
                    // 17시 감지, 즉시 강제 디스폰
                    isScheduledForDespawn = false; // 플래그 리셋
                    Handle17OClockForcedDespawn();
                    yield break;
                }
            }
        }
    }

    private IEnumerator UseWanderingBehavior()
    {
        if (currentRoomIndex < 0 || roomManager == null)
        {
            Debug.LogError($"AI {gameObject.name}: 잘못된 룸 인덱스 {currentRoomIndex} 또는 RoomManager 없음.");
            DetermineBehaviorByTime();
            yield break;
        }

        // 방 외부 배회 시작

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
                // 방 외부 배회
                
                // 목적지까지 이동 대기
                yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance < arrivalDistance);
            }
            else
            {
                // 방 외부 배회 위치를 찾지 못함, 기본 배회 사용
                WanderOnGround();
            }

            float waitTime = Random.Range(4f, 10f);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private IEnumerator RoomWanderingBehavior()
    {
        if (currentRoomIndex < 0 || roomManager == null)
        {
            Debug.LogError($"AI {gameObject.name}: 잘못된 룸 인덱스 {currentRoomIndex} 또는 RoomManager 없음.");
            DetermineBehaviorByTime();
            yield break;
        }

        float wanderingTime = Random.Range(15f, 30f);
        float elapsedTime = 0f;
        
        // 방 내부 배회 시작

        while (currentState == AIState.RoomWandering && elapsedTime < wanderingTime && agent.isOnNavMesh)
        {
            // 17시 체크는 하지 않음 - 방 사용 중인 AI는 계속 작동
            
            // 방 내부에서만 배회
            if (TryGetRoomWanderingPosition(currentRoomIndex, out Vector3 roomPosition))
            {
                agent.SetDestination(roomPosition);
                // 방 내부 배회
                
                // 목적지까지 이동 대기
                yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance < arrivalDistance);
            }
            else
            {
                // 방 내부 위치를 찾지 못한 경우 기존 방식 사용
                Transform roomTransform = roomManager.GetRoomTransform(currentRoomIndex);
                if (roomTransform != null)
                {
                    Vector3 roomCenter = roomTransform.position;
                    float roomSize = 2f; // 기본 크기 사용
                    if (TryGetValidPosition(roomCenter, roomSize, NavMesh.AllAreas, out Vector3 fallbackPos))
                    {
                        agent.SetDestination(fallbackPos);
                    }
                }
            }

            float waitTime = Random.Range(3f, 6f);
            yield return new WaitForSeconds(waitTime);
            elapsedTime += waitTime;
        }

        // 방 내부 배회 완룼
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
            // 넓은 범위 배회
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
                // 방과의 거리 체크 (RoomManager를 통해)
                bool tooCloseToRoom = false;
                if (roomManager != null)
                {
                    var availableRooms = roomManager.GetAvailableRooms();
                    foreach (var room in availableRooms)
                    {
                        if (room != null && room.gameObject != null)
                        {
                            float distanceToRoom = Vector3.Distance(hit.position, room.transform.position);
                            // 기본 거리 3유닛 이상 떨어져 있어야 함
                            if (distanceToRoom < 3f)
                            {
                                tooCloseToRoom = true;
                                break;
                            }
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
        
        if (roomIndex < 0 || roomManager == null)
            return false;
            
        Bounds roomBounds = roomManager.GetRoomBounds(roomIndex);
        if (roomBounds.size == Vector3.zero)
            return false;
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
        // 모든 코루틴을 안전하게 정리
        try
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
            if (useWanderingCoroutine != null)
            {
                StopCoroutine(useWanderingCoroutine);
                useWanderingCoroutine = null;
            }
            if (queueBehaviorCoroutine != null)
            {
                StopCoroutine(queueBehaviorCoroutine);
                queueBehaviorCoroutine = null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"AI {gameObject.name}: 코루틴 정리 중 오류 발생: {e.Message}");
        }
        
        // 추가 안전 조치: 모든 코루틴 강제 정지
        StopAllCoroutines();
    }

    private void CleanupResources()
    {
        try
        {
            // RoomManager를 통해 방 해제
            if (currentRoomIndex != -1 && roomManager != null)
            {
                roomManager.ReleaseRoom(gameObject.name);
                currentRoomIndex = -1;
            }

            // 상태 초기화
            isBeingServed = false;
            isInQueue = false;
            isWaitingForService = false;
            isScheduledForDespawn = false;
            personalCheckoutTime = -1f; // 개인별 체크아웃 시간 리셋

            // 대기열에서 제거
            if (counterManager != null)
            {
                counterManager.LeaveQueue(this);
            }
            
            // NavMeshAgent 경로 초기화
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"AI {gameObject.name}: 리소스 정리 중 오류 발생: {e.Message}");
        }
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
        isScheduledForDespawn = false;
        
        // 개인별 체크아웃 시간 재초기화
        InitializePersonalCheckoutTime();

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