using UnityEngine;
using System;
using ZLinq;

/// <summary>
/// 선택된 오브젝트의 이동을 담당하는 클래스
/// </summary>
public class ObjectMover : MonoBehaviour
{
    [SerializeField] private InputManager inputManager;
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private Grid grid;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask placementLayerMask;
    
    private GameObject targetObject;
    private bool isMoving = false;
    private Vector3 originalPosition;
    private Vector3Int originalGridPosition;
    
    // 이동 시작/종료 이벤트
    public event Action<GameObject> OnMoveStarted;
    public event Action<GameObject, Vector3> OnMoveCompleted;
    
    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }
    
    private void Update()
    {
        if (isMoving && targetObject != null)
        {
            // 마우스 위치에 따라 오브젝트 이동
            MoveObjectWithMouse();
            
            // 엔터 키나 마우스 클릭으로 이동 확정
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0) && !inputManager.IsPointerOverUI())
            {
                FinishMoving();
            }
            
            // ESC 키로 이동 취소
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelMoving();
            }
        }
    }
    
    public void StartMoving(GameObject obj)
    {
        if (obj == null) return;
        
        targetObject = obj;
        originalPosition = obj.transform.position;
        originalGridPosition = grid.WorldToCell(originalPosition);
        isMoving = true;
        
        // 하이라이트 효과 등 추가 가능
        OnMoveStarted?.Invoke(targetObject);
        
        Debug.Log($"{targetObject.name} 이동 시작");
    }
    
    private void MoveObjectWithMouse()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, 100f, placementLayerMask))
        {
            // 그리드에 맞춰 위치 조정
            Vector3Int gridPosition = grid.WorldToCell(hit.point);
            Vector3 worldPosition = grid.GetCellCenterWorld(gridPosition);
            
            // 오브젝트 이동
            targetObject.transform.position = worldPosition;
            
            // 배치 가능 여부 시각적 표시 (선택 사항)
            bool canPlace = CheckPlacementValidity(gridPosition);
            VisualizeValidity(canPlace);
        }
    }
    
    public void FinishMoving()
    {
        if (!isMoving || targetObject == null) return;
        
        // 현재 그리드 위치
        Vector3Int newGridPosition = grid.WorldToCell(targetObject.transform.position);
        
        // 배치 가능 여부 확인
        if (CheckPlacementValidity(newGridPosition))
        {
            // GridData 업데이트
            UpdateGridData(newGridPosition);
            
            isMoving = false;
            OnMoveCompleted?.Invoke(targetObject, targetObject.transform.position);
            
            Debug.Log($"{targetObject.name} 이동 완료: {newGridPosition}");
        }
        else
        {
            // 배치 불가능한 위치면 원래 위치로 복귀
            Debug.Log("이동 불가능한 위치입니다. 원래 위치로 복귀합니다.");
            CancelMoving();
        }
        
        targetObject = null;
    }
    
    public void CancelMoving()
    {
        if (!isMoving || targetObject == null) return;
        
        // 원래 위치로 복원
        targetObject.transform.position = originalPosition;
        
        isMoving = false;
        Debug.Log($"{targetObject.name} 이동 취소");
        targetObject = null;
    }
    
    private bool CheckPlacementValidity(Vector3Int gridPosition)
    {
        // 오브젝트 인덱스 찾기
        int objectIndex = FindObjectIndex(targetObject);
        if (objectIndex < 0) return false;
        
        // 해당 오브젝트의 GridData 찾기
        GridData gridData = FindGridDataByObjectIndex(objectIndex);
        if (gridData == null) return false;
        
        // 오브젝트 ID 찾기
        int objectID = FindObjectID(gridData, objectIndex);
        if (objectID < 0) return false;
        
        // 오브젝트 데이터 가져오기
        ObjectData objectData = placementSystem.database.GetObjectData(objectID);
        if (objectData == null) return false;
        
        // 임시로 기존 데이터 제거 (검사만을 위해)
        gridData.RemoveObjectByIndex(objectIndex);
        
        // 새 위치에 배치 가능한지 확인
        bool canPlace = placementSystem.CheckPlacementValidity(
            gridPosition, 
            placementSystem.database.objectsData.FindIndex(data => data.ID == objectID),
            targetObject.transform.rotation
        );
        
        // 원래 데이터 복원
        gridData.AddObjectAt(
            originalGridPosition,
            objectData.Size,
            objectData.ID,
            objectIndex,
            objectData.kindIndex,
            targetObject.transform.rotation,
            grid,
            objectData.IsWall
        );
        
        return canPlace;
    }
    
    private void UpdateGridData(Vector3Int newGridPosition)
    {
        // 오브젝트 인덱스 찾기
        int objectIndex = FindObjectIndex(targetObject);
        if (objectIndex < 0) return;
        
        // 해당 오브젝트의 GridData 찾기
        GridData gridData = FindGridDataByObjectIndex(objectIndex);
        if (gridData == null) return;
        
        // 오브젝트 ID 찾기
        int objectID = FindObjectID(gridData, objectIndex);
        if (objectID < 0) return;
        
        // 오브젝트 데이터 가져오기
        ObjectData objectData = placementSystem.database.GetObjectData(objectID);
        if (objectData == null) return;
        
        // 기존 데이터 제거
        gridData.RemoveObjectByIndex(objectIndex);
        
        // 새 위치로 데이터 추가
        gridData.AddObjectAt(
            newGridPosition,
            objectData.Size,
            objectData.ID,
            objectIndex,
            objectData.kindIndex,
            targetObject.transform.rotation,
            grid,
            objectData.IsWall
        );
    }
    
    private void VisualizeValidity(bool isValid)
    {
        // 오브젝트의 모든 렌더러 컴포넌트 가져오기
        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>();
        
        // 배치 가능 여부에 따라 색상 변경
        Color color = isValid ? new Color(0.5f, 1f, 0.5f, 0.7f) : new Color(1f, 0.5f, 0.5f, 0.7f);
        
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].color = color;
            }
            renderer.materials = materials;
        }
    }
    
    private int FindObjectIndex(GameObject obj)
    {
        // ObjectPlacer에서 오브젝트 인덱스 찾기
        ObjectPlacer objectPlacer = FindFirstObjectByType<ObjectPlacer>();
        if (objectPlacer != null)
        {
            return objectPlacer.GetObjectIndex(obj);
        }
        return -1;
    }
    
    private GridData FindGridDataByObjectIndex(int objectIndex)
    {
        // 각 GridData에서 오브젝트 인덱스 검색
        if (placementSystem.floorData.placedObjects.AsValueEnumerable().Any(kvp => kvp.Value.AsValueEnumerable().Any(data => data.PlacedObjectIndex == objectIndex)))
            return placementSystem.floorData;
            
        if (placementSystem.furnitureData.placedObjects.AsValueEnumerable().Any(kvp => kvp.Value.AsValueEnumerable().Any(data => data.PlacedObjectIndex == objectIndex)))
            return placementSystem.furnitureData;
            
        if (placementSystem.wallData.placedObjects.AsValueEnumerable().Any(kvp => kvp.Value.AsValueEnumerable().Any(data => data.PlacedObjectIndex == objectIndex)))
            return placementSystem.wallData;
            
        if (placementSystem.decoData.placedObjects.AsValueEnumerable().Any(kvp => kvp.Value.AsValueEnumerable().Any(data => data.PlacedObjectIndex == objectIndex)))
            return placementSystem.decoData;
            
        return null;
    }
    
    private int FindObjectID(GridData gridData, int objectIndex)
    {
        // GridData에서 오브젝트 ID 찾기
        foreach (var kvp in gridData.placedObjects)
        {
            foreach (var data in kvp.Value)
            {
                if (data.PlacedObjectIndex == objectIndex)
                {
                    return data.ID;
                }
            }
        }
        return -1;
    }
}
