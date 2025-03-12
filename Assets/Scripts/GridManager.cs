using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("그리드 설정")]
    public int gridWidth = 10;
    public int gridHeight = 10;
    public float cellSize = 1f;

    // 특정 그리드 좌표를 월드 좌표로 변환합니다.
    public Vector3 GetWorldPosition(int x, int y)
    {
        return new Vector3(x * cellSize, 0, y * cellSize);
    }

    // 월드 좌표를 그리드 좌표로 변환합니다.
    public Vector2Int GetGridCoordinates(Vector3 worldPosition)
    {
        int x = Mathf.RoundToInt(worldPosition.x / cellSize);
        int y = Mathf.RoundToInt(worldPosition.z / cellSize);
        return new Vector2Int(x, y);
    }

    // 그리드 좌표에 해당하는 셀의 중앙 위치를 반환합니다.
    public Vector3 GetCellCenter(Vector2Int coordinates)
    {
        Vector3 worldPos = GetWorldPosition(coordinates.x, coordinates.y);
        return worldPos + new Vector3(cellSize / 2, 0, cellSize / 2);
    }
}
