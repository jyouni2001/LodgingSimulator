using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZLinq;

[CustomEditor(typeof(ObjectsDatabaseSO))]
public class ObjectDatabaseEditor : Editor
{
    private ObjectsDatabaseSO database;
    private Vector2 scrollPos;
    private int selectedTab = 0;
    private int newObjectKindIndex = 0;
    private string newObjectName = "";
    private Vector2Int newObjectSize = Vector2Int.one;
    private GameObject newObjectPrefab = null;
    private int newObjectBuildPrice = 0;
    private int newObjectBasePrice = 0;
    private bool newObjectIsWall = false;
    private Dictionary<ObjectData, bool> foldoutStates = new Dictionary<ObjectData, bool>();

    private void OnEnable()
    {
        database = (ObjectsDatabaseSO)target;
        database.InitializeDictionary();
        foreach (var obj in database.objectsData)
        {
            if (!foldoutStates.ContainsKey(obj)) foldoutStates[obj] = false;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Tab selection
        string[] tabNames = {"바닥", "가구", "벽", "장식품"};
        selectedTab = GUILayout.SelectionGrid(selectedTab, tabNames, tabNames.Length);

        // Add new object section
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("데이터", EditorStyles.boldLabel);
        newObjectName = EditorGUILayout.TextField("이름", newObjectName);
        EditorGUILayout.LabelField("종류", tabNames[selectedTab]);
        newObjectSize = EditorGUILayout.Vector2IntField("크기", newObjectSize);
        newObjectPrefab = (GameObject)EditorGUILayout.ObjectField("프리팹", newObjectPrefab, typeof(GameObject), false);
        newObjectBuildPrice = EditorGUILayout.IntField("건축 가격", newObjectBuildPrice);
        newObjectBasePrice = EditorGUILayout.IntField("기본 가격", newObjectBasePrice);
        newObjectIsWall = EditorGUILayout.Toggle("벽 or Not", newObjectIsWall);

        if (GUILayout.Button("데이터 추가"))
        {
            var newObject = new ObjectData
            {
                Name = newObjectName,
                ID = database.objectsData.Count > 0 ? database.objectsData.AsValueEnumerable().Max(o => o.ID) + 1 : 0,
                kindIndex = selectedTab,
                Size = newObjectSize,
                Prefab = newObjectPrefab,
                BuildPrice = newObjectBuildPrice,
                BasePrice = newObjectBasePrice,
                IsWall = newObjectIsWall
            };
            database.objectsData.Add(newObject);
            foldoutStates[newObject] = false;
            database.InitializeDictionary();
            EditorUtility.SetDirty(database);

            newObjectName = "";
            newObjectSize = Vector2Int.one;
            newObjectPrefab = null;
            newObjectBuildPrice = 0;
            newObjectBasePrice = 0;
            newObjectIsWall = false;
        }

        // Scroll view for objects
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // Filter and sort objects by kindIndex and ID
        var filteredObjects = database.objectsData.AsValueEnumerable()
            .Where(obj => obj.kindIndex == selectedTab)
            .OrderBy(obj => obj.ID)
            .ToList();

        foreach (var obj in filteredObjects)
        {
            foldoutStates.TryGetValue(obj, out bool isFolded);
            isFolded = EditorGUILayout.Foldout(isFolded, $"ID: {obj.ID} - {obj.Name}", true, EditorStyles.foldoutHeader);
            foldoutStates[obj] = isFolded;

            if (isFolded)
            {
                EditorGUILayout.BeginVertical("box");
                obj.Size = EditorGUILayout.Vector2IntField("크기", obj.Size);
                obj.Prefab = (GameObject)EditorGUILayout.ObjectField("프리팹", obj.Prefab, typeof(GameObject), false);
                obj.BuildPrice = EditorGUILayout.IntField("건축 가격", obj.BuildPrice);
                obj.BasePrice = EditorGUILayout.IntField("기본 가격", obj.BasePrice);
                obj.IsWall = EditorGUILayout.Toggle("벽 or Not", obj.IsWall);

                if (GUILayout.Button("Delete"))
                {
                    database.objectsData.Remove(obj);
                    foldoutStates.Remove(obj);
                    EditorUtility.SetDirty(database);
                    break;
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space();
        }
        EditorGUILayout.EndScrollView();

        serializedObject.ApplyModifiedProperties();

    }
}