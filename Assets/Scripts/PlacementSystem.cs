using System.Collections.Generic;
using UnityEngine;

public class PlacementSystem : MonoBehaviour
{
    [SerializeField] private GameObject   mouseIndicator;   // 인디케이터 (커서)
    [SerializeField] private GameObject   cellIndicator;    // 인디케이터 (셀커서)
    [SerializeField] private InputManager inputManager;     // 인풋매니저
    [SerializeField] private Grid         grid;             // 그리드 컴포넌트
    [SerializeField] private ObjectsDatabaseSO database;    // 데이터
    [SerializeField] private GameObject gridVisualization;  // 시각화 그리드 
    [SerializeField] private GameObject previewObject;      // 미리보기 객체를 저장할 변수
    [SerializeField] private GameObject plane;              // 그리드 반경체크 플레인
    
    private int              selectedObjectIndex = -1;      // 인덱스 초기화
    private GridData         floorData, furnitureData;      // 그리드 데이터
    private Renderer         previewRenderer;               // 미리보기 머티리얼 렌더러
    private List<GameObject> placedGameObjects   = new();   // 리스트 선언
    private Vector3Int       gridPosition;                  // 그리드 좌표
    private Bounds           planeBounds;                   // 플레인 반경 좌표

    private Quaternion       previewRotation     = Quaternion.identity; 
    
    private void Start()
    {
        StopPlacement();
        InitailizeGridDatas();
        InitializeGridBounds();

        previewRenderer = cellIndicator.GetComponentInChildren<Renderer>();
    }

    private void Update()
    {
        if (selectedObjectIndex < 0) return;

        // 마우스 인디케이터 위치
        Vector3 mousePosition = inputManager.GetSelectedMapPosition();
        mouseIndicator.transform.position = mousePosition;

        // 셀 인디케이터 위치
        gridPosition = grid.WorldToCell(mousePosition);
        cellIndicator.transform.position = grid.GetCellCenterWorld(gridPosition) - new Vector3(0, .25f, 0);

        PreviewObjectFunc();
    }

    #region 그리드 데이터 초기화

    private void InitailizeGridDatas()
    {
        floorData = new();
        furnitureData = new();
    }

    #endregion

    #region 그리드 건설반경 초기화

    private void InitializeGridBounds()
    {
        // Plane의 경계 계산 및 디버깅
        if (plane != null)
        {
            Renderer planeRenderer = plane.GetComponent<Renderer>();
            if (planeRenderer != null)
            {
                planeBounds = planeRenderer.bounds;
                planeBounds.Expand(new Vector3(0, 1, 0));
            }
        }
        else
        {
            Debug.LogError("Plane 오브젝트가 지정되지 않았습니다.");
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
        
        gridVisualization.SetActive(true);
        cellIndicator.SetActive(true);

        CreatePreview();

        inputManager.OnClicked += PlaceStructure;
        inputManager.OnExit += StopPlacement;
    }
    #endregion

    #region 미리보기
    public void CreatePreview()
    {
        // 미리보기 생성
        previewObject = Instantiate(database.objectsData[selectedObjectIndex].Prefab);
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
    }
    #endregion

    #region 오브젝트 배치
    private void PlaceStructure()
    {
        if (inputManager.IsPointerOverUI())
        {
            return;
        }

        // 마우스 인디케이터 위치
        Vector3 mousePosition = inputManager.GetSelectedMapPosition();
        Vector3Int gridPosition = grid.WorldToCell(mousePosition);

        bool placementValidity = CheckPlacementValidity(gridPosition, selectedObjectIndex, previewRotation);
        if (placementValidity == false) return;

        // 그리드 위치에 오프셋 적용 후 월드 위치 계산
        Vector3 worldPosition = grid.GetCellCenterWorld(gridPosition);

        GameObject newObject = Instantiate(database.objectsData[selectedObjectIndex].Prefab);
        newObject.transform.position = worldPosition;
        newObject.transform.rotation = previewRotation;

        placedGameObjects.Add(newObject);
        GridData selectedData = database.objectsData[selectedObjectIndex].ID == 0
            ? floorData
            : furnitureData;

        selectedData.AddObjectAt(gridPosition,
            database.objectsData[selectedObjectIndex].Size,
            database.objectsData[selectedObjectIndex].ID,
            placedGameObjects.Count - 1,
            previewRotation,  // 회전 정보 추가
            grid);

        Debug.Log($"현재 설치된 오브젝트 :  {placedGameObjects.Count}");
    }

    /*private void PlaceStructure()
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
        

        // 그리드 위치에 오프셋 적용 후 월드 위치 계산
        Vector3 worldPosition = grid.GetCellCenterWorld(gridPosition);

        GameObject newObject = Instantiate(database.objectsData[selectedObjectIndex].Prefab);
        newObject.transform.position = worldPosition;
        newObject.transform.rotation = previewRotation;

        placedGameObjects.Add(newObject);
        GridData selectedData = database.objectsData[selectedObjectIndex].ID == 0 
            ? floorData 
            : furnitureData;
        
        selectedData.AddObjectAt(gridPosition, 
            database.objectsData[selectedObjectIndex].Size, 
            database.objectsData[selectedObjectIndex].ID,
            placedGameObjects.Count - 1);
        
        Debug.Log($"현재 설치된 오브젝트 :  {placedGameObjects.Count}");
    }*/

    #endregion

    #region 점유상태 확인
    private bool CheckPlacementValidity(Vector3Int gridPosition, int selectedObjectIndex, Quaternion rotation)
    {
        GridData selectedData = database.objectsData[selectedObjectIndex].ID == 0 ? floorData : furnitureData;

        // 1. GridData를 통한 중복 체크
        if (!selectedData.CanPlaceObjectAt(gridPosition, database.objectsData[selectedObjectIndex].Size, rotation, grid))
        {
            return false;
        }

        // 2. Plane 경계 체크
        List<Vector3Int> positionsToCheck = selectedData.CalculatePosition(gridPosition, database.objectsData[selectedObjectIndex].Size, rotation, grid);
        foreach (Vector3Int pos in positionsToCheck)
        {
            Vector3 worldPos = grid.GetCellCenterWorld(pos);
            if (!planeBounds.Contains(worldPos))
            {
                Debug.Log($"Position {pos} is outside Plane bounds: {worldPos}");
                return false;
            }
        }

        return true;
    }

    /*private bool CheckPlacementValidity(Vector3Int gridPosition, int selectedObjectIndex, Quaternion rotation)
    {
        GridData selectedData = database.objectsData[selectedObjectIndex].ID == 0 ? floorData : furnitureData;
        return selectedData.CanPlaceObjectAt(gridPosition, database.objectsData[selectedObjectIndex].Size, rotation, grid);
    }*/

    /*private bool CheckPlacementValidity(Vector3Int gridPosition, int selectedObjectIndex)
    {
        GridData selectedData = database.objectsData[selectedObjectIndex].ID == 0 
            ? floorData 
            : furnitureData;
        
        return selectedData.CanPlaceObjectAt(gridPosition, database.objectsData[selectedObjectIndex].Size);
    }*/
    #endregion

    #region 건축 종료
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
    #endregion    

    #region 프리뷰 변경
    private void PreviewObjectFunc()
    {
        // 중복건설 체크
        //bool placementValidity = CheckPlacementValidity(gridPosition, selectedObjectIndex);
        bool placementValidity = CheckPlacementValidity(gridPosition, selectedObjectIndex, previewRotation);
        previewRenderer.material.color = placementValidity ? Color.white : Color.red;

        // 미리보기 객체 위치 업데이트
        if (previewObject != null)
        {
            previewObject.transform.position = grid.GetCellCenterWorld(gridPosition);
            previewObject.transform.rotation = previewRotation;
            Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                renderer.material.color = placementValidity ? new Color(1f, 1f, 1f, 0.5f) : new Color(1f, 0f, 0f, 0.5f); // 유효하면 하얀색, 아니면 빨간색
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                previewRotation = Quaternion.Euler(0, previewRotation.eulerAngles.y + 90, 0);
                previewObject.transform.rotation = previewRotation;
            }
        }
    }
    #endregion

    #region 그리드 사이즈 증가
    public void ResizeMesh()
    {
        Vector3 scale = gridVisualization.transform.localScale;
        scale += new Vector3(0.1f, 0.1f, 0.1f); // X, Y, Z 각각 0.5씩 증가
        planeBounds.Expand(new Vector3(1f, 0, 1f));
        gridVisualization.transform.localScale = scale;
    }
    #endregion
}
