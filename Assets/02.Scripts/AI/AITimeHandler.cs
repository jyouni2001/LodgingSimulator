using UnityEngine;
using System;
using JY.AI.Interfaces;

namespace JY
{
    /// <summary>
    /// AI의 시간 기반 행동을 관리하는 클래스
    /// 시간에 따른 행동 변화, 스케줄 관리 등을 처리
    /// </summary>
    public class AITimeHandler
    {
        private ITimeProvider timeProvider;
        private int lastBehaviorUpdateHour = -1;
        private bool isScheduledForDespawn = false;

        public bool IsScheduledForDespawn => isScheduledForDespawn;
        public int LastBehaviorUpdateHour => lastBehaviorUpdateHour;

        // 이벤트
        public event Action OnForcedDespawnTime;
        public event Action OnCheckoutTime;
        public event Action<int> OnHourlyBehaviorUpdate;

        /// <summary>
        /// 시간 제공자로 초기화 (의존성 주입)
        /// </summary>
        public void Initialize(ITimeProvider provider)
        {
            timeProvider = provider;
            if (timeProvider == null)
            {
                AIDebugLogger.LogWarning("AITimeHandler", "TimeProvider가 제공되지 않았습니다");
            }
        }

        /// <summary>
        /// 기존 호환성을 위한 초기화 (Deprecated)
        /// </summary>
        [System.Obsolete("Initialize()는 더 이상 사용되지 않습니다. Initialize(ITimeProvider)를 사용하세요.")]
        public void Initialize()
        {
            var timeSystem = TimeSystem.Instance;
            if (timeSystem != null)
            {
                timeProvider = new TimeSystemAdapter(timeSystem);
            }
            else
            {
                AIDebugLogger.LogWarning("AITimeHandler", "TimeSystem을 찾을 수 없습니다");
            }
        }

        /// <summary>
        /// 시간 기반 업데이트 체크
        /// </summary>
        public void UpdateTimeBasedBehavior()
        {
            if (timeProvider == null) return;

            int hour = timeProvider.CurrentHour;
            int minute = timeProvider.CurrentMinute;

            // 17:00 강제 디스폰 체크
            if (timeProvider.IsForcedDespawnTime())
            {
                OnForcedDespawnTime?.Invoke();
                return;
            }

            // 11:00 체크아웃 체크
            if (timeProvider.IsCheckoutTime() && lastBehaviorUpdateHour != hour)
            {
                OnCheckoutTime?.Invoke();
                lastBehaviorUpdateHour = hour;
                return;
            }

            // 매시간 행동 업데이트
            if (minute == 0 && hour != lastBehaviorUpdateHour)
            {
                if (!isScheduledForDespawn)
                {
                    OnHourlyBehaviorUpdate?.Invoke(hour);
                }
                lastBehaviorUpdateHour = hour;
            }
        }

        /// <summary>
        /// 현재 시간을 기준으로 행동 결정
        /// </summary>
        public BehaviorDecision DetermineBehaviorByTime()
        {
            if (timeProvider == null)
            {
                return BehaviorDecision.Fallback;
            }

            int hour = timeProvider.CurrentHour;

            // 17:00 이후는 모든 AI 디스폰
            if (hour >= 17)
            {
                return BehaviorDecision.Despawn;
            }

            // 영업시간 중 행동 결정
            if (timeProvider.IsBusinessHours())
            {
                return DetermineBusinessHoursBehavior(hour);
            }

            // 기타 시간대
            return BehaviorDecision.Fallback;
        }

        /// <summary>
        /// 영업시간 중 행동 결정
        /// </summary>
        private BehaviorDecision DetermineBusinessHoursBehavior(int hour)
        {
            float randomValue = UnityEngine.Random.value;

            return hour switch
            {
                11 => randomValue < 0.7f ? BehaviorDecision.Queue : BehaviorDecision.Wander,
                12 => randomValue < 0.8f ? BehaviorDecision.Queue : BehaviorDecision.Wander,
                13 => randomValue < 0.6f ? BehaviorDecision.Queue : BehaviorDecision.Wander,
                14 => randomValue < 0.5f ? BehaviorDecision.Queue : BehaviorDecision.Wander,
                15 => randomValue < 0.4f ? BehaviorDecision.Queue : BehaviorDecision.Wander,
                16 => randomValue < 0.3f ? BehaviorDecision.Queue : BehaviorDecision.Wander,
                _ => BehaviorDecision.Fallback
            };
        }

        /// <summary>
        /// 방 사용 중 행동 재결정
        /// </summary>
        public RoomBehaviorDecision DetermineRoomBehavior()
        {
            float randomValue = UnityEngine.Random.value;
            return randomValue < 0.5f ? RoomBehaviorDecision.InsideRoom : RoomBehaviorDecision.OutsideRoom;
        }

        /// <summary>
        /// 11시 디스폰 예약 설정
        /// </summary>
        public void ScheduleForDespawn()
        {
            isScheduledForDespawn = true;
        }

        /// <summary>
        /// 현재 시간 정보 반환
        /// </summary>
        public (int hour, int minute) GetCurrentTime()
        {
            if (timeProvider == null) return (0, 0);
            return (timeProvider.CurrentHour, timeProvider.CurrentMinute);
        }
    }

    /// <summary>
    /// 시간 기반 행동 결정 열거형
    /// </summary>
    public enum BehaviorDecision
    {
        Queue,      // 대기열로 이동
        Wander,     // 배회
        Despawn,    // 디스폰
        Fallback    // 기본 행동
    }

    /// <summary>
    /// 방 사용 중 행동 결정 열거형
    /// </summary>
    public enum RoomBehaviorDecision
    {
        InsideRoom,  // 방 내부 배회
        OutsideRoom  // 방 외부 배회
    }
}