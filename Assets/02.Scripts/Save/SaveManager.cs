using JY;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class SaveData
{
    public int playerMoney;
    public GridDataSave floorData;
    public GridDataSave furnitureData;
    public GridDataSave wallData;
    public GridDataSave decoData;
    public List<PaymentSystem.PaymentInfo> paymentQueue;
    public int currentReputation;
    public float currentTime;
    public int currentDay;
    public int currentPurchaseLevel;
    public bool floorLock;
}

[System.Serializable]
public class GridDataSave
{
    public List<GridEntry> placedObjects;
}

[System.Serializable]
public class GridEntry
{
    public Vector3Int key;
    public List<PlacementData> value;
}

public class SaveManager : MonoBehaviour
{
    private SaveData loadedSaveData;
    private string savePath;
    public static SaveManager Instance { get; private set; }

    private void Awake()
    {
        // 세이브 파일 경로 설정 (예: Application.persistentDataPath)
        savePath = Path.Combine(Application.persistentDataPath, "saveData.json");
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.P)) 
        {
            SaveGame();
        }
    }

    public async void SaveGame()
    {
        SaveData saveData = new SaveData
        {
            playerMoney = PlayerWallet.Instance.money,
            currentPurchaseLevel = PlacementSystem.Instance.currentPurchaseLevel,
            floorLock = PlacementSystem.Instance.GetFloorLock(),
            floorData = ConvertGridData(PlacementSystem.Instance.floorData),
            furnitureData = ConvertGridData(PlacementSystem.Instance.furnitureData),
            wallData = ConvertGridData(PlacementSystem.Instance.wallData),
            decoData = ConvertGridData(PlacementSystem.Instance.decoData),
            paymentQueue = PaymentSystem.Instance.paymentQueue,
            currentReputation = ReputationSystem.Instance.CurrentReputation,
            currentTime = TimeSystem.Instance.currentTime,
            currentDay = TimeSystem.Instance.CurrentDay
        };

        try
        {
            string json = JsonUtility.ToJson(saveData, true);
            await File.WriteAllTextAsync(savePath, json);
            Debug.Log($"게임 저장 완료: {savePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"게임 저장 오류: {e.Message}");
        }
    }

    public async Task LoadGame()
    {
        if (!File.Exists(savePath))
        {
            Debug.LogWarning($"세이브 파일이 존재하지 않습니다: {savePath}");
            await LoadMainScene();
            return;
        }

        try
        {
            string json = await File.ReadAllTextAsync(savePath);
            loadedSaveData = JsonUtility.FromJson<SaveData>(json);
            if (loadedSaveData == null)
            {
                Debug.LogError("세이브 데이터 역직렬화 실패");
                await LoadMainScene();
                return;
            }

            await LoadMainScene();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"세이브 파일 로드 오류: {e.Message}");
            await LoadMainScene();
        }
    }

    private async Task LoadMainScene()
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("MainScene");
        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f)
        {
            await Task.Yield();
        }

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            await Task.Yield();
        }

        await Task.Delay(100);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainScene" && loadedSaveData != null)
        {
            StartCoroutine(WaitAndRestoreData());
        }
    }

    private IEnumerator WaitAndRestoreData()
    {
        // 시스템 초기화 대기
        yield return new WaitUntil(() => AreAllSystemsInitialized());
        yield return new WaitForEndOfFrame(); // 추가 프레임 대기

        if (!AreAllSystemsInitialized())
        {
            Debug.LogError("시스템 초기화 실패: 하나 이상의 인스턴스가 null입니다.");
            loadedSaveData = null;
            yield break;
        }

        RestoreGameData();
    }

    private bool AreAllSystemsInitialized()
    {
        bool initialized = PlayerWallet.Instance != null &&
                          PlacementSystem.Instance != null &&
                          PaymentSystem.Instance != null &&
                          ReputationSystem.Instance != null &&
                          TimeSystem.Instance != null &&
                          ObjectPlacer.Instance != null;

        if (!initialized)
        {
            Debug.LogWarning($"시스템 초기화 상태: " +
                             $"PlayerWallet: {PlayerWallet.Instance != null}, " +
                             $"PlacementSystem: {PlacementSystem.Instance != null}, " +
                             $"PaymentSystem: {PaymentSystem.Instance != null}, " +
                             $"ReputationSystem: {ReputationSystem.Instance != null}, " +
                             $"TimeSystem: {TimeSystem.Instance != null}, " +
                             $"ObjectPlacer: {ObjectPlacer.Instance != null}");
        }
        else
        {
            Debug.Log("초기화 완료");
        }

        return initialized;
    }

    private void RestoreGameData()
    {
        try
        {
            // 데이터 복원
            if (PlayerWallet.Instance == null) throw new System.Exception("PlayerWallet.Instance is null");
            PlayerWallet.Instance.money = loadedSaveData.playerMoney;

            if (PlacementSystem.Instance == null) throw new System.Exception("PlacementSystem.Instance is null");
            PlacementSystem.Instance.currentPurchaseLevel = loadedSaveData.currentPurchaseLevel;
            PlacementSystem.Instance.FloorLock = loadedSaveData.floorLock;

            ClearPlacedObjects();

            LoadGridData(PlacementSystem.Instance.floorData, loadedSaveData.floorData);
            LoadGridData(PlacementSystem.Instance.furnitureData, loadedSaveData.furnitureData);
            LoadGridData(PlacementSystem.Instance.wallData, loadedSaveData.wallData);
            LoadGridData(PlacementSystem.Instance.decoData, loadedSaveData.decoData);

            if (PaymentSystem.Instance == null) throw new System.Exception("PaymentSystem.Instance is null");
            PaymentSystem.Instance.paymentQueue = loadedSaveData.paymentQueue;

            if (ReputationSystem.Instance == null) throw new System.Exception("ReputationSystem.Instance is null");
            ReputationSystem.Instance.SetReputation(loadedSaveData.currentReputation);

            if (TimeSystem.Instance == null) throw new System.Exception("TimeSystem.Instance is null");
            TimeSystem.Instance.SetDateTime(loadedSaveData.currentDay, (int)(loadedSaveData.currentTime / 3600), (int)((loadedSaveData.currentTime % 3600) / 60));

            PlacementSystem.Instance.UpdateGridBounds();
            PlacementSystem.Instance.ActivatePlanesByLevel(loadedSaveData.currentPurchaseLevel);

            Debug.Log("게임 데이터 복원 완료");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"데이터 복원 중 오류 발생: {e.Message}\nStackTrace: {e.StackTrace}");
        }
        finally
        {
            loadedSaveData = null;
        }
    }

    private GridDataSave ConvertGridData(GridData gridData)
    {
        if (gridData == null) return null;

        GridDataSave saveData = new GridDataSave { placedObjects = new List<GridEntry>() };

        foreach (var kvp in gridData.placedObjects)
        {
            saveData.placedObjects.Add(new GridEntry { key = kvp.Key, value = kvp.Value });
        }

        return saveData;
    }

    private void LoadGridData(GridData gridData, GridDataSave saveData)
    {
        if (gridData == null || saveData == null || saveData.placedObjects == null)
        {
            Debug.LogError("필수 데이터가 null입니다.");
            return;
        }

        gridData.placedObjects = new Dictionary<Vector3Int, List<PlacementData>>();
        //var processedKeys = new HashSet<Vector3Int>(); // 중복 키 추적
        var processedObjects = new HashSet<int>();

        foreach (var entry in saveData.placedObjects)
        {
            if (entry.value == null || entry.value.Count == 0)
            {
                Debug.LogWarning($"GridEntry의 value가 null 또는 비어 있습니다. key: {entry.key}");
                continue;
            }

            // 이미 처리된 키인지 확인 (Size를 고려한 인접 키도 체크)
            /*bool isDuplicate = false;
            foreach (var processedKey in processedKeys)
            {
                if (Vector3Int.Distance(entry.key, processedKey) < 2) // 인접 칸 체크 (Size에 따라 조정 가능)
                {
                    isDuplicate = true;
                    break;
                }
            }*/

            /*if (isDuplicate)
            {
                Debug.LogWarning($"중복 또는 인접 키 발견, 무시: {entry.key}");
                continue;
            }*/

            var placementData = entry.value[0]; // 첫 번째 PlacementData를 기준으로
            if (processedObjects.Contains(placementData.PlacedObjectIndex))
            {
                continue; // 이미 처리된 객체 무시
            }

            ObjectData objectData = PlacementSystem.Instance.GetDatabase().GetObjectData(placementData.ID);
            if (objectData != null)
            {
                Vector3 worldPosition = PlacementSystem.Instance.grid.GetCellCenterWorld(entry.key);
                Debug.Log($"배치 시도 - key: {entry.key}, ID: {placementData.ID}, Position: {worldPosition}, Rotation: {placementData.Rotation}, Size: {objectData.Size}");
                int index = ObjectPlacer.Instance.PlaceObject(objectData.Prefab, worldPosition, placementData.Rotation);
                if (index != -1)
                {
                    placementData.PlacedObjectIndex = index;
                    // Size에 따라 점유 칸 복원
                    List<Vector3Int> occupiedPositions = PlacementSystem.Instance.floorData.CalculatePosition(entry.key, objectData.Size, placementData.Rotation, PlacementSystem.Instance.grid);
                    PlacementData dataToAdd = new PlacementData(occupiedPositions, placementData.ID, index, placementData.KindIndex, placementData.Rotation);
                    foreach (var pos in occupiedPositions)
                    {
                        if (!gridData.placedObjects.ContainsKey(pos))
                        {
                            gridData.placedObjects[pos] = new List<PlacementData>();
                        }
                        gridData.placedObjects[pos].Add(dataToAdd);
                        /*if (!processedKeys.Contains(pos))
                        {
                            if (!gridData.placedObjects.ContainsKey(pos))
                            {
                                gridData.placedObjects[pos] = new List<PlacementData>();
                            }
                            gridData.placedObjects[pos].Add(dataToAdd);
                            processedKeys.Add(pos);
                        }*/
                    }
                    processedObjects.Add(index);
                    Debug.Log($"로드 성공 - Index: {index}, Occupied Positions: {string.Join(", ", occupiedPositions)}");
                }
                else
                {
                    Debug.LogError($"로드 실패 - key: {entry.key}, ID: {placementData.ID}");
                }
            }
            gridData.placedObjects[entry.key] = new List<PlacementData> { placementData }; // 단일 PlacementData만 저장
        }
    }

    private void ClearPlacedObjects()
    {
        if (ObjectPlacer.Instance != null)
        {
            for (int i = 0; i < ObjectPlacer.Instance.placedGameObjects.Count; i++)
            {
                if (ObjectPlacer.Instance.placedGameObjects[i] != null)
                {
                    ObjectPlacer.Instance.RemoveObject(i);
                }
            }
            ObjectPlacer.Instance.placedGameObjects.Clear();
        }
        else
        {
            Debug.LogError("ObjectPlacer.Instance가 null입니다.");
        }

        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.floorData.placedObjects.Clear();
            PlacementSystem.Instance.furnitureData.placedObjects.Clear();
            PlacementSystem.Instance.wallData.placedObjects.Clear();
            PlacementSystem.Instance.decoData.placedObjects.Clear();
        }
        else
        {
            Debug.LogError("PlacementSystem.Instance가 null입니다.");
        }
    }

    private void SetPrivateField<T>(object obj, string fieldName, T value)
    {
        if (obj == null)
        {
            Debug.LogError($"SetPrivateField: 대상 객체가 null입니다. 필드: {fieldName}");
            return;
        }

        var field = obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(obj, value);
            Debug.Log($"필드 {fieldName} 설정 완료: {value}");
        }
        else
        {
            Debug.LogError($"{fieldName}를 {obj.GetType().Name}에서 찾을 수 없습니다.");
        }
    }
}