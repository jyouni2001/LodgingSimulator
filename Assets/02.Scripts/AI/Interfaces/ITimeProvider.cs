namespace JY.AI.Interfaces
{
    /// <summary>
    /// 시간 정보를 제공하는 인터페이스
    /// AI 시스템이 TimeSystem에 직접 의존하지 않도록 추상화
    /// </summary>
    public interface ITimeProvider
    {
        /// <summary>
        /// 현재 시간 (시)
        /// </summary>
        int CurrentHour { get; }
        
        /// <summary>
        /// 현재 시간 (분)
        /// </summary>
        int CurrentMinute { get; }
        
        /// <summary>
        /// 현재 일차
        /// </summary>
        int CurrentDay { get; }
        
        /// <summary>
        /// 시간이 특정 범위 내에 있는지 확인
        /// </summary>
        bool IsTimeInRange(int startHour, int endHour);
        
        /// <summary>
        /// 영업시간인지 확인
        /// </summary>
        bool IsBusinessHours();
        
        /// <summary>
        /// 강제 디스폰 시간인지 확인 (17시)
        /// </summary>
        bool IsForcedDespawnTime();
        
        /// <summary>
        /// 체크아웃 시간인지 확인 (11시)
        /// </summary>
        bool IsCheckoutTime();
    }

    /// <summary>
    /// TimeSystem을 ITimeProvider로 감싸는 어댑터
    /// </summary>
    public class TimeSystemAdapter : ITimeProvider
    {
        private TimeSystem timeSystem;

        public TimeSystemAdapter(TimeSystem system)
        {
            timeSystem = system;
        }

        public int CurrentHour => timeSystem?.CurrentHour ?? 0;
        public int CurrentMinute => timeSystem?.CurrentMinute ?? 0;
        public int CurrentDay => timeSystem?.CurrentDay ?? 1;

        public bool IsTimeInRange(int startHour, int endHour)
        {
            if (timeSystem == null) return false;
            int hour = timeSystem.CurrentHour;
            return hour >= startHour && hour <= endHour;
        }

        public bool IsBusinessHours()
        {
            return IsTimeInRange(11, 16);
        }

        public bool IsForcedDespawnTime()
        {
            return CurrentHour == 17 && CurrentMinute == 0;
        }

        public bool IsCheckoutTime()
        {
            return CurrentHour == 11 && CurrentMinute == 0;
        }
    }
}