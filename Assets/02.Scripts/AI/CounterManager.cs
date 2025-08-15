using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using JY;

namespace JY
{
public class CounterManager : MonoBehaviour
{
    [Header("Queue Settings")]
    public float queueSpacing = 2f;           // AI 간격
    public float counterServiceDistance = 2f;  // 카운터와 서비스 받는 위치 사이의 거리
    public int maxQueueLength = 10;           // 최대 대기열 길이
    public float serviceTime = 3f;            // 서비스 처리 시간 (3초로 단축)
    
    [Header("Checkout System")]
    [Tooltip("체크아웃 재시도 최대 횟수")]
    public int maxRetryAttempts = 5;
    
    [Tooltip("재시도 간격 (초)")]
    public float retryInterval = 2f;
    
    // 재시도 대기 중인 AI 목록
    private Dictionary<AIAgent, int> retryQueue = new Dictionary<AIAgent, int>();
    private Dictionary<AIAgent, float> lastRetryTime = new Dictionary<AIAgent, float>();

    // 통합 대기열 - 방 배정과 방 사용완료 보고를 모두 처리
    private Queue<AIAgent> waitingQueue = new Queue<AIAgent>();
    private AIAgent currentServingAgent = null;
    private Vector3 counterFront;
    private Transform counterTransform;
    private bool isProcessingService = false;

    void Start()
    {
        counterTransform = transform;
        // 카운터 정면 위치 계산 (카운터의 forward 방향으로 2유닛)
        counterFront = counterTransform.position + counterTransform.forward * counterServiceDistance;
    }
    
    void Update()
    {
        // 재시도 큐 처리
        ProcessRetryQueue();
    }

    // 대기열에 합류 요청 (방 배정/방 사용완료 보고 모두 동일 대기열 사용)
    public bool TryJoinQueue(AIAgent agent)
    {
        // 대기열 진입 요청
        
        if (waitingQueue.Count >= maxQueueLength)
        {
            // 대기열이 가득 참 - 재시도 시스템 활용
            return TryAddToRetryQueue(agent);
        }

        waitingQueue.Enqueue(agent);
        UpdateQueuePositions();
        
        // 재시도 큐에서 제거 (성공적으로 대기열에 진입했으므로)
        if (retryQueue.ContainsKey(agent))
        {
            retryQueue.Remove(agent);
            lastRetryTime.Remove(agent);
        }
        
        // AI 대기열 합류
        return true;
    }
    
    /// <summary>
    /// 재시도 큐에 AI를 추가합니다
    /// </summary>
    private bool TryAddToRetryQueue(AIAgent agent)
    {
        if (!retryQueue.ContainsKey(agent))
        {
            retryQueue[agent] = 0;
            lastRetryTime[agent] = Time.time;
        }
        
        if (retryQueue[agent] < maxRetryAttempts)
        {
            retryQueue[agent]++;
            lastRetryTime[agent] = Time.time;
            return false; // 대기열 진입 실패, 하지만 재시도 예정
        }
        else
        {
            // 최대 재시도 횟수 초과
            retryQueue.Remove(agent);
            lastRetryTime.Remove(agent);
            return false;
        }
    }
    
    /// <summary>
    /// 재시도 큐를 처리합니다
    /// </summary>
    private void ProcessRetryQueue()
    {
        var agentsToRetry = new List<AIAgent>();
        
        foreach (var kvp in retryQueue.ToList())
        {
            AIAgent agent = kvp.Key;
            if (agent == null) continue;
            
            if (Time.time - lastRetryTime[agent] >= retryInterval)
            {
                agentsToRetry.Add(agent);
            }
        }
        
        foreach (var agent in agentsToRetry)
        {
            if (waitingQueue.Count < maxQueueLength)
            {
                // 대기열에 자리가 생겼으므로 재시도
                if (TryJoinQueue(agent))
                {
                    // 재시도 성공 - AIAgent에게 알림
                    StartCoroutine(NotifyRetrySuccess(agent));
                }
            }
        }
    }
    
    /// <summary>
    /// 재시도 성공을 AI에게 알립니다
    /// </summary>
    private IEnumerator NotifyRetrySuccess(AIAgent agent)
    {
        yield return new WaitForEndOfFrame();
        // AI에게 재시도 성공 알림 (필요시 AIAgent에 메서드 추가)
        // agent.OnRetrySuccess();
    }
    
    /// <summary>
    /// AI가 재시도 대기 중인지 확인합니다
    /// </summary>
    public bool IsInRetryQueue(AIAgent agent)
    {
        return retryQueue.ContainsKey(agent);
    }
    
    /// <summary>
    /// AI의 재시도 횟수를 반환합니다
    /// </summary>
    public int GetRetryCount(AIAgent agent)
    {
        return retryQueue.ContainsKey(agent) ? retryQueue[agent] : 0;
    }
    
    /// <summary>
    /// AI의 대기열 위치를 반환합니다 (1부터 시작)
    /// </summary>
    public int GetQueuePosition(AIAgent agent)
    {
        if (waitingQueue.Count == 0) return -1;
        
        int position = 1;
        foreach (var queuedAgent in waitingQueue)
        {
            if (queuedAgent == agent)
            {
                return position;
            }
            position++;
        }
        
        return -1; // 대기열에 없음
    }
    
    /// <summary>
    /// 현재 대기열 길이를 반환합니다
    /// </summary>
    public int GetCurrentQueueLength()
    {
        return waitingQueue.Count;
    }
    
    /// <summary>
    /// 특정 대기열 위치에 해당하는 좌표를 반환합니다
    /// </summary>
    public Vector3 GetCorrectQueuePosition(int queuePosition)
    {
        if (queuePosition <= 0) return counterFront;
        
        // queuePosition은 1부터 시작하므로 -1
        int actualIndex = queuePosition - 1;
        float distance = counterServiceDistance + (actualIndex * queueSpacing);
        return transform.position + transform.forward * distance;
    }

    // AI가 대기열에서 나가기 요청
    public void LeaveQueue(AIAgent agent)
    {
        // 대기열 나가기 요청
        
        if (currentServingAgent == agent)
        {
            // 서비스 중인 AI 서비스 중단
            currentServingAgent = null;
            isProcessingService = false;
        }

        RemoveFromQueue(waitingQueue, agent);
        
        // 재시도 큐에서도 제거
        if (retryQueue.ContainsKey(agent))
        {
            retryQueue.Remove(agent);
            lastRetryTime.Remove(agent);
        }
        
        UpdateQueuePositions();
        // AI 대기열에서 나감
    }

    private void RemoveFromQueue(Queue<AIAgent> queue, AIAgent agent)
    {
        int originalCount = queue.Count;
        var tempQueue = new Queue<AIAgent>();
        bool removed = false;
        
        while (queue.Count > 0)
        {
            var queuedAgent = queue.Dequeue();
            if (queuedAgent != agent)
            {
                tempQueue.Enqueue(queuedAgent);
            }
            else
            {
                removed = true;
                // 대기열에서 AI 제거됨
            }
        }
        while (tempQueue.Count > 0)
        {
            queue.Enqueue(tempQueue.Dequeue());
        }
        
        if (!removed)
        {
            // AI가 대기열에 없어서 제거할 수 없음
        }
    }

    // 대기열 위치 업데이트
    private void UpdateQueuePositions()
    {
        int queueIndex = 0;  // 서비스 중인 AI 제외한 순수 대기열 인덱스
        foreach (var agent in waitingQueue)
        {
            if (agent != null)
            {
                if (agent == currentServingAgent)
                {
                    agent.SetQueueDestination(counterFront);
                }
                else
                {
                    float distance = counterServiceDistance + (queueIndex * queueSpacing);
                    Vector3 queuePosition = transform.position + counterTransform.forward * distance;
                    agent.SetQueueDestination(queuePosition);
                    queueIndex++;  // 대기 중인 AI만 카운트
                }
            }
        }
    }

    // 현재 서비스 받을 수 있는지 확인
    public bool CanReceiveService(AIAgent agent)
    {
        bool canReceive = waitingQueue.Count > 0 && waitingQueue.Peek() == agent && !isProcessingService;
        // 서비스 가능 확인
        return canReceive;
    }

    // 서비스 시작
    public void StartService(AIAgent agent)
    {
        // StartService 호출
        
        if (CanReceiveService(agent))
        {
            currentServingAgent = agent;
            isProcessingService = true;
            agent.SetQueueDestination(counterFront);
            UpdateQueuePositions();
            StartCoroutine(ServiceCoroutine(agent));
            // AI 서비스 시작
        }
        else
        {
            // AI는 서비스를 받을 수 없음
        }
    }

    // 서비스 처리 코루틴
    private IEnumerator ServiceCoroutine(AIAgent agent)
    {
        // 서비스 시작
        yield return new WaitForSeconds(serviceTime);
        
        // 서비스 시간 완료
        
        if (currentServingAgent == agent)
        {
            // 대기열에서 제거
            if (waitingQueue.Count > 0 && waitingQueue.Peek() == agent)
            {
                waitingQueue.Dequeue();
                // 서비스 완료로 AI 대기열에서 제거
            }
            else
            {
                // 서비스 완료했지만 AI가 대기열 첫 번째가 아님
            }

            currentServingAgent = null;
            isProcessingService = false;
            UpdateQueuePositions();
            agent.OnServiceComplete();
            // AI 서비스 완료
        }
        else
        {
            // 서비스 완료 시 currentServingAgent가 다른 AI
        }
    }

    // 대기열 위치 얻기
    public Vector3 GetCounterServicePosition()
    {
        return counterFront;
    }

    /// <summary>
    /// 대기열을 완전히 정리합니다 (17시 강제 디스폰 등에 사용)
    /// </summary>
    public void ForceCleanupQueue()
    {
        // 강제 대기열 정리 시작
        
        int originalCount = waitingQueue.Count;
        var cleanQueue = new Queue<AIAgent>();
        
        while (waitingQueue.Count > 0)
        {
            var agent = waitingQueue.Dequeue();
            if (agent != null && agent.gameObject != null)
            {
                // AI가 실제로 유효한지 확인
                try
                {
                    // GameObject가 파괴되었는지 확인
                    if (agent.gameObject.activeInHierarchy)
                    {
                        cleanQueue.Enqueue(agent);
                    }
                    else
                    {
                        // 비활성화된 AI 대기열에서 제거
                    }
                }
                catch
                {
                    // 파괴된 AI 참조 대기열에서 제거
                }
            }
            else
            {
                // null AI 참조 대기열에서 제거
            }
        }
        
        waitingQueue = cleanQueue;
        
        // 서비스 중인 AI도 정리
        if (currentServingAgent != null)
        {
            try
            {
                if (currentServingAgent.gameObject == null || !currentServingAgent.gameObject.activeInHierarchy)
                {
                    Debug.Log($"[CounterManager] 서비스 중인 AI가 비활성화됨, 서비스 중단");
                    currentServingAgent = null;
                    isProcessingService = false;
                }
            }
            catch
            {
                Debug.Log($"[CounterManager] 서비스 중인 AI 참조가 파괴됨, 서비스 중단");
                currentServingAgent = null;
                isProcessingService = false;
            }
        }
        
        UpdateQueuePositions();
        Debug.Log($"[CounterManager] 강제 대기열 정리 완료 - 정리 후: {waitingQueue.Count}명 (제거됨: {originalCount - waitingQueue.Count}명)");
    }

    void OnDrawGizmos()
    {
        // 에디터에서도 대기열 위치를 시각화
        if (!Application.isPlaying)
        {
            counterFront = transform.position + transform.forward * counterServiceDistance;
        }

        // 서비스 위치 표시 (노란색)
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(counterFront, 0.3f);

        // 대기열 위치 표시 (파란색)
        Gizmos.color = Color.blue;
        for (int i = 0; i < maxQueueLength; i++)
        {
            float distance = counterServiceDistance + (i * queueSpacing);
            Vector3 queuePos = transform.position + transform.forward * distance;
            Gizmos.DrawSphere(queuePos, 0.2f);
            
            // 대기열 라인 표시
            if (i < maxQueueLength - 1)
            {
                float nextDistance = counterServiceDistance + ((i + 1) * queueSpacing);
                Vector3 nextPos = transform.position + transform.forward * nextDistance;
                Gizmos.DrawLine(queuePos, nextPos);
            }
            }
        }
    }
}