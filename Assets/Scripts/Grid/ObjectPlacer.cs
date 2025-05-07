using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPlacer : MonoBehaviour
{
    [SerializeField] private List<GameObject> placedGameObjects = new();
    [SerializeField] private InputManager inputManager;
    [SerializeField] private ChangeFloorSystem changeFloorSystem;
    /// <summary>
    /// 매개 변수의 오브젝트들을 배치한다.
    /// </summary> 
    /// <param name="prefab"></param>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <returns></returns>
    public int PlaceObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject newObject = Instantiate(prefab); //, BatchedObj.transform, true);
        newObject.transform.position = position;
        newObject.transform.rotation = rotation;
        //newObject.isStatic = true;
        SoundManager.PlaySound(SoundType.Build, 0.1f); 
        
        // 현재 층에 따라 레이어 설정
        int currentFloor = changeFloorSystem.currentFloor;
        string layerName = $"{currentFloor}F"; // 예: "1F", "2F", "3F", "4F"
        int layer = LayerMask.NameToLayer(layerName);
        if (layer == -1)
        {
            Debug.LogError($"Layer {layerName} not found!");
        }
        else
        {
            Transform firstChild = newObject.transform.GetChild(0);
            if (firstChild != null)
            {
                firstChild.gameObject.layer = layer;
            }
            else
            {
                Debug.LogWarning("No child object found to set layer!");
            }
        }
        
        placedGameObjects.Add(newObject);
        return placedGameObjects.Count - 1;
    }

    /// <summary>
    /// 오브젝트들을 삭제한다.
    /// </summary>
    /// <param name="index"></param>
    public void RemoveObject(int index)
    {
        if (index >= 0 && index < placedGameObjects.Count)
        {
            GameObject obj = placedGameObjects[index];
            if (obj != null)
            {
                Destroy(obj);
            }
            placedGameObjects[index] = null; // 참조 제거 (선택적으로 리스트에서 완전히 제거 가능)
        }
    }

    /// <summary>
    /// 오브젝트의 인덱스를 추출한다.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public int GetObjectIndex(GameObject obj)
    {
        return placedGameObjects.IndexOf(obj);
    }
}
