using System;
using UnityEngine;
using System.Collections.Generic;

public class GridData
{
    // 설치된 오브젝트 데이터가 담긴 딕셔너리
    private Dictionary<Vector3Int, PlacementData> placedObjects = new();

    #region 딕셔너리에 설치된 오브젝트 포함
    public void AddObjectAt(Vector3Int gridPosition, Vector2Int objectSize, int ID, int placedObjectIndex, Quaternion rotation, Grid grid)
    {
        List<Vector3Int> positions = CalculatePosition(gridPosition, objectSize, rotation, grid);
        PlacementData data = new PlacementData(positions, ID, placedObjectIndex);

        foreach (var pos in positions)
        {
            if (placedObjects.ContainsKey(pos))
            {
                throw new Exception($"이 셀({pos})은 이미 딕셔너리에 포함되어있다");
            }
            placedObjects[pos] = data;
        }
    }
    #endregion

    #region 그리드 좌표 계산
    private List<Vector3Int> CalculatePosition(Vector3Int gridPosition, Vector2Int objectSize, Quaternion rotation, Grid grid)
    {
        List<Vector3Int> positions = new List<Vector3Int>();

        // 회전 각도 (0, 90, 180, 270 중 하나로 반올림)
        float angle = Mathf.Round(rotation.eulerAngles.y / 90) * 90;

        // 회전된 크기 계산
        Vector2Int size = objectSize;
        if (Mathf.Approximately(angle % 180, 90))
        {
            // 90도나 270도 회전 시 x와 y 교환
            size = new Vector2Int(objectSize.y, objectSize.x);
        }

        // 회전 각도에 따른 방향 벡터 계산
        Vector2Int dirX, dirY;

        if (Mathf.Approximately(angle, 0))
        {
            // 0도 회전 - 기본 방향
            dirX = new Vector2Int(1, 0);
            dirY = new Vector2Int(0, 1);
        }
        else if (Mathf.Approximately(angle, 90))
        {
            // 90도 회전
            dirX = new Vector2Int(0, 1);
            dirY = new Vector2Int(0, -1);
        }
        else if (Mathf.Approximately(angle, 180))
        {
            // 180도 회전
            dirX = new Vector2Int(-1, 0);
            dirY = new Vector2Int(0, -1);
        }
        else // 270도
        {
            // 270도 회전
            dirX = new Vector2Int(0, -1);
            dirY = new Vector2Int(0, 1);
        }

        // 시작 위치 계산 (항상 왼쪽 상단 코너에서 시작)
        Vector3Int startPos = gridPosition;

        // 모든 셀 위치 계산
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector3Int cellPos = new Vector3Int(
                    startPos.x + dirX.x * x + dirY.x * y,
                    0,
                    startPos.z + dirX.y * x + dirY.y * y
                );
                positions.Add(cellPos);
            }
        }

        return positions;
    }
    #endregion

    #region 점유 확인 여부 플래그
    public bool CanPlaceObjectAt(Vector3Int gridPosition, Vector2Int objectSize, Quaternion rotation, Grid grid)
    {
        List<Vector3Int> positions = CalculatePosition(gridPosition, objectSize, rotation, grid);

        foreach (var pos in positions)
        {
            if (placedObjects.ContainsKey(pos))
            {
                return false;
            }
        }

        return true;
    }
    #endregion
}