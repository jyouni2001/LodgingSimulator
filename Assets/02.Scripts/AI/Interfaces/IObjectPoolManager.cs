using UnityEngine;

namespace JY.AI.Interfaces
{
    /// <summary>
    /// 오브젝트 풀링을 추상화하는 인터페이스
    /// AI 시스템이 AISpawner에 직접 의존하지 않도록 함
    /// </summary>
    public interface IObjectPoolManager
    {
        /// <summary>
        /// 오브젝트를 풀에서 가져오기
        /// </summary>
        GameObject GetFromPool();
        
        /// <summary>
        /// 오브젝트를 풀로 반환
        /// </summary>
        void ReturnToPool(GameObject obj);
        
        /// <summary>
        /// 활성화된 오브젝트 수
        /// </summary>
        int ActiveObjectCount { get; }
        
        /// <summary>
        /// 풀 크기
        /// </summary>
        int PoolSize { get; }
        
        /// <summary>
        /// 스폰 위치 반환
        /// </summary>
        Vector3 GetSpawnPosition();
    }

    /// <summary>
    /// AISpawner를 IObjectPoolManager로 감싸는 어댑터
    /// </summary>
    public class AISpawnerAdapter : IObjectPoolManager
    {
        private AISpawner spawner;

        public AISpawnerAdapter(AISpawner aiSpawner)
        {
            spawner = aiSpawner;
        }

        public GameObject GetFromPool()
        {
            // AISpawner는 수동 스폰을 지원하지 않으므로 null 반환
            // 실제로는 AISpawner가 자동으로 관리
            AIDebugLogger.LogWarning("AISpawnerAdapter", "AISpawner는 수동 풀링을 지원하지 않습니다");
            return null;
        }

        public void ReturnToPool(GameObject obj)
        {
            if (spawner == null || obj == null) return;
            spawner.ReturnToPool(obj);
        }

        public int ActiveObjectCount => spawner?.GetActiveAICount() ?? 0;

        public int PoolSize => spawner?.poolSize ?? 0;

        public Vector3 GetSpawnPosition()
        {
            // AISpawner의 위치를 스폰 포인트로 사용
            if (spawner != null)
            {
                return spawner.transform.position;
            }
            
            // 대안으로 스폰 태그를 가진 오브젝트 찾기
            var spawnPoint = GameObject.FindGameObjectWithTag("Spawn");
            return spawnPoint?.transform.position ?? Vector3.zero;
        }
    }
}