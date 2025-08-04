using UnityEngine;

namespace JY
{
    /// <summary>
    /// AI 시스템의 디버그 및 로깅을 담당하는 클래스
    /// 통합된 로깅 시스템으로 중복 코드를 제거
    /// </summary>
    public static class AIDebugLogger
    {
        [System.Serializable]
        public class DebugSettings
        {
            [Tooltip("디버그 로그 표시 여부")]
            public bool showDebugLogs = false;
            
            [Tooltip("중요한 이벤트만 로그 표시")]
            public bool showImportantLogsOnly = true;
            
            [Tooltip("상태 변경 로그 표시")]
            public bool showStateChangeLogs = true;
            
            [Tooltip("룸 관련 로그 표시")]
            public bool showRoomLogs = true;
            
            [Tooltip("이동 관련 로그 표시")]
            public bool showMovementLogs = false;
            
            [Tooltip("시간 관련 로그 표시")]
            public bool showTimeLogs = false;
        }

        private static DebugSettings globalSettings = new DebugSettings();

        /// <summary>
        /// 전역 디버그 설정 업데이트
        /// </summary>
        public static void UpdateGlobalSettings(DebugSettings settings)
        {
            globalSettings = settings;
        }

        /// <summary>
        /// 일반 디버그 로그
        /// </summary>
        public static void Log(string aiName, string message, LogCategory category = LogCategory.General, bool isImportant = false)
        {
            if (!ShouldLog(category, isImportant)) return;

            string categoryPrefix = GetCategoryPrefix(category);
            Debug.Log($"[AI-{categoryPrefix}] {aiName}: {message}");
        }

        /// <summary>
        /// 경고 로그
        /// </summary>
        public static void LogWarning(string aiName, string message, LogCategory category = LogCategory.General)
        {
            if (!globalSettings.showDebugLogs) return;

            string categoryPrefix = GetCategoryPrefix(category);
            Debug.LogWarning($"[AI-{categoryPrefix}] {aiName}: {message}");
        }

        /// <summary>
        /// 에러 로그
        /// </summary>
        public static void LogError(string aiName, string message, LogCategory category = LogCategory.General)
        {
            string categoryPrefix = GetCategoryPrefix(category);
            Debug.LogError($"[AI-{categoryPrefix}] {aiName}: {message}");
        }

        /// <summary>
        /// 로그 출력 여부 판단
        /// </summary>
        private static bool ShouldLog(LogCategory category, bool isImportant)
        {
            if (!globalSettings.showDebugLogs) return false;
            if (globalSettings.showImportantLogsOnly && !isImportant) return false;

            return category switch
            {
                LogCategory.StateChange => globalSettings.showStateChangeLogs,
                LogCategory.Room => globalSettings.showRoomLogs,
                LogCategory.Movement => globalSettings.showMovementLogs,
                LogCategory.Time => globalSettings.showTimeLogs,
                LogCategory.General => true,
                _ => true
            };
        }

        /// <summary>
        /// 카테고리별 접두사 반환
        /// </summary>
        private static string GetCategoryPrefix(LogCategory category)
        {
            return category switch
            {
                LogCategory.StateChange => "State",
                LogCategory.Room => "Room",
                LogCategory.Movement => "Move",
                LogCategory.Time => "Time",
                LogCategory.Queue => "Queue",
                LogCategory.General => "Gen",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// 상태 변경 로그 (특별 메서드)
        /// </summary>
        public static void LogStateChange(string aiName, string oldState, string newState)
        {
            Log(aiName, $"상태 변경: {oldState} → {newState}", LogCategory.StateChange, true);
        }

        /// <summary>
        /// 룸 관련 로그 (특별 메서드)
        /// </summary>
        public static void LogRoomAction(string aiName, string action, int roomIndex = -1)
        {
            string roomInfo = roomIndex >= 0 ? $"룸 {roomIndex + 1}번" : "룸";
            Log(aiName, $"{roomInfo} {action}", LogCategory.Room, true);
        }

        /// <summary>
        /// 이동 관련 로그 (특별 메서드)
        /// </summary>
        public static void LogMovement(string aiName, string destination)
        {
            Log(aiName, $"이동 목적지: {destination}", LogCategory.Movement);
        }

        /// <summary>
        /// 시간 관련 로그 (특별 메서드)
        /// </summary>
        public static void LogTimeEvent(string aiName, string timeEvent)
        {
            Log(aiName, $"시간 이벤트: {timeEvent}", LogCategory.Time, true);
        }
    }

    /// <summary>
    /// 로그 카테고리 열거형
    /// </summary>
    public enum LogCategory
    {
        General,        // 일반
        StateChange,    // 상태 변경
        Room,          // 룸 관련
        Movement,      // 이동 관련
        Time,          // 시간 관련
        Queue          // 대기열 관련
    }
}