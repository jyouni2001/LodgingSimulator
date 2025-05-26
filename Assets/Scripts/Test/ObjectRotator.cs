using UnityEngine;
using System;
using ZLinq;

/// <summary>
/// 선택된 오브젝트의 회전을 담당하는 클래스
/// </summary>
public class ObjectRotator : MonoBehaviour
{
    [SerializeField] private InputManager inputManager;
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private Grid grid;
    
    private GameObject targetObject;
    private bool isRotating = false;
    private Quaternion originalRotation;
    private Quaternion targetRotation;
    private float rotationSpeed = 5f;
    
    // 회전 시작/종료 이벤트
    public event Action<GameObject> OnRotationStarted;
    public event Action<GameObject, Quaternion> OnRotationCompleted;
    
    private void Update()
    {
        if (isRotating && targetObject != null)
        {
            // R 키로 90도씩 회전
            if (Input.GetKeyDown(KeyCode.R))
            {
                targetRotation = Quaternion.Euler(0, targetRotation.eulerAngles.y + 90, 0);
            }
            
            // 부드러운 회전 적용
            targetObject.transform.rotation = Quaternion.Slerp(
                targetObject.transform.rotation, 
                targetRotation, 
                Time.deltaTime * rotationSpeed
            );
            
            // 엔터 키나 마우스 클릭으로 회전 확정
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0) && !inputManager.IsPointerOverUI())
            {
                FinishRotation();
            }
            
            // ESC 키로 회전 취소
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelRotation();
            }
        }
    }
    
    public void StartRotating(GameObject obj)
    {
        if (obj == null) return;
        
        targetObject = obj;
        originalRotation = obj.transform.rotation;
        targetRotation = originalRotation;
        isRotating = true;
        
        // 하이라이트 효과 등 추가 가능
        OnRotationStarted?.Invoke(targetObject);
        
        Debug.Log($"{targetObject.name} 회전 시작");
    }
    
    public void FinishRotation()
    {
        if (!isRotating || targetObject == null) return;
        
        // 회전 각도를 90도 단위로 맞춤
        float yAngle = Mathf.Round(targetObject.transform.eulerAngles.y / 90) * 90;
        targetRotation = Quaternion.Euler(0, yAngle, 0);
        targetObject.transform.rotation = targetRotation;
        
        // GridData 업데이트
        UpdateGridData();
        
        isRotating = false;
        OnRotationCompleted?.Invoke(targetObject, targetRotation);
        
        Debug.Log($"{targetObject.name} 회전 완료: {yAngle}도");
        targetObject = null;
    }
    
    public void CancelRotation()
    {
        if (!isRotating || targetObject == null) return;
        
        // 원래 회전으로 복원
        targetObject.transform.rotation = originalRotation;
        
        isRotating = false;
        Debug.Log($"{targetObject.name} 회전 취소");
        targetObject = null;
    }
    
    private void UpdateGridData()
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
        
        // 새 위치 및 회전으로 데이터 추가
        Vector3Int gridPosition = grid.WorldToCell(targetObject.transform.position);
        
        gridData.AddObjectAt(
            gridPosition,
            objectData.Size,
            objectData.ID,
            objectIndex,
            objectData.kindIndex,
            targetRotation,
            grid,
            objectData.IsWall
        );
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
