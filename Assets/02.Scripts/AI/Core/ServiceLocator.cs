using System;
using System.Collections.Generic;
using UnityEngine;

namespace JY.AI
{
    /// <summary>
    /// 서비스 로케이터 패턴을 통한 의존성 주입 최적화
    /// FindObjectOfType을 대체하여 성능 개선
    /// </summary>
    public class ServiceLocator : MonoBehaviour
    {
        private static ServiceLocator _instance;
        public static ServiceLocator Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ServiceLocator>();
                    if (_instance == null)
                    {
                        var go = new GameObject("ServiceLocator");
                        _instance = go.AddComponent<ServiceLocator>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        // 서비스 저장소
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();
        private readonly Dictionary<string, Component> cachedComponents = new Dictionary<string, Component>();

        #region 서비스 등록 및 조회

        /// <summary>
        /// 서비스 등록
        /// </summary>
        public static void RegisterService<T>(T service) where T : class
        {
            if (Instance.services.ContainsKey(typeof(T)))
            {
                Instance.services[typeof(T)] = service;
                Debug.Log($"[ServiceLocator] 서비스 업데이트: {typeof(T).Name}");
            }
            else
            {
                Instance.services.Add(typeof(T), service);
                Debug.Log($"[ServiceLocator] 서비스 등록: {typeof(T).Name}");
            }
        }

        /// <summary>
        /// 서비스 조회 (캐시된 서비스 우선)
        /// </summary>
        public static T GetService<T>() where T : class
        {
            var type = typeof(T);
            
            // 캐시된 서비스가 있으면 반환
            if (Instance.services.TryGetValue(type, out var service))
            {
                if (service is T typedService)
                {
                    return typedService;
                }
            }

            // 캐시에 없으면 FindObjectOfType으로 찾고 캐시에 저장
            var foundService = FindObjectOfType<T>();
            if (foundService != null)
            {
                RegisterService(foundService);
                return foundService;
            }

            Debug.LogWarning($"[ServiceLocator] 서비스를 찾을 수 없습니다: {typeof(T).Name}");
            return null;
        }

        /// <summary>
        /// 컴포넌트 캐시 조회 (성능 최적화)
        /// </summary>
        public static T GetCachedComponent<T>(string tag = null) where T : Component
        {
            var key = tag ?? typeof(T).Name;
            
            if (Instance.cachedComponents.TryGetValue(key, out var cached))
            {
                if (cached != null && cached is T typedComponent)
                {
                    return typedComponent;
                }
                // 캐시된 것이 파괴되었으면 제거
                Instance.cachedComponents.Remove(key);
            }

            // 캐시에 없으면 찾아서 저장
            T found = string.IsNullOrEmpty(tag) 
                ? FindObjectOfType<T>() 
                : GameObject.FindWithTag(tag)?.GetComponent<T>();
                
            if (found != null)
            {
                Instance.cachedComponents[key] = found;
                Debug.Log($"[ServiceLocator] 컴포넌트 캐시됨: {typeof(T).Name}");
            }

            return found;
        }

        #endregion

        #region AI 특화 서비스 접근자

        /// <summary>
        /// AISpawner 빠른 접근
        /// </summary>
        public static AISpawner AISpawner => GetService<AISpawner>();

        /// <summary>
        /// CounterManager 빠른 접근  
        /// </summary>
        public static CounterManager CounterManager => GetService<CounterManager>();

        /// <summary>
        /// PaymentSystem 빠른 접근
        /// </summary>
        public static PaymentSystem PaymentSystem => GetService<PaymentSystem>();

        /// <summary>
        /// ReputationSystem 빠른 접근
        /// </summary>
        public static ReputationSystem ReputationSystem => GetService<ReputationSystem>();

        /// <summary>
        /// TimeSystem 빠른 접근 (다른 폴더에 있을 수 있음)
        /// </summary>
        public static Component TimeSystem
        {
            get
            {
                // TimeSystem은 여러 이름일 수 있으므로 타입명으로 캐시
                return GetCachedComponent<Component>("TimeSystem") ?? 
                       GetCachedComponent<MonoBehaviour>("SunMoonController");
            }
        }

        #endregion

        #region 메모리 관리

        /// <summary>
        /// 서비스 등록 해제
        /// </summary>
        public static void UnregisterService<T>() where T : class
        {
            var type = typeof(T);
            if (Instance.services.ContainsKey(type))
            {
                Instance.services.Remove(type);
                Debug.Log($"[ServiceLocator] 서비스 등록 해제: {typeof(T).Name}");
            }
        }

        /// <summary>
        /// 캐시 정리
        /// </summary>
        public static void ClearCache()
        {
            Instance.cachedComponents.Clear();
            Debug.Log("[ServiceLocator] 캐시 정리 완료");
        }

        /// <summary>
        /// 전체 정리
        /// </summary>
        private void OnDestroy()
        {
            services?.Clear();
            cachedComponents?.Clear();
            
            if (_instance == this)
            {
                _instance = null;
            }
            
            Debug.Log("[ServiceLocator] 메모리 정리 완료");
        }

        #endregion

        #region 디버그

        /// <summary>
        /// 등록된 서비스 목록 출력
        /// </summary>
        [ContextMenu("디버그: 등록된 서비스 목록")]
        public void DebugPrintServices()
        {
            Debug.Log("=== 등록된 서비스 ===");
            foreach (var service in services)
            {
                Debug.Log($"- {service.Key.Name}: {service.Value}");
            }
            
            Debug.Log("=== 캐시된 컴포넌트 ===");
            foreach (var comp in cachedComponents)
            {
                Debug.Log($"- {comp.Key}: {comp.Value}");
            }
        }

        #endregion
    }
} 