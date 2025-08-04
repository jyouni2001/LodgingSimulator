using UnityEngine;

namespace JY.AI.Interfaces
{
    /// <summary>
    /// 대기열 관리를 추상화하는 인터페이스
    /// AI 시스템이 CounterManager에 직접 의존하지 않도록 함
    /// </summary>
    public interface IQueueManager
    {
        /// <summary>
        /// 대기열 합류 시도
        /// </summary>
        bool TryJoinQueue(object agent);
        
        /// <summary>
        /// 대기열에서 나가기
        /// </summary>
        void LeaveQueue(object agent);
        
        /// <summary>
        /// 서비스 받을 수 있는지 확인
        /// </summary>
        bool CanReceiveService(object agent);
        
        /// <summary>
        /// 서비스 시작
        /// </summary>
        void StartService(object agent);
        
        /// <summary>
        /// 대기열 위치 반환
        /// </summary>
        Vector3 GetQueuePosition(object agent);
        
        /// <summary>
        /// 서비스 위치 반환
        /// </summary>
        Vector3 GetServicePosition();
    }

    /// <summary>
    /// CounterManager를 IQueueManager로 감싸는 어댑터
    /// </summary>
    public class CounterManagerAdapter : IQueueManager
    {
        private CounterManager counterManager;

        public CounterManagerAdapter(CounterManager manager)
        {
            counterManager = manager;
        }

        public bool TryJoinQueue(object agent)
        {
            if (counterManager == null || !(agent is AIAgent aiAgent))
                return false;
            return counterManager.TryJoinQueue(aiAgent);
        }

        public void LeaveQueue(object agent)
        {
            if (counterManager == null || !(agent is AIAgent aiAgent))
                return;
            counterManager.LeaveQueue(aiAgent);
        }

        public bool CanReceiveService(object agent)
        {
            if (counterManager == null || !(agent is AIAgent aiAgent))
                return false;
            return counterManager.CanReceiveService(aiAgent);
        }

        public void StartService(object agent)
        {
            if (counterManager == null || !(agent is AIAgent aiAgent))
                return;
            counterManager.StartService(aiAgent);
        }

        public Vector3 GetQueuePosition(object agent)
        {
            // CounterManager의 대기열 위치 계산 로직 활용
            // 기본적으로 카운터 앞쪽으로 반환
            return counterManager?.transform.position + Vector3.forward * 2f ?? Vector3.zero;
        }

        public Vector3 GetServicePosition()
        {
            return counterManager?.transform.position + counterManager.transform.forward * 2f ?? Vector3.zero;
        }
    }
}