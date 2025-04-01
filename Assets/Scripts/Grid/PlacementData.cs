using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설치된 오브젝트의 데이터를 저장하는 클래스
/// </summary>
public class PlacementData 
{
    public List<Vector3Int> occupiedPositions;
    public int ID { get; private set; }
    public int PlacedObjectIndex { get; private set; }


    // KindIndex
    // 0 = 바닥 오브젝트
    // 1 = 가구 오브젝트
    // 2 = 벽 오브젝트
    
    public int kindIndex { get; private set; }

    public PlacementData(List<Vector3Int> occupiedPositions, int id, int placedObjectIndex, int kindOfIndex)
    {
        this.occupiedPositions = occupiedPositions;
        ID = id;
        PlacedObjectIndex = placedObjectIndex;
        kindIndex = kindOfIndex;
    }
}