using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JY
{
    /// <summary>
    /// 방 생성을 담당하는 팩토리 클래스 (인터페이스 구현)
    /// 방 정보를 바탕으로 실제 게임 오브젝트를 생성하고 설정
    /// </summary>
    public class RoomFactory : IRoomFactory
    {
        private readonly RoomScanSettings settings;
        
        public RoomFactory(RoomScanSettings scanSettings)
        {
            settings = scanSettings;
        }
        
        /// <summary>
        /// 방 리스트에서 게임 오브젝트들 생성
        /// </summary>
        public List<GameObject> CreateRoomGameObjects(List<RoomInfo> validRooms)
        {
            var createdRooms = new List<GameObject>();
            
            foreach (var room in validRooms)
            {
                var roomGameObject = CreateRoomGameObject(room);
                if (roomGameObject != null)
                {
                    createdRooms.Add(roomGameObject);
                    RoomUtilities.DebugLog($"방 오브젝트 생성 완료: {room.roomId}", false, settings);
                }
            }
            
            RoomUtilities.DebugLog($"총 {createdRooms.Count}개 방 오브젝트 생성 완료", true, settings);
            
            return createdRooms;
        }
        
        /// <summary>
        /// 단일 방 게임 오브젝트 생성
        /// </summary>
        public GameObject CreateRoomGameObject(RoomInfo room)
        {
            if (room == null || !room.isValid)
            {
                RoomUtilities.DebugLog($"유효하지 않은 방 정보로 인한 생성 실패", true, settings);
                return null;
            }
            
            // 기존 게임 오브젝트가 있다면 제거
            if (room.gameObject != null)
            {
                Object.DestroyImmediate(room.gameObject);
            }
            
            // 새 게임 오브젝트 생성
            GameObject roomObj = new GameObject(GenerateRoomName(room));
            roomObj.transform.position = room.center;
            roomObj.tag = RoomConstants.TAG_ROOM;
            
            // 컴포넌트 추가
            SetupRoomComponents(roomObj, room);
            
            // 방 정보에 참조 저장
            room.gameObject = roomObj;
            
            return roomObj;
        }
        
        /// <summary>
        /// 방 컴포넌트들 설정
        /// </summary>
        private void SetupRoomComponents(GameObject roomObj, RoomInfo room)
        {
            // RoomContents 컴포넌트 추가
            var roomContents = roomObj.AddComponent<RoomContents>();
            ConfigureRoomContents(roomContents, room);
            
            // 콜라이더 추가
            var collider = roomObj.AddComponent<BoxCollider>();
            ConfigureRoomCollider(collider, room);
            
            // 선베드 방인 경우 특별 설정
            if (room.isSunbedRoom)
            {
                SetupSunbedRoomSpecial(roomObj, room);
            }
            
            // 디버그 정보 (개발용)
            if (settings.showDebugLogs)
            {
                AddDebugComponents(roomObj, room);
            }
        }
        
        /// <summary>
        /// RoomContents 컴포넌트 설정
        /// </summary>
        private void ConfigureRoomContents(RoomContents roomContents, RoomInfo room)
        {
            roomContents.roomBounds = room.bounds;
            roomContents.basePrice = room.GetFinalPrice();
            roomContents.reputationValue = room.GetFinalReputation();
            
            // 방 타입별 특별 설정
            if (room.isSunbedRoom)
            {
                roomContents.roomType = "SunbedRoom";
            }
            else
            {
                roomContents.roomType = "StandardRoom";
            }
            
            // 방 정보 저장
            roomContents.roomInfo = GenerateRoomInfoString(room);
        }
        
        /// <summary>
        /// 방 콜라이더 설정
        /// </summary>
        private void ConfigureRoomCollider(BoxCollider collider, RoomInfo room)
        {
            collider.center = Vector3.zero; // 로컬 좌표계 기준 중심
            collider.size = room.bounds.size;
            collider.isTrigger = true; // 방은 트리거로 설정
        }
        
        /// <summary>
        /// 선베드 방 특별 설정
        /// </summary>
        private void SetupSunbedRoomSpecial(GameObject roomObj, RoomInfo room)
        {
            // 선베드 방 표시를 위한 머티리얼 또는 컴포넌트 추가 (필요시)
            var renderer = roomObj.AddComponent<MeshRenderer>();
            var meshFilter = roomObj.AddComponent<MeshFilter>();
            
            // 간단한 평면 메시 생성 (시각적 표시용)
            meshFilter.mesh = CreatePlaneMesh(room.bounds.size);
            
            // 반투명 머티리얼 설정 (선베드 방 구분용)
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color(1f, 1f, 0f, 0.3f); // 노란색 반투명
            material.SetFloat("_Mode", 3); // Transparent mode
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            
            renderer.material = material;
        }
        
        /// <summary>
        /// 디버그 컴포넌트 추가
        /// </summary>
        private void AddDebugComponents(GameObject roomObj, RoomInfo room)
        {
            // 디버그 정보 표시를 위한 텍스트 컴포넌트 (3D 텍스트)
            var textObj = new GameObject("DebugInfo");
            textObj.transform.SetParent(roomObj.transform);
            textObj.transform.localPosition = Vector3.up * 2f; // 방 위에 표시
            
            var textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = $"Room: {room.roomId}\nPrice: {room.GetFinalPrice():F0}\nRep: {room.GetFinalReputation():F0}";
            textMesh.fontSize = 20;
            textMesh.color = Color.white;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            
            // 텍스트가 항상 카메라를 향하도록 설정
            var billboard = textObj.AddComponent<Billboard>();
        }
        
        /// <summary>
        /// 방 이름 생성
        /// </summary>
        private string GenerateRoomName(RoomInfo room)
        {
            string baseName = room.isSunbedRoom ? "SunbedRoom" : "Room";
            string floorInfo = $"F{room.floorLevel}";
            string sizeInfo = $"S{room.GetRoomSize()}";
            
            return $"{baseName}_{floorInfo}_{sizeInfo}_{room.roomId[^8..]}"; // 마지막 8자리 ID
        }
        
        /// <summary>
        /// 방 정보 문자열 생성
        /// </summary>
        private string GenerateRoomInfoString(RoomInfo room)
        {
            return $"Room Info:\n" +
                   $"ID: {room.roomId}\n" +
                   $"Type: {(room.isSunbedRoom ? "Sunbed" : "Standard")}\n" +
                   $"Floor: {room.floorLevel}\n" +
                   $"Size: {room.GetRoomSize()}\n" +
                   $"Price: {room.GetFinalPrice():F0}\n" +
                   $"Reputation: {room.GetFinalReputation():F0}\n" +
                   $"Walls: {room.walls.Count}\n" +
                   $"Doors: {room.doors.Count}\n" +
                   $"Beds: {room.beds.Count}\n" +
                   $"Sunbeds: {room.sunbeds.Count}";
        }
        
        /// <summary>
        /// 평면 메시 생성 (선베드 방 시각화용)
        /// </summary>
        private Mesh CreatePlaneMesh(Vector3 size)
        {
            var mesh = new Mesh();
            
            float halfX = size.x * 0.5f;
            float halfZ = size.z * 0.5f;
            
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-halfX, 0.01f, -halfZ),
                new Vector3(-halfX, 0.01f, halfZ),
                new Vector3(halfX, 0.01f, halfZ),
                new Vector3(halfX, 0.01f, -halfZ)
            };
            
            Vector2[] uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(1, 0)
            };
            
            int[] triangles = new int[]
            {
                0, 1, 2,
                0, 2, 3
            };
            
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            
            return mesh;
        }
        
        /// <summary>
        /// 기존 방 오브젝트들 안전하게 정리
        /// </summary>
        public void CleanupExistingRooms()
        {
            try
            {
                var existingRooms = GameObject.FindGameObjectsWithTag(RoomConstants.TAG_ROOM);
                int cleanedCount = 0;
                
                foreach (var room in existingRooms)
                {
                    if (room != null)
                    {
                        try
                        {
                            // 런타임에서는 Destroy, 에디터에서는 DestroyImmediate 사용
                            #if UNITY_EDITOR
                            if (UnityEditor.EditorApplication.isPlaying)
                            {
                                Object.Destroy(room);
                            }
                            else
                            {
                                Object.DestroyImmediate(room);
                            }
                            #else
                            Object.Destroy(room);
                            #endif
                            
                            cleanedCount++;
                        }
                        catch (System.Exception ex)
                        {
                            RoomUtilities.DebugLog($"방 오브젝트 정리 실패 ({room.name}): {ex.Message}", true, settings);
                        }
                    }
                }
                
                RoomUtilities.DebugLog($"기존 {cleanedCount}개 방 오브젝트 정리 완료", true, settings);
            }
            catch (System.Exception ex)
            {
                RoomUtilities.DebugLog($"방 오브젝트 정리 중 치명적 오류: {ex.Message}", true, settings);
            }
        }
        
        /// <summary>
        /// 특정 방 오브젝트만 안전하게 정리
        /// </summary>
        public void CleanupRoomGameObject(RoomInfo room)
        {
            if (room?.gameObject != null)
            {
                try
                {
                    #if UNITY_EDITOR
                    if (UnityEditor.EditorApplication.isPlaying)
                    {
                        Object.Destroy(room.gameObject);
                    }
                    else
                    {
                        Object.DestroyImmediate(room.gameObject);
                    }
                    #else
                    Object.Destroy(room.gameObject);
                    #endif
                    
                    room.gameObject = null;
                    RoomUtilities.DebugLog($"방 오브젝트 정리: {room.roomId}", false, settings);
                }
                catch (System.Exception ex)
                {
                    RoomUtilities.DebugLog($"방 오브젝트 정리 실패 ({room.roomId}): {ex.Message}", true, settings);
                }
            }
        }
        
        /// <summary>
        /// 메모리 정리 및 리소스 해제
        /// </summary>
        public void Dispose()
        {
            CleanupExistingRooms();
            RoomUtilities.DebugLog("RoomFactory 메모리 정리 완료", true, settings);
        }
        
        /// <summary>
        /// 방 오브젝트들을 부모 오브젝트로 정리
        /// </summary>
        public GameObject OrganizeRoomsUnderParent(List<GameObject> roomObjects, string parentName = "DetectedRooms")
        {
            // 기존 부모 오브젝트 찾기 또는 생성
            var parentObj = GameObject.Find(parentName);
            if (parentObj == null)
            {
                parentObj = new GameObject(parentName);
            }
            
            // 모든 방 오브젝트를 부모 아래로 이동
            foreach (var roomObj in roomObjects)
            {
                if (roomObj != null)
                {
                    roomObj.transform.SetParent(parentObj.transform);
                }
            }
            
            RoomUtilities.DebugLog($"{roomObjects.Count}개 방 오브젝트를 {parentName} 아래로 정리 완료", true, settings);
            
            return parentObj;
        }
        
        /// <summary>
        /// 방 오브젝트 업데이트 (방 정보 변경 시)
        /// </summary>
        public void UpdateRoomGameObject(RoomInfo room)
        {
            if (room?.gameObject == null) return;
            
            var roomContents = room.gameObject.GetComponent<RoomContents>();
            if (roomContents != null)
            {
                ConfigureRoomContents(roomContents, room);
            }
            
            var collider = room.gameObject.GetComponent<BoxCollider>();
            if (collider != null)
            {
                ConfigureRoomCollider(collider, room);
            }
            
            // 이름 업데이트
            room.gameObject.name = GenerateRoomName(room);
            
            RoomUtilities.DebugLog($"방 오브젝트 업데이트 완료: {room.roomId}", false, settings);
        }
    }
    
    /// <summary>
    /// 빌보드 컴포넌트 (텍스트가 항상 카메라를 향하도록)
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        private Camera playerCamera;
        
        private void Start()
        {
            playerCamera = Camera.main;
        }
        
        private void Update()
        {
            if (playerCamera != null)
            {
                transform.LookAt(playerCamera.transform);
                transform.Rotate(0, 180, 0); // 텍스트가 뒤집히지 않도록
            }
        }
    }
} 