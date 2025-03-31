using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPlacer : MonoBehaviour
{
    private List<GameObject> placedGameObjects = new();
    [SerializeField] private InputManager inputManager;
    public int PlaceObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject newObject = Instantiate(prefab);
        newObject.transform.position = position;
        newObject.transform.rotation = rotation;
    
        placedGameObjects.Add(newObject);
        return placedGameObjects.Count - 1;
    }
}
