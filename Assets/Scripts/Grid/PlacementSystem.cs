using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlacementSystem : MonoBehaviour
{
    // 싱글톤 인스턴스
    private static PlacementSystem _instance;

    public static PlacementSystem Instance
    {
        get
        {
            if (_instance is null)
            {
                _instance = FindFirstObjectByType<PlacementSystem>();
                if (_instance is null)
                {
                    Debug.LogError("PlacementSystem 인스턴스가 씬에 존재하지 않습니다. PlacementSystem 오브젝트를 추가해주세요.");
                }
            }
            return _instance;
        }
    }

    [SerializeField] private GameObject mouseIndicator;
    [SerializeField] private GameObject cellIndicatorPrefab;
    private List<GameObject> cellIndicators = new List<GameObject>();

    [Header("컴포넌트")]
    [SerializeField] private InputManager inputManager;
    [SerializeField] private ObjectPlacer objectPlacer;
    [SerializeField] private Grid grid;
    public ObjectsDatabaseSO database;
    [SerializeField] private GameObject previewObject;

    [Header("그리드 관련")]
    [SerializeField] private List<GameObject> gridVisualization;
    [SerializeField] private List<Bounds> planeBounds;

    [Header("플레인 리스트")]
    [SerializeField] private List<GameObject> plane;
    [SerializeField] private List<GameObject> plane2f;
    [SerializeField] private List<GameObject> plane3f;
    [SerializeField] private List<GameObject> plane4f;

    private int selectedObjectIndex = -1;
    private GridData floorData, furnitureData, wallData;
    private Renderer previewRenderer;
    private Vector3Int gridPosition;
    private Quaternion previewRotation = Quaternion.identity;

    [Header("땅 구매 버튼")]
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Button purchase2FButton;
    private int currentPurchaseLevel = 1;

    private bool FloorLock = false;
    
    [SerializeField] private GridData selectedData;
    private void Awake()
    {
        // 싱글톤 인스턴스 설정
        if (_instance is not null && _instance != this)
        {
            Debug.LogWarning("이미 PlacementSystem 인스턴스가 존재합니다. 이 인스턴스를 파괴합니다.");
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // 씬 전환 시 파괴되지 않도록 설정 (선택 사항)
        // DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StopPlacement();
        InitailizeGridDatas();
        InitializeGridBounds();
        InitializePlane();

        database.InitializeDictionary();
        
        if (purchaseButton != null)
        {
            purchaseButton.onClick.AddListener(PurchaseNextLand);
        }

        if (purchase2FButton != null)
        {
            purchase2FButton.onClick.AddListener(PurchaseOtherFloor);
        }

    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            FloorLock = true;
            Debug.Log($"증축 시스템 해금 상태 = {FloorLock}");
        }

        if (selectedObjectIndex < 0) return;

        IndicatorPos();
        PreviewObjectFunc();
    }

    #region 플레인 초기화

    /// <summary>
    /// 시작 시 그리드를 전부 비활성화시켜 보이지않도록 한다.
    /// </summary>
    public void InitializePlane()
    {
        foreach (GameObject gridVisual in gridVisualization)
        {
            gridVisual.SetActive(false);
        }
    }
    #endregion

    #region 인디케이터 위치

    /// <summary>
    /// 마우스와 셀 인디케이터의 좌표를 조절한다.
    /// </summary>
    private void IndicatorPos()
    {
        Vector3 mousePosition = inputManager.GetSelectedMapPosition();
        mouseIndicator.transform.position = mousePosition;
        gridPosition = grid.WorldToCell(mousePosition);
        UpdateCellIndicators();
    }

    /// <summary>
    /// 마우스의 위치를 통해 인디케이터의 좌표를 실시간으로 변경하고, 미리보기 중일 때 또한 변경 되도록 한다.
    /// </summary>
    private void UpdateCellIndicators()
    {
        if (selectedObjectIndex < 0 || selectedObjectIndex >= database.objectsData.Count) return;

        Vector2Int objectSize = database.objectsData[selectedObjectIndex].Size;
        int requiredIndicators = objectSize.x * objectSize.y;

        // 최대 인디케이터 수 제한
        const int maxIndicators = 50; // 적절한 값으로 설정
        while (cellIndicators.Count < requiredIndicators && cellIndicators.Count < maxIndicators)
        {
            GameObject newIndicator = Instantiate(cellIndicatorPrefab, transform);
            cellIndicators.Add(newIndicator);
            if (previewRenderer == null)
            {
                previewRenderer = newIndicator.GetComponentInChildren<Renderer>();
                if (previewRenderer == null)
                {
                    Debug.LogError("cellIndicatorPrefab에 Renderer가 없습니다!");
                }
            }
        }
        /*while (cellIndicators.Count < requiredIndicators)
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
        }*/
        
        for (int i = 0; i < cellIndicators.Count; i++)
        {
            if (i < requiredIndicators)
            {
                cellIndicators[i].SetActive(true);
                List<Vector3Int> positions = floorData.CalculatePosition(gridPosition, objectSize, previewRotation, grid);
                if (i < positions.Count)
                {
                    cellIndicators[i].transform.position = grid.GetCellCenterWorld(positions[i]) - new Vector3(0, .499f, 0);
                    cellIndicators[i].transform.rotation = Quaternion.Euler(90, 0, 0);
                }
            }
            else
            {
                cellIndicators[i].SetActive(false);
            }
        }

        /*for (int i = requiredIndicators; i < cellIndicators.Count; i++)
        {
            cellIndicators[i].SetActive(false);
        }

        // GridData의 CalculatePosition과 동일한 로직 사용
        List<Vector3Int> positions = floorData.CalculatePosition(gridPosition, objectSize, previewRotation, grid);
        for (int i = 0; i < requiredIndicators && i < positions.Count; i++)
        {
            cellIndicators[i].SetActive(true);
            cellIndicators[i].transform.position = grid.GetCellCenterWorld(positions[i]) - new Vector3(0, .498f, 0);
            // 셀 인디케이터를 X축 90도로 고정 (평면에 맞게)
            cellIndicators[i].transform.rotation = Quaternion.Euler(90, 0, 0);
        }*/
    }
    #endregion

    #region 그리드 데이터 초기화

    /// <summary>
    /// 첫 그리드 데이터를 초기화한다.
    /// </summary>
    private void InitailizeGridDatas()
    {
        floorData = new GridData();
        furnitureData = new GridData();
        wallData = new GridData();
    }
    #endregion

    #region 그리드 건설반경 초기화

    /// <summary>
    /// 그리드 건설 반경을 초기화하는 함수
    /// </summary>
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
    /// <summary>
    /// 그리드 반경에 따라 건축 가능한 지역 바운드 설정
    /// </summary>
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

        if (FloorLock)
        {
            foreach (GameObject planeRend in plane2f)
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
        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

        foreach (Renderer renderer in renderers)
        {
            renderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
            renderer.SetPropertyBlock(propBlock);
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

        //GridData selectedData = database.objectsData[selectedObjectIndex].kindIndex == 0 ? floorData : furnitureData;

        switch (database.objectsData[selectedObjectIndex].kindIndex)
        {
            case 0:
                selectedData = floorData;
                Debug.Log("현재 상태: floorData");
                break;
            case 1:
                selectedData = furnitureData;
                Debug.Log("현재 상태: furnitureData");
                break;
            case 2:
                selectedData = wallData;
                Debug.Log("현재 상태: wallData");
                break;
            default:
                selectedData = furnitureData;
                Debug.Log("현재 상태: furnitureData (기본값)");
                break;
        }

        bool isWall = database.objectsData[selectedObjectIndex].IsWall;
        selectedData.AddObjectAt(
            gridPosition, 
            database.objectsData[selectedObjectIndex].Size, 
            database.objectsData[selectedObjectIndex].ID, 
            index, 
            database.objectsData[selectedObjectIndex].kindIndex, 
            previewRotation, 
            grid,
            isWall
            );
        /*GridData selectedData = database.objectsData[selectedObjectIndex].ID == 0 ? floorData : furnitureData;
        selectedData.AddObjectAt(gridPosition, database.objectsData[selectedObjectIndex].Size, database.objectsData[selectedObjectIndex].ID, index, previewRotation, grid);*/

        if (inputManager.hit.transform is not null) Debug.Log($"현재 설치된 오브젝트 : {index}, 선택한 오브젝트 : {inputManager.hit.transform.name}");
    }
    #endregion

    #region 점유상태 확인
    private bool CheckPlacementValidity(Vector3Int gridPosition, int selectedObjectIndex, Quaternion rotation)
    {
        selectedData = database.objectsData[selectedObjectIndex].ID == 0 ? floorData : furnitureData;
        
        /*switch (database.objectsData[selectedObjectIndex].kindIndex)
        {
            case 0:
                selectedData = floorData;
                break;
            case 1:
                selectedData = furnitureData;
                break;
            case 2:
                selectedData = wallData;
                break;
            default:
                selectedData = furnitureData;
                break;
        }
        */
        
        bool isWall = database.objectsData[selectedObjectIndex].IsWall;

        if (!selectedData.CanPlaceObjectAt(gridPosition, database.objectsData[selectedObjectIndex].Size, rotation, grid, isWall))
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

        if (previewRenderer is not null)
        {
            previewRenderer.material.color = placementValidity ? Color.white : Color.red;
        }
        else
        {
            Debug.LogWarning("previewRenderer가 null입니다.");
        }

        if (previewObject is not null)
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
                    renderer.material.color = placementValidity ? new Color(1f, 1f, 1f, 0.2f) : new Color(1f, 0f, 0f, 0.5f);

                }
            }
        }
    }
    #endregion

    #region 버튼 기능(테스트 기능, 후에 삭제)
    public void ResizeMesh()
    {
        UpdateGridBounds();
    }
    #endregion

    #region 땅 구매

    /// <summary>
    /// 땅을 구매하는 버튼을 통해 실행되는 함수
    /// 버튼을 클릭하면 그리드 플레인이 활성화 + 그리드 건설 반경이 확대되며, 4번(최대 횟수) 클릭 시 버튼이 비활성화 된다.
    /// </summary>
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

    /// <summary>
    /// 플레인의 이름에서 뽑아낸 정수와 반환된 정수의 값이 같을 때, 그리드 플레인을 활성화한다. 
    /// </summary>
    /// <param name="level"></param>
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

    /// <summary>
    /// 플레인의 이름에서 _를 기준으로 숫자를 뽑아내어 반환한다.
    /// </summary>
    /// <param name="planeName"></param>
    /// <returns></returns>
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

    #region 2층 구매

    // 땅이 전부 구매된 후에 2층 해금이 풀림
    // 해금 해제 후에는 2층의 땅을 모두 구입할 필요가 없기 때문에 전부다 비활성화 후 한꺼번에 활성화가 가능
    // 활성화가 된 후에 그리드 바운드가 업데이트 되어야만 함
    // 사라질 때 함께 사라지도록 테스트 필요
    // 오브젝트 활성화 후 그리드바운드만 작동되도록 테스트 = 성공
    // 그러면 ActivatePlanesByLevel 함수가 필요 없음
    // 따라서 2층을 구매하면 리스트들이 전부 활성화가 되며, 업데이트 그리드 바운드 함수가 실행되도록 테스트 시작 = 성공
    // 성공함으로서 2층 해금 시, 건축모드에서는 2층 그리드가 활성화가 됌
    // 조건은 무조건 땅을 모두 구매한 후에 해금 되도록 설정


    /// <summary>
    /// 버튼의 기능으로서, 함수가 실행되면 2층 플레인이 모두 활성화가 되며, 그리드 건설 반경이 확대된다.
    /// </summary>
    private void PurchaseOtherFloor()
    {
        if (!FloorLock) return;

        if (currentPurchaseLevel < 4)
        {
            Debug.LogWarning("모든 땅을 구매한 후에만 2층을 구매할 수 있습니다.");
            return;
        }

        foreach (GameObject planeObj in plane2f)
        {
            planeObj.SetActive(true);
        }
        UpdateGridBounds();
    }

    #endregion

    #region 건설 상태
    /// <summary>
    /// 건설모드가 되었을 때, 건설UI와 그리드를 활성화한다.
    /// </summary>
    public void EnterBuildMode()
    {
        inputManager.isBuildMode = true;
        inputManager.BuildUI.SetActive(true);

        foreach (GameObject gridVisual in gridVisualization)
        {
            gridVisual.SetActive(true);
        }

        Debug.Log("건설 상태 진입: BuildUI와 Grid 활성화");
    }

    /// <summary>
    /// 건설모드에서 해제 되었을 때, 건설UI와 그리드를 비활성화한다.
    /// </summary>
    public void ExitBuildMode()
    {
        inputManager.isBuildMode = false;
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