using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System.Linq;
using DG.Tweening;

public class GridSystem : MonoBehaviour
{
    public GameObject plane;
    public Material lineMaterial;
    private bool[,] grid;
    public float lineWidth = 0.01f;
    public float cellSize = 1f;

    public Camera cam;
    public GameObject PreviewObject;
    public GameObject objectToPlace;
    private HashSet<Vector3> occupiedPositions = new HashSet<Vector3>();

    public Vector3 gridStartPosition;

    private LineRenderer raycastLineRenderer;
    public GameObject raycastHitMarker;
    public float time = 0f;

    private bool logging = false;

    private Dictionary<int, List<Vector2Int>> unavailableZones;
    private int zoneIdCounter = 0;
    public int playerMoney = 1000;

    private GameObject[,] cellOverlays;
    public Material availableMaterial;
    public Material unavailableMaterial;
    private List<GameObject> zoneBoundaryLines = new List<GameObject>();
    private Dictionary<int, Sequence> zoneAnimations = new Dictionary<int, Sequence>();

    private bool isBuildMode = false;
    private int highlightedZoneId = -1;

    void Start()
    {
        InitializeGrid();
        SetupInitialZones();
        DrawGrid(gridStartPosition, grid.GetLength(0), grid.GetLength(1), cellSize);
        UpdateGridVisuals();

        /*GameObject raycastObj = new GameObject("RaycastLine");
        raycastObj.transform.parent = transform;*/
        /*raycastLineRenderer = raycastObj.AddComponent<LineRenderer>();
        raycastLineRenderer.startWidth = 0.05f;
        raycastLineRenderer.endWidth = 0.05f;
        raycastLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        raycastLineRenderer.startColor = Color.yellow;
        raycastLineRenderer.endColor = Color.yellow;*/

        /*if (raycastHitMarker == null)
        {
            raycastHitMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            raycastHitMarker.GetComponent<Collider>().enabled = false;
            raycastHitMarker.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            Material markerMaterial = new Material(Shader.Find("Sprites/Default"));
            markerMaterial.color = Color.red;
            raycastHitMarker.GetComponent<Renderer>().material = markerMaterial;
            raycastHitMarker.SetActive(false);
        }*/

        CreatePreviewObject();
        PreviewObject.SetActive(false);

        DOTween.Init();
    }

    void OnDestroy()
    {
        foreach (var seq in zoneAnimations.Values)
        {
            seq.Kill();
        }
        zoneAnimations.Clear();

        foreach (var line in zoneBoundaryLines)
        {
            if (line != null) Destroy(line);
        }
        zoneBoundaryLines.Clear();
    }

    void InitializeGrid()
    {
        MeshFilter meshFilter = plane.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            Mesh mesh = meshFilter.mesh;
            Vector3 planeSize = mesh.bounds.size;
            Vector3 scale = plane.transform.localScale;

            int gridWidth = Mathf.FloorToInt(planeSize.x * scale.x / cellSize);
            int gridHeight = Mathf.FloorToInt(planeSize.z * scale.z / cellSize);

            grid = new bool[gridWidth, gridHeight];
            unavailableZones = new Dictionary<int, List<Vector2Int>>();
            cellOverlays = new GameObject[gridWidth, gridHeight];

            Vector3 pivotOffset = new Vector3(-planeSize.x * scale.x / 2, 0, -planeSize.z * scale.z / 2);
            gridStartPosition = plane.transform.position + pivotOffset;

            Debug.Log($"Grid initialized: {gridWidth}x{gridHeight}");
        }
    }

    void SetupInitialZones()
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = false;
            }
        }

        int usableWidth = Mathf.FloorToInt(width * 0.2f);
        int usableHeight = Mathf.FloorToInt(height * 0.2f);
        int startX = (width - usableWidth) / 2;
        int startY = (height - usableHeight) / 2;

        for (int x = startX; x < startX + usableWidth; x++)
        {
            for (int y = startY; y < startY + usableHeight; y++)
            {
                grid[x, y] = true;
            }
        }

        int zoneSize = 5;
        for (int x = 0; x < width; x += zoneSize)
        {
            for (int y = 0; y < height; y += zoneSize)
            {
                if (!grid[x, y])
                {
                    List<Vector2Int> zoneCells = new List<Vector2Int>();
                    for (int i = 0; i < zoneSize && x + i < width; i++)
                    {
                        for (int j = 0; j < zoneSize && y + j < height; j++)
                        {
                            if (!grid[x + i, y + j])
                            {
                                zoneCells.Add(new Vector2Int(x + i, y + j));
                            }
                        }
                    }
                    if (zoneCells.Count > 0)
                    {
                        unavailableZones[zoneIdCounter++] = zoneCells;
                    }
                }
            }
        }

        Debug.Log($"Total Unavailable Zones: {unavailableZones.Count}");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            isBuildMode = !isBuildMode;
            PreviewObject.SetActive(isBuildMode);
            ToggleGridVisibility(isBuildMode);
            Debug.Log($"Build Mode: {(isBuildMode ? "ON" : "OFF")}");
        }

        if (isBuildMode)
        {
            UpdateGhostPosition();
            InputSystem();
            HighlightZoneUnderMouse();
        }

        time += Time.deltaTime;
        if (time > 1f)
        {
            logging = true;
            time = 0f;
        }
    }

    private void InputSystem()
{
    if (Input.GetMouseButtonDown(0))
    {
        if (highlightedZoneId != -1)
        {
            List<Vector2Int> unlockedCells = UnlockZone(highlightedZoneId, 500);
            if (isBuildMode && unlockedCells != null)
            {
                Debug.Log($"Playing animation for Zone {highlightedZoneId}");
                PlayPurchaseAnimation(highlightedZoneId, () => UpdateGridVisualsForZone(unlockedCells));
            }
        }
        else
        {
            PlaceObject();
        }
    }
}

    void DrawGrid(Vector3 startPosition, int width, int height, float cellSizeField)
    {
        for (int y = 0; y <= height; y++)
        {
            GameObject lineObj = new GameObject("GridLine_H_" + y);
            lineObj.transform.parent = transform;
            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.material = lineMaterial;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(startPosition.x, 0.001f, startPosition.z + y * cellSize));
            line.SetPosition(1, new Vector3(startPosition.x + width * cellSize, 0.001f, startPosition.z + y * cellSize));
            line.gameObject.SetActive(false);
        }

        for (int x = 0; x <= width; x++)
        {
            GameObject lineObj = new GameObject("GridLine_V_" + x);
            lineObj.transform.parent = transform;
            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.material = lineMaterial;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(startPosition.x + x * cellSize, 0.001f, startPosition.z));
            line.SetPosition(1, new Vector3(startPosition.x + x * cellSize, 0.001f, startPosition.z + height * cellSize));
            line.gameObject.SetActive(false);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
                overlay.transform.parent = transform;
                overlay.transform.position = startPosition + new Vector3(x * cellSize + cellSize / 2, 0.002f, y * cellSize + cellSize / 2);
                overlay.transform.localScale = new Vector3(cellSize, cellSize, 1f);
                overlay.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                overlay.name = $"CellOverlay_{x}_{y}";
                cellOverlays[x, y] = overlay;
                overlay.SetActive(false);
                Destroy(overlay.GetComponent<Collider>());
            }
        }

        UpdateZoneBoundaries();
    }

    void UpdateGridVisuals()
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Renderer rend = cellOverlays[x, y].GetComponent<Renderer>();
                if (grid[x, y])
                {
                    rend.material = availableMaterial;
                }
                else
                {
                    rend.material = unavailableMaterial;
                }
            }
        }
        UpdateZoneBoundaries();
    }

    void UpdateGridVisualsForZone(List<Vector2Int> unlockedCells)
    {
        if (unlockedCells == null) return;

        foreach (Vector2Int cell in unlockedCells)
        {
            Renderer rend = cellOverlays[cell.x, cell.y].GetComponent<Renderer>();
            rend.material = availableMaterial;
        }
        UpdateZoneBoundaries();
    }

    void UpdateZoneBoundaries()
    {
        List<GameObject> linesToRemove = new List<GameObject>();
        foreach (var line in zoneBoundaryLines)
        {
            if (line == null) continue;
            int zoneId = int.Parse(line.name.Replace("ZoneBoundary_", ""));
            if (!unavailableZones.ContainsKey(zoneId) && (!zoneAnimations.ContainsKey(zoneId) || !zoneAnimations[zoneId].IsPlaying()))
            {
                linesToRemove.Add(line);
            }
            else
            {
                LineRenderer lr = line.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.startColor = highlightedZoneId == zoneId ? Color.green : Color.red;
                    lr.endColor = highlightedZoneId == zoneId ? Color.green : Color.red;
                }
            }
        }

        foreach (var line in linesToRemove)
        {
            zoneBoundaryLines.Remove(line);
            Destroy(line);
        }

        foreach (var zone in unavailableZones)
        {
            if (zoneBoundaryLines.Exists(l => l != null && l.name == $"ZoneBoundary_{zone.Key}")) continue;

            List<Vector2Int> cells = zone.Value;
            if (cells.Count == 0) continue;

            int minX = cells[0].x, maxX = cells[0].x;
            int minY = cells[0].y, maxY = cells[0].y;
            foreach (var cell in cells)
            {
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxY = Mathf.Max(maxY, cell.y);
            }

            Vector3 bottomLeft = gridStartPosition + new Vector3(minX * cellSize, 0.003f, minY * cellSize);
            Vector3 topRight = gridStartPosition + new Vector3((maxX + 1) * cellSize, 0.003f, (maxY + 1) * cellSize);

            GameObject boundary = new GameObject($"ZoneBoundary_{zone.Key}");
            boundary.transform.parent = transform;
            LineRenderer line = boundary.AddComponent<LineRenderer>();
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = highlightedZoneId == zone.Key ? Color.green : Color.red;
            line.endColor = highlightedZoneId == zone.Key ? Color.green : Color.red;
            line.startWidth = 0.05f;
            line.endWidth = 0.05f;
            line.positionCount = 5;
            line.SetPosition(0, bottomLeft);
            line.SetPosition(1, new Vector3(topRight.x, 0.003f, bottomLeft.z));
            line.SetPosition(2, topRight);
            line.SetPosition(3, new Vector3(bottomLeft.x, 0.003f, topRight.z));
            line.SetPosition(4, bottomLeft);
            line.gameObject.SetActive(isBuildMode);
            zoneBoundaryLines.Add(boundary);
        }
    }

    void PlayPurchaseAnimation(int zoneId, System.Action onComplete)
    {
        GameObject boundary = zoneBoundaryLines.Find(line => line != null && line.name == $"ZoneBoundary_{zoneId}");
        if (boundary == null || boundary.GetComponent<LineRenderer>() == null) return;

        LineRenderer line = boundary.GetComponent<LineRenderer>();
        if (zoneAnimations.ContainsKey(zoneId))
        {
            zoneAnimations[zoneId].Kill();
            zoneAnimations.Remove(zoneId);
        }

        // 빨간색에서 초록색으로 퍼지는 애니메이션
        Sequence seq = DOTween.Sequence();
        seq.AppendCallback(() => {
            if (line != null)
            {
                line.startColor = Color.red;
                line.endColor = Color.red;
            }
        })
        .Append(line.DOColor(new Color2(Color.red, Color.red), new Color2(Color.green, Color.green), 1f)
            .SetEase(Ease.InOutQuad)) // 부드럽게 퍼지는 효과
        .Join(boundary.transform.DOScale(1.05f, 0.5f).SetEase(Ease.OutQuad)) // 살짝 커짐
        .Append(boundary.transform.DOScale(1f, 0.5f).SetEase(Ease.InQuad)) // 원래 크기로
        .OnComplete(() => {
            if (line != null)
            {
                line.startColor = Color.green; // 최종적으로 초록색 유지
                line.endColor = Color.green;
            }
            zoneAnimations.Remove(zoneId);
            onComplete?.Invoke();
        });

        zoneAnimations[zoneId] = seq;
        seq.Play();
    }

    void ToggleGridVisibility(bool visible)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        for (int x = 0; x <= width; x++)
        {
            transform.Find("GridLine_V_" + x)?.gameObject.SetActive(visible);
        }
        for (int y = 0; y <= height; y++)
        {
            transform.Find("GridLine_H_" + y)?.gameObject.SetActive(visible);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cellOverlays[x, y].SetActive(visible);
            }
        }

        foreach (GameObject line in zoneBoundaryLines)
        {
            if (line != null) line.SetActive(visible);
        }
    }

    void CreatePreviewObject()
    {
        PreviewObject = Instantiate(objectToPlace);
        PreviewObject.GetComponent<Collider>().enabled = false;
        PreviewObject.GetComponent<NavMeshObstacle>().enabled = false;
        Renderer[] renderers = PreviewObject.GetComponentsInChildren<Renderer>();

        foreach (Renderer rend in renderers)
        {
            Material mat = rend.material;
            Color color = mat.color;
            color.a = 0.5f;
            mat.color = color;

            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Mode", 2);
            mat.SetInt("_Blend", 1);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
    }

    void UpdateGhostPosition()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 point = hit.point;
            Vector3 relativePosition = point - gridStartPosition;

            Vector3 snappedRelativePosition = new Vector3(
                Mathf.FloorToInt(relativePosition.x / cellSize) + 0.5f,
                0,
                Mathf.FloorToInt(relativePosition.z / cellSize) + 0.5f
            );
            Vector3 snappedPosition = gridStartPosition + snappedRelativePosition;
            PreviewObject.transform.position = snappedPosition + Vector3.up * 0.5f;

            if (Input.GetKeyDown(KeyCode.R))
            {
                PreviewObject.transform.rotation *= Quaternion.Euler(0, 30, 0);
            }

            int gridX = Mathf.FloorToInt((snappedPosition.x - gridStartPosition.x) / cellSize);
            int gridY = Mathf.FloorToInt((snappedPosition.z - gridStartPosition.z) / cellSize);

            if (gridX >= 0 && gridX < grid.GetLength(0) && gridY >= 0 && gridY < grid.GetLength(1))
            {
                if (!grid[gridX, gridY])
                {
                    SetGhostColor(Color.red);
                }
                else if (occupiedPositions.Contains(snappedPosition))
                {
                    SetGhostColor(Color.yellow);
                }
                else
                {
                    SetGhostColor(Color.blue);
                }
            }
        }
    }

    void PlaceObject()
    {
        Vector3 placementPosition = PreviewObject.transform.position;
        Quaternion placementRotation = PreviewObject.transform.rotation;

        Vector3 relativePosition = placementPosition - gridStartPosition - Vector3.up * 0.5f;
        Vector3 snappedRelativePosition = new Vector3(
            Mathf.FloorToInt(relativePosition.x / cellSize) + 0.5f,
            0,
            Mathf.FloorToInt(relativePosition.z / cellSize) + 0.5f
        );
        Vector3 snappedPosition = gridStartPosition + snappedRelativePosition;

        int gridX = Mathf.FloorToInt((snappedPosition.x - gridStartPosition.x) / cellSize);
        int gridY = Mathf.FloorToInt((snappedPosition.z - gridStartPosition.z) / cellSize);

        if (gridX >= 0 && gridX < grid.GetLength(0) && gridY >= 0 && gridY < grid.GetLength(1))
        {
            if (grid[gridX, gridY] && !occupiedPositions.Contains(snappedPosition))
            {
                Instantiate(objectToPlace, snappedPosition + Vector3.up * 0.5f, placementRotation);
                occupiedPositions.Add(snappedPosition);
            }
            else
            {
                Debug.Log("Cannot place object: Cell is unavailable or occupied!");
            }
        }
    }

    List<Vector2Int> UnlockZone(int zoneId, int cost)
    {
        if (playerMoney >= cost && unavailableZones.ContainsKey(zoneId))
        {
            playerMoney -= cost;
            List<Vector2Int> zoneCells = unavailableZones[zoneId];
            foreach (Vector2Int cell in zoneCells)
            {
                grid[cell.x, cell.y] = true;
            }
            unavailableZones.Remove(zoneId);
            Debug.Log($"Zone {zoneId} unlocked! Remaining money: {playerMoney}");
            return zoneCells;
        }
        else
        {
            Debug.Log("Not enough money or invalid zone!");
            return null;
        }
    }

    void HighlightZoneUnderMouse()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 point = hit.point;
            Vector3 relativePosition = point - gridStartPosition;
            int gridX = Mathf.FloorToInt(relativePosition.x / cellSize);
            int gridY = Mathf.FloorToInt(relativePosition.z / cellSize);

            if (gridX >= 0 && gridX < grid.GetLength(0) && gridY >= 0 && gridY < grid.GetLength(1))
            {
                if (!grid[gridX, gridY])
                {
                    int newHighlightedZoneId = FindZoneIdAt(gridX, gridY);
                    if (newHighlightedZoneId != -1 && IsAdjacentToAvailable(newHighlightedZoneId))
                    {
                        if (highlightedZoneId != newHighlightedZoneId)
                        {
                            highlightedZoneId = newHighlightedZoneId;
                            UpdateZoneBoundaries();
                        }
                    }
                    else
                    {
                        highlightedZoneId = -1;
                        UpdateZoneBoundaries();
                    }
                }
                else
                {
                    highlightedZoneId = -1;
                    UpdateZoneBoundaries();
                }
            }
        }
    }

    int FindZoneIdAt(int x, int y)
    {
        foreach (var zone in unavailableZones)
        {
            foreach (var cell in zone.Value)
            {
                if (cell.x == x && cell.y == y)
                {
                    return zone.Key;
                }
            }
        }
        return -1;
    }

    bool IsAdjacentToAvailable(int zoneId)
    {
        if (!unavailableZones.ContainsKey(zoneId)) return false;

        List<Vector2Int> cells = unavailableZones[zoneId];
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        foreach (var cell in cells)
        {
            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = cell.x + dx[i];
                int ny = cell.y + dy[i];
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && grid[nx, ny])
                {
                    return true;
                }
            }
        }
        return false;
    }

    void SetGhostColor(Color color)
    {
        Renderer[] renderers = PreviewObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer rend in renderers)
        {
            Material mat = rend.material;
            mat.color = new Color(color.r, color.g, color.b, 0.5f);
        }
    }

    void OnDrawGizmos()
    {
        if (cam != null)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(ray.origin, ray.direction * 100f);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(hit.point, 0.1f);
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(hit.point + Vector3.up * 0.2f, 0.05f);
            }
        }
    }
}