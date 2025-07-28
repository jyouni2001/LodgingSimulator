using UnityEngine;
using System.Collections.Generic;

namespace JY
{
    /// <summary>
    /// 방 관련 명령의 기본 인터페이스
    /// </summary>
    public interface IRoomCommand
    {
        bool Execute();
        void Undo();
        string GetDescription();
    }
    
    /// <summary>
    /// 방 생성 명령
    /// </summary>
    public class CreateRoomCommand : IRoomCommand
    {
        private readonly RoomInfo roomInfo;
        private readonly IRoomFactory roomFactory;
        private readonly IRoomDataManager dataManager;
        private GameObject createdGameObject;
        
        public CreateRoomCommand(RoomInfo room, IRoomFactory factory, IRoomDataManager manager)
        {
            roomInfo = room;
            roomFactory = factory;
            dataManager = manager;
        }
        
        public bool Execute()
        {
            try
            {
                createdGameObject = roomFactory.CreateRoomGameObject(roomInfo);
                dataManager.AddRoom(roomInfo);
                
                // 중앙 이벤트 시스템에 알림
                RoomEventManager.Instance?.TriggerRoomCreated(roomInfo);
                
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"방 생성 명령 실행 실패: {ex.Message}");
                return false;
            }
        }
        
        public void Undo()
        {
            try
            {
                if (createdGameObject != null)
                {
                    Object.Destroy(createdGameObject);
                }
                
                dataManager.RemoveRoom(roomInfo.roomId);
                
                // 중앙 이벤트 시스템에 알림
                RoomEventManager.Instance?.TriggerRoomDestroyed(roomInfo);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"방 생성 명령 되돌리기 실패: {ex.Message}");
            }
        }
        
        public string GetDescription()
        {
            return $"방 생성: {roomInfo.roomId}";
        }
    }
    
    /// <summary>
    /// 방 제거 명령
    /// </summary>
    public class RemoveRoomCommand : IRoomCommand
    {
        private readonly string roomId;
        private readonly IRoomDataManager dataManager;
        private RoomInfo removedRoom;
        
        public RemoveRoomCommand(string id, IRoomDataManager manager)
        {
            roomId = id;
            dataManager = manager;
        }
        
        public bool Execute()
        {
            try
            {
                removedRoom = dataManager.GetRoomById(roomId);
                if (removedRoom == null) return false;
                
                // GameObject 제거
                if (removedRoom.gameObject != null)
                {
                    Object.Destroy(removedRoom.gameObject);
                }
                
                dataManager.RemoveRoom(roomId);
                
                // 중앙 이벤트 시스템에 알림
                RoomEventManager.Instance?.TriggerRoomDestroyed(removedRoom);
                
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"방 제거 명령 실행 실패: {ex.Message}");
                return false;
            }
        }
        
        public void Undo()
        {
            try
            {
                if (removedRoom != null)
                {
                    dataManager.AddRoom(removedRoom);
                    
                    // 중앙 이벤트 시스템에 알림
                    RoomEventManager.Instance?.TriggerRoomCreated(removedRoom);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"방 제거 명령 되돌리기 실패: {ex.Message}");
            }
        }
        
        public string GetDescription()
        {
            return $"방 제거: {roomId}";
        }
    }
    
    /// <summary>
    /// 전체 방 스캔 명령
    /// </summary>
    public class ScanRoomsCommand : IRoomCommand
    {
        private readonly IRoomDetector roomDetector;
        private List<RoomInfo> previousRooms;
        
        public ScanRoomsCommand(IRoomDetector detector)
        {
            roomDetector = detector;
        }
        
        public bool Execute()
        {
            try
            {
                // 이전 상태 백업
                previousRooms = new List<RoomInfo>(roomDetector.GetDetectedRooms());
                
                // 방 스캔 실행
                roomDetector.ScanForRooms();
                
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"방 스캔 명령 실행 실패: {ex.Message}");
                return false;
            }
        }
        
        public void Undo()
        {
            // 방 스캔의 되돌리기는 복잡하므로 로그만 출력
            Debug.LogWarning("방 스캔 명령은 되돌릴 수 없습니다.");
        }
        
        public string GetDescription()
        {
            return "전체 방 스캔";
        }
    }
    
    /// <summary>
    /// 명령 실행자 (Invoker)
    /// </summary>
    public class RoomCommandInvoker
    {
        private readonly Stack<IRoomCommand> executedCommands = new Stack<IRoomCommand>();
        private readonly Stack<IRoomCommand> undoneCommands = new Stack<IRoomCommand>();
        private const int MAX_HISTORY = 50; // 최대 히스토리 보관 수
        
        /// <summary>
        /// 명령 실행
        /// </summary>
        public bool ExecuteCommand(IRoomCommand command)
        {
            if (command == null) return false;
            
            bool success = command.Execute();
            
            if (success)
            {
                executedCommands.Push(command);
                undoneCommands.Clear(); // 새 명령 실행 시 되돌리기 히스토리 초기화
                
                // 히스토리 크기 제한
                if (executedCommands.Count > MAX_HISTORY)
                {
                    // 오래된 명령 제거 (Stack을 뒤집어서 처리)
                    var tempCommands = new IRoomCommand[MAX_HISTORY];
                    for (int i = 0; i < MAX_HISTORY && executedCommands.Count > 0; i++)
                    {
                        tempCommands[i] = executedCommands.Pop();
                    }
                    
                    executedCommands.Clear();
                    for (int i = MAX_HISTORY - 1; i >= 0; i--)
                    {
                        if (tempCommands[i] != null)
                        {
                            executedCommands.Push(tempCommands[i]);
                        }
                    }
                }
                
                Debug.Log($"명령 실행 완료: {command.GetDescription()}");
            }
            else
            {
                Debug.LogError($"명령 실행 실패: {command.GetDescription()}");
            }
            
            return success;
        }
        
        /// <summary>
        /// 명령 되돌리기
        /// </summary>
        public bool UndoCommand()
        {
            if (executedCommands.Count == 0)
            {
                Debug.LogWarning("되돌릴 명령이 없습니다.");
                return false;
            }
            
            var command = executedCommands.Pop();
            
            try
            {
                command.Undo();
                undoneCommands.Push(command);
                
                Debug.Log($"명령 되돌리기 완료: {command.GetDescription()}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"명령 되돌리기 실패: {ex.Message}");
                executedCommands.Push(command); // 실패 시 다시 넣기
                return false;
            }
        }
        
        /// <summary>
        /// 되돌린 명령 다시 실행
        /// </summary>
        public bool RedoCommand()
        {
            if (undoneCommands.Count == 0)
            {
                Debug.LogWarning("다시 실행할 명령이 없습니다.");
                return false;
            }
            
            var command = undoneCommands.Pop();
            return ExecuteCommand(command);
        }
        
        /// <summary>
        /// 명령 히스토리 정보
        /// </summary>
        public string GetCommandHistory()
        {
            var history = "=== 명령 히스토리 ===\n";
            history += $"실행된 명령: {executedCommands.Count}개\n";
            history += $"되돌린 명령: {undoneCommands.Count}개\n";
            
            int count = 0;
            foreach (var command in executedCommands)
            {
                if (count >= 10) break; // 최근 10개만 표시
                history += $"  {count + 1}. {command.GetDescription()}\n";
                count++;
            }
            
            return history;
        }
        
        /// <summary>
        /// 모든 히스토리 초기화
        /// </summary>
        public void ClearHistory()
        {
            executedCommands.Clear();
            undoneCommands.Clear();
            Debug.Log("명령 히스토리 초기화 완료");
        }
    }
} 