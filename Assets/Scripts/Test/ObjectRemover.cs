using UnityEngine;
using System;
using ZLinq;

/// <summary>
/// 선택된 오브젝트의 삭제를 담당하는 클래스
/// </summary>
public class ObjectRemover : MonoBehaviour
{
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private ObjectPlacer objectPlacer;
    [SerializeField] private SpawnEffect spawnEffect;
    
    // 삭제 이벤트
    public event Action<GameObject> OnObjectRemoved;
    
    private void Start()
    {
        if (objectPlacer == null)
            objectPlacer = FindFirstObjectByType<ObjectPlacer>();
            
        if (placementSystem == null)
            placementSystem = FindFirstObjectByType<PlacementSystem>();
            
        if (spawnEffect == null)
            spawnEffect = FindFirstObjectByType<SpawnEffect>();
    }
    
    public void RemoveObject(GameObject obj)
    {
        if (obj == null) return;
        
        // 오브젝트 인덱스 찾기
        int objectIndex = objectPlacer.GetObjectIndex(obj);
        if (objectIndex < 0)
        {
            Debug.LogWarning("삭제할 수 없는 오브젝트입니다.");
            return;
        }
        
        // 적절한 GridData 선택
        GridData gridData = FindGridDataByObjectIndex(objectIndex);
        if (gridData == null)
        {
            Debug.LogWarning("해당 오브젝트의 GridData를 찾을 수 없습니다.");
            return;
        }
        
        // 오브젝트 ID 찾기
        int objectID = FindObjectID(gridData, objectIndex);
        if (objectID < 0)
        {
            Debug.LogWarning("GridData에서 오브젝트 ID를 찾을 수 없습니다.");
            return;
        }
        
        // ObjectData 조회
        ObjectData objectData = placementSystem.database.GetObjectData(objectID);
        if (objectData == null)
        {
            Debug.LogWarning($"ID {objectID}에 해당하는 ObjectData를 찾을 수 없습니다.");
            return;
        }
        
        // 이펙트 생성 위치 저장
        Vector3 objectPosition = obj.transform.position;
        
        // GridData에서 데이터 제거
        if (gridData.RemoveObjectByIndex(objectIndex))
        {
            // ObjectPlacer에서 오브젝트 제거
            objectPlacer.RemoveObject(objectIndex);
            
            // 비용 환불
            PlayerWallet.Instance.AddMoney(objectData.BuildPrice);
            
            // 이펙트 재생
            if (spawnEffect != null)
            {
                spawnEffect.OnBuildingPlaced(objectPosition);
            }
            
            // 이벤트 발생
            OnObjectRemoved?.Invoke(obj);
            
            Debug.Log($"오브젝트 삭제 완료: 인덱스 {objectIndex}");
        }
        else
        {
            Debug.LogWarning("GridData에서 오브젝트 데이터를 제거하지 못했습니다.");
        }
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
