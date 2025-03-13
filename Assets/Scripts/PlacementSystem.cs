using System.Collections.Generic;
using UnityEngine;

public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private GameObject   mouseIndicator, cellIndicator;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private Grid         grid;
    [SerializeField] private ObjectsDatabaseSO database;
    [SerializeField] private GameObject gridVisualization;
    [SerializeField] private GameObject previewObject; // 미리보기 객체를 저장할 변수
    private int selectedObjectIndex = -1;
    private GridData floorData, furnitureData;
    [SerializeField] private Renderer previewRenderer;
    private List<GameObject> placedGameObjects = new();
    
    public float cellHeight;

    private void Start()
    {
        StopPlacement();

        floorData = new();
        furnitureData = new();
        previewRenderer = cellIndicator.GetComponentInChildren<Renderer>();
    }

    public void StartPlacement(int ID)
    {
        StopPlacement();
        
        selectedObjectIndex = database.objectsData.FindIndex(data => data.ID == ID);
        if (selectedObjectIndex < 0)
        {
            Debug.LogError($"No Id Found{ID}");
            return;
        }
        
        gridVisualization.SetActive(true);
        cellIndicator.SetActive(true);

        // 미리보기 객체 생성
        previewObject = Instantiate(database.objectsData[selectedObjectIndex].Prefab);
        // 미리보기 객체의 모든 렌더러를 찾아 반투명 하얀색으로 설정
        Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            Material material = renderer.material;
            material.color = new Color(1f, 1f, 1f, 0.5f); // 하얀색 반투명 (알파값 0.5)
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        inputManager.OnClicked += PlaceStructure;
        inputManager.OnExit += StopPlacement;
    }

    private void PlaceStructure()
    {
        if (inputManager.IsPointerOverUI())
        {
            return;
        }
        
        // 마우스 인디케이터 위치
        Vector3 mousePosition = inputManager.GetSelectedMapPosition();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);
        
        bool placementValidity = CheckPlacementValidity(gridPosition, selectedObjectIndex);
        if (placementValidity == false) return;
               

        // 객체 크기 가져오기 (추가)
        Vector2Int objectSize = database.objectsData[selectedObjectIndex].Size;
        // 실수형으로 중앙 조정 계산 (Vector3 사용)
        Vector3 adjustedPositionOffset = new Vector3(
            (objectSize.x - 1) * 0.25f, // 2x2면 0.5, 3x3면 1.0 등
            database.objectsData[selectedObjectIndex].Prefab.gameObject.transform.position.y,
            (objectSize.y - 1) * 0.25f 
        );

        // 그리드 위치에 오프셋 적용 후 월드 위치 계산
        Vector3 worldPosition = grid.GetCellCenterWorld(gridPosition) + adjustedPositionOffset;
        Debug.Log($"gridPoisiton = {gridPosition}, objectSize = {objectSize},AdjustedGridPosition = {worldPosition}");


        GameObject newObject = Instantiate(database.objectsData[selectedObjectIndex].Prefab);
        newObject.transform.position = worldPosition;
        //newObject.transform.position = grid.CellToWorld(gridPosition);
        //newObject.transform.position = grid.GetCellCenterWorld(gridPosition);

        placedGameObjects.Add(newObject);
        GridData selectedData = database.objectsData[selectedObjectIndex].ID == 0 
            ? floorData 
            : furnitureData;
        selectedData.AddObjectAt(gridPosition, 
            database.objectsData[selectedObjectIndex].Size, 
            database.objectsData[selectedObjectIndex].ID,
            placedGameObjects.Count - 1);
        
        Debug.Log($"현재 설치된 오브젝트 :  {placedGameObjects.Count}");
    }

    private bool CheckPlacementValidity(Vector3Int gridPosition, int selectedObjectIndex)
    {
        GridData selectedData = database.objectsData[selectedObjectIndex].ID == 0 
            ? floorData 
            : furnitureData;
        
        return selectedData.CanPlaceObjectAt(gridPosition, database.objectsData[selectedObjectIndex].Size);
    }

    private void StopPlacement()
    {
        selectedObjectIndex = -1;
        gridVisualization.SetActive(false);
        cellIndicator.SetActive(false);
        inputManager.OnClicked -= PlaceStructure;
        inputManager.OnExit -= StopPlacement;

        // 미리보기 객체 제거
        if (previewObject != null)
        {
            Destroy(previewObject);
            previewObject = null;
        }
    }

    private void Update()
    {
        if (selectedObjectIndex < 0) return;
        
        // 마우스 인디케이터 위치
        Vector3 mousePosition = inputManager.GetSelectedMapPosition();
        mouseIndicator.transform.position = mousePosition;
        
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);
        
        bool placementValidity = CheckPlacementValidity(gridPosition, selectedObjectIndex);
        previewRenderer.material.color = placementValidity ? Color.white : Color.red;

        // 객체 크기에 따라 미리보기 위치 조정
        Vector2Int objectSize = database.objectsData[selectedObjectIndex].Size;
        Vector3 adjustedPositionOffset = new Vector3(
            (objectSize.x - 1) * 0.25f,
            database.objectsData[selectedObjectIndex].Prefab.gameObject.transform.position.y,
            (objectSize.y - 1) * 0.25f
        );
        Vector3 pos = grid.GetCellCenterWorld(gridPosition);
        cellIndicator.transform.position = pos + adjustedPositionOffset;
        // 미리보기 객체 위치 업데이트
        if (previewObject != null)
        {
            previewObject.transform.position = pos + adjustedPositionOffset;
            Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                renderer.material.color = placementValidity ? new Color(1f, 1f, 1f, 0.5f) : new Color(1f, 0f, 0f, 0.5f); // 유효하면 하얀색, 아니면 빨간색
            }
        }
    }
}
