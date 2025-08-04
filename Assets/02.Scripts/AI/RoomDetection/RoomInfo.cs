using UnityEngine;
using System.Collections.Generic;

namespace JY.RoomDetection
{
    /// <summary>
    /// 감지된 방의 정보를 저장하는 클래스
    /// </summary>
    [System.Serializable]
    public class RoomInfo
    {
        [Header("방 기본 정보")]
        public string roomId;
        public Vector3 center;
        public Bounds bounds;
        public GameObject gameObject;

        [Header("방 구성 요소")]
        public List<GameObject> walls = new List<GameObject>();
        public List<GameObject> doors = new List<GameObject>();
        public List<GameObject> beds = new List<GameObject>();
        
        [Header("그리드 정보")]
        public List<Vector3Int> floorCells = new List<Vector3Int>();

        [Header("특수 방 설정")]
        public bool isSunbedRoom = false;
        public float fixedPrice = 0f;
        public float fixedReputation = 0f;

        /// <summary>
        /// 방이 유효한지 확인
        /// </summary>
        public bool isValid(int minWalls, int minDoors, int minBeds)
        {
            return walls.Count >= minWalls && 
                   doors.Count >= minDoors && 
                   beds.Count >= minBeds &&
                   floorCells.Count > 0;
        }

        /// <summary>
        /// 방 정보 요약 문자열 반환
        /// </summary>
        public string GetSummary()
        {
            return $"방 {roomId}: 벽 {walls.Count}개, 문 {doors.Count}개, 침대 {beds.Count}개, 바닥 {floorCells.Count}개";
        }

        /// <summary>
        /// 특정 위치가 이 방에 포함되는지 확인
        /// </summary>
        public bool ContainsPosition(Vector3 worldPosition)
        {
            return bounds.Contains(worldPosition);
        }

        /// <summary>
        /// 선베드 방으로 설정
        /// </summary>
        public void SetAsSunbedRoom(float price, float reputation)
        {
            isSunbedRoom = true;
            fixedPrice = price;
            fixedReputation = reputation;
        }
    }
}