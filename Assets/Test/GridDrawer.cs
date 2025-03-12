using UnityEngine;

[ExecuteAlways] // 에디터와 플레이 모드 모두에서 실행
public class GridDrawer : MonoBehaviour
{
    public int gridWidth = 10;    // 그리드의 가로 셀 개수
    public int gridHeight = 10;   // 그리드의 세로 셀 개수
    public float cellSize = 1f;   // 한 셀의 크기
    public Color gridColor = Color.green; // 그리드 선 색상

    void OnDrawGizmos()
    {
        Gizmos.color = gridColor;
        Vector3 origin = transform.position;

        // 세로선 그리기
        for (int x = 0; x <= gridWidth; x++)
        {
            Vector3 start = origin + new Vector3(x * cellSize, 0, 0);
            Vector3 end = start + new Vector3(0, 0, gridHeight * cellSize);
            Gizmos.DrawLine(start, end);
        }

        // 가로선 그리기
        for (int z = 0; z <= gridHeight; z++)
        {
            Vector3 start = origin + new Vector3(0, 0, z * cellSize);
            Vector3 end = start + new Vector3(gridWidth * cellSize, 0, 0);
            Gizmos.DrawLine(start, end);
        }
    }
}
