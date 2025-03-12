using UnityEngine;

public class BuildingPlacer : MonoBehaviour
{
    [Header("참조 설정")]
    public GridManager gridManager;      // 그리드 계산 컴포넌트 참조
    public LayerMask terrainLayer;       // 터레인에 설정한 레이어
    public GameObject buildingPrefab;    // 배치할 건물 프리팹
    public Camera cam;

    void Update()
    {
        // 마우스 왼쪽 버튼 클릭 시 처리
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            Debug.Log("눌렀나");

            // 터레인 레이어에 Raycast
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, terrainLayer))
            {
                Vector3 hitPoint = hit.point;
                Debug.Log($"{hitPoint} 누름");

                // 그리드 좌표로 변환 후, 셀 중앙 위치 계산
                Vector2Int gridCoord = gridManager.GetGridCoordinates(hitPoint);
                Vector3 placementPosition = gridManager.GetCellCenter(gridCoord);

                // 건물 배치 (회전이나 추가 로직은 필요에 따라 확장)
                Instantiate(buildingPrefab, placementPosition, Quaternion.identity);
                Debug.Log("배치?");
            }
        }
    }
}
