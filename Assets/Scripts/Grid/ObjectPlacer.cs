using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPlacer : MonoBehaviour
{
    private List<GameObject> placedGameObjects = new();
    [SerializeField] private InputManager inputManager;
    public int PlaceObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        Transform parentobj = inputManager.hit2.transform;
    
        if (parentobj == null)
        {
            Debug.LogWarning("parentobj가 null입니다. 월드에 배치됩니다.");
        }
        else
        {
            Debug.Log($"부모 오브젝트: {parentobj.name}");
        }

        GameObject newObject = Instantiate(prefab);
        newObject.transform.position = position;
        newObject.transform.rotation = rotation;
        newObject.transform.SetParent(parentobj);
    
        placedGameObjects.Add(newObject);
        return placedGameObjects.Count - 1;
    }
}
