using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private GameObject mouseIndicator;
    [SerializeField] private GameObject cellIndicatorPrefab;
    private List<GameObject> cellIndicators = new List<GameObject>();
    
    [SerializeField] private InputManager inputManager;
    [SerializeField] private ObjectPlacer objectPlacer;
    [SerializeField] private Grid grid;
    [SerializeField] private ObjectsDatabaseSO database;
    [SerializeField] private GameObject previewObject;

    [SerializeField] private List<GameObject> gridVisualization;
    [SerializeField] private List<Bounds> planeBounds;
    [SerializeField] private List<GameObject> plane;

    private int selectedObjectIndex = -1;
    private GridData floorData, furnitureData;
    private Renderer previewRenderer;
    private Vector3Int gridPosition;
    private Quaternion previewRotation = Quaternion.identity; 
    
    [SerializeField] private Button purchaseButton;
    private int currentPurchaseLevel = 1;
    private bool isBuildMode = false;
    
    private void Start()
    {
        StopPlacement();
        InitailizeGridDatas();
        InitializeGridBounds();
        InitializePlane();

        if (purchaseButton != null)
        {
            purchaseButton.onClick.AddListener(PurchaseNextLand);
        }
    }

    private void Update()
    {
        if (selectedObjectIndex < 0) return;

        IndicatorPos();
        PreviewObjectFunc();
    }

    #region 플레인 초기화
    public void InitializePlane()
    {
        foreach (GameObject gridVisual in gridVisualization)
        {
            gridVisual.SetActive(false);
        }
    }
    #endregion

    #region 인디케이터 위치
    private void IndicatorPos()
    {
        Vector3 mousePosition = inputManager.GetSelectedMapPosition();
        mouseIndicator.transform.position = mousePosition;
        gridPosition = grid.WorldToCell(mousePosition);
        UpdateCellIndicators();
    }

    private void UpdateCellIndicators()
    {
        if (selectedObjectIndex < 0 || selectedObjectIndex >= database.objectsData.Count) return;

        Vector2Int objectSize = database.objectsData[selectedObjectIndex].Size;
        int requiredIndicators = objectSize.x * objectSize.y;

        while (cellIndicators.Count < requiredIndicators)
        {
            GameObject newIndicator = Instantiate(cellIndicatorPrefab, transform);
            cellIndicators.Add(newIndicator);
            // previewRenderer가 null이면 첫 번째 셀 인디케이터에서 초기화
            if (previewRenderer == null)
            {
                previewRenderer = newIndicator.GetComponentInChildren<Renderer>();
                if (previewRenderer == null)
                {
                    Debug.LogError("cellIndicatorPrefab에 Renderer가 없습니다!");
                }
            }
        }

        for (int i = requiredIndicators; i < cellIndicators.Count; i++)
        {
            cellIndicators[i].SetActive(false);
        }

        // GridData의 CalculatePosition과 동일한 로직 사용
        List<Vector3Int> positions = floorData.CalculatePosition(gridPosition, objectSize, previewRotation, grid);
        for (int i = 0; i < requiredIndicators && i < positions.Count; i++)
        {
            cellIndicators[i].SetActive(true);
            cellIndicators[i].transform.position = grid.GetCellCenterWorld(positions[i]) - new Vector3(0, .499f, 0);
            // 셀 인디케이터를 X축 90도로 고정 (평면에 맞게)
            cellIndicators[i].transform.rotation = Quaternion.Euler(90, 0, 0);
        }
    }
    #endregion

    #region 그리드 데이터 초기화
    private void InitailizeGridDatas()
    {
        floorData = new GridData();
        furnitureData = new GridData();
    }
    #endregion

    #region 그리드 건설반경 초기화
    private void InitializeGridBounds()
    {
        if (plane == null || plane.Count == 0)
        {
            Debug.LogWarning("plane 리스트가 null이거나 비어 있습니다.");
            return;
        }
        
        planeBounds.Clear();

        if (plane[0] != null)
        {
            Renderer firstPlaneRenderer = plane[0].GetComponent<Renderer>();
            if (firstPlaneRenderer != null)
            {
                Bounds rendererBounds = firstPlaneRenderer.bounds;
                rendererBounds.Expand(new Vector3(0, 1, 0));
                planeBounds.Add(rendererBounds);
            }
        }
    }
    #endregion

    #region UpdateGridBounds
    private void UpdateGridBounds()
    {
        foreach (GameObject planeRend in plane)
        {
            if (planeRend != null && planeRend.activeSelf)
            {
                Renderer planeRenderer = planeRend.GetComponent<Renderer>();
                if (planeRenderer != null)
                {
                    Bounds rendererBounds = planeRenderer.bounds;
                    rendererBounds.Expand(new Vector3(0, 1, 0));
                    
                    bool alreadyExists = planeBounds.Exists(b => b.center == rendererBounds.center && b.size == rendererBounds.size);
                    if (!alreadyExists)
                    {
                        planeBounds.Add(rendererBounds);
                        gridVisualization.Add(planeRend);
                    }
                }
            }
        }
    }
    #endregion

    #region 건축 시작
    public void StartPlacement(int ID)
    {
        StopPlacement();
        
        selectedObjectIndex = database.objectsData.FindIndex(data => data.ID == ID);
        if (selectedObjectIndex < 0)
        {
            Debug.LogError($"No Id Found{ID}");
            return;
        }
        
        CreatePreview();
        inputManager.OnClicked += PlaceStructure;
        inputManager.OnExit += StopPlacement;
        UpdateCellIndicators(); // 즉시 셀 인디케이터 업데이트
    }
    #endregion

    #region 미리보기
    public void CreatePreview()
    {
        if (selectedObjectIndex < 0 || selectedObjectIndex >= database.objectsData.Count) return;
        
        if (previewObject != null)
        {
            Destroy(previewObject);
        }

        previewObject = Instantiate(database.objectsData[selectedObjectIndex].Prefab);
        ApplyPreviewMaterial(previewObject);
    }

    private void ApplyPreviewMaterial(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            Material material = renderer.material;
            material.color = new Color(1f, 1f, 1f, 0.5f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
    #endregion

    #region 오브젝트 배치
    private void PlaceStructure()
    {
        if (inputManager.IsPointerOverUI())
        {
            return;
        }

        Vector3 mousePosition = inputManager.GetSelectedMapPosition();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);

        bool placementValidity = CheckPlacementValidity(gridPosition, selectedObjectIndex, previewRotation);
        if (!placementValidity) return;

        Vector3 worldPosition = grid.GetCellCenterWorld(gridPosition);
        int index = objectPlacer.PlaceObject(database.objectsData[selectedObjectIndex].Prefab, worldPosition, previewRotation);

        GridData selectedData = database.objectsData[selectedObjectIndex].ID == 0 ? floorData : furnitureData;
        selectedData.AddObjectAt(gridPosition, database.objectsData[selectedObjectIndex].Size, database.objectsData[selectedObjectIndex].ID, index, previewRotation, grid);

        Debug.Log($"현재 설치된 오브젝트 : {index}");
    }
    #endregion

    #region 점유상태 확인
    private bool CheckPlacementValidity(Vector3Int gridPosition, int selectedObjectIndex, Quaternion rotation)
    {
        GridData selectedData = database.objectsData[selectedObjectIndex].ID == 0 ? floorData : furnitureData;

        if (!selectedData.CanPlaceObjectAt(gridPosition, database.objectsData[selectedObjectIndex].Size, rotation, grid))
        {
            return false;
        }

        List<Vector3Int> positionsToCheck = selectedData.CalculatePosition(gridPosition, database.objectsData[selectedObjectIndex].Size, rotation, grid);
        foreach (Vector3Int pos in positionsToCheck)
        {
            Vector3 worldPos = grid.GetCellCenterWorld(pos);
            bool isWithinBounds = false;

            foreach (Bounds bound in planeBounds)
            {
                if (bound.Contains(worldPos))
                {
                    isWithinBounds = true;
                    break;
                }
            }

            if (!isWithinBounds)
            {
                Debug.Log($"그리드 반경을 벗어남: {pos}");
                return false;
            }
        }

        return true;
    }
    #endregion

    #region 건축 종료
    private void StopPlacement()
    {
        selectedObjectIndex = -1;
        
        foreach (GameObject indicator in cellIndicators)
        {
            indicator.SetActive(false);
        }
        
        inputManager.OnClicked -= PlaceStructure;
        inputManager.OnExit -= StopPlacement;

        if (previewObject != null)
        {
            Destroy(previewObject);
            previewObject = null;
        }
    }
    #endregion    

    #region 프리뷰 변경
    private void PreviewObjectFunc()
    {
        bool placementValidity = CheckPlacementValidity(gridPosition, selectedObjectIndex, previewRotation);

        if (previewRenderer != null)
        {
            previewRenderer.material.color = placementValidity ? Color.white : Color.red;
        }
        else
        {
            Debug.LogWarning("previewRenderer가 null입니다.");
        }

        if (previewObject != null)
        {
            previewObject.transform.position = grid.GetCellCenterWorld(gridPosition);
            previewObject.transform.rotation = previewRotation;
            Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                renderer.material.color = placementValidity ? new Color(1f, 1f, 1f, 0.5f) : new Color(1f, 0f, 0f, 0.5f);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                previewRotation = Quaternion.Euler(0, previewRotation.eulerAngles.y + 90, 0);
                previewObject.transform.rotation = previewRotation;
                UpdateCellIndicators();
            }
        }
        
        foreach (GameObject indicator in cellIndicators)
        {
            if (indicator.activeSelf)
            {
                Renderer renderer = indicator.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = placementValidity ? Color.white : Color.red;
                }
            }
        }
    }
    #endregion

    #region 버튼 기능
    public void ResizeMesh()
    {
        UpdateGridBounds();
    }
    #endregion
    
    #region 땅 구매
    public void PurchaseNextLand()
    {
        currentPurchaseLevel++;
        Debug.Log(currentPurchaseLevel);

        ActivatePlanesByLevel(currentPurchaseLevel);
        UpdateGridBounds();
        
        if (currentPurchaseLevel >= 4)
        {
            Debug.Log("모든 땅이 구매되었습니다!");
            if (purchaseButton != null)
            {
                purchaseButton.gameObject.SetActive(false);
            }
            return;
        }
        
        Debug.Log($"현재 구매 단계: {currentPurchaseLevel}, 활성화된 Plane 수: {gridVisualization.Count}");
    }

    private void ActivatePlanesByLevel(int level)
    {
        foreach (GameObject planeObj in plane)
        {
            string planeName = planeObj.name;
            int planeLevel = ExtractLevelFromPlaneName(planeName);

            if (planeLevel == level)
            {
                planeObj.SetActive(true);
                Debug.Log($"활성화된 Plane: {planeName}");
            }
        }
    }

    private int ExtractLevelFromPlaneName(string planeName)
    {
        string[] parts = planeName.Split('_');
        if (parts.Length > 1 && int.TryParse(parts[1], out int level))
        {
            return level;
        }
        Debug.LogWarning($"Plane 이름에서 레벨을 추출할 수 없습니다: {planeName}");
        return 0;
    }
    #endregion
    
    #region 건설 상태
    public void EnterBuildMode()
    {
        isBuildMode = true;
        inputManager.BuildUI.SetActive(true);

        foreach (GameObject gridVisual in gridVisualization)
        {
            gridVisual.SetActive(true);
        }

        Debug.Log("건설 상태 진입: BuildUI와 Grid 활성화");
    }
    
    public void ExitBuildMode()
    {
        isBuildMode = false;
        StopPlacement();
        inputManager.BuildUI.SetActive(false);

        foreach (GameObject gridVisual in gridVisualization)
        {
            gridVisual.SetActive(false);
        }

        Debug.Log("건설 상태 종료: BuildUI와 Grid 비활성화");
    }
    #endregion
}