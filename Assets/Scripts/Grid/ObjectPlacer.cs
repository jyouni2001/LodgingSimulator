using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPlacer : MonoBehaviour
{
    private List<GameObject> placedGameObjects = new();
    [SerializeField] private InputManager inputManager;

    /// <summary>
    /// 매개 변수의 오브젝트들을 배치한다.
    /// </summary> 
    /// <param name="prefab"></param>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <returns></returns>
    public int PlaceObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject newObject = Instantiate(prefab);
        newObject.transform.position = position;
        newObject.transform.rotation = rotation;
        newObject.isStatic = true;
    
        placedGameObjects.Add(newObject);
        return placedGameObjects.Count - 1;
    }
}
