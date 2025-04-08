using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 그리드 데이터를 저장하는 클래스
/// </summary>
public class GridData
{
    // 설치된 오브젝트 데이터가 담긴 딕셔너리
    //private Dictionary<Vector3Int, PlacementData> placedObjects = new();
    
    //최적화 시도
    public Dictionary<Vector3Int, List<PlacementData>> placedObjects = new();

    #region 딕셔너리에 설치된 오브젝트 포함

    /// <summary>
    /// 설치된 오브젝트의 데이터를 딕셔너리에 추가한다.
    /// </summary>
    /// <param name="gridPosition"></param>
    /// <param name="objectSize"></param>
    /// <param name="ID"></param>
    /// <param name="placedObjectIndex"></param>
    /// <param name="rotation"></param>
    /// <param name="grid"></param>
    /// <exception cref="Exception"></exception>
    public void AddObjectAt(Vector3Int gridPosition, Vector2Int objectSize, int ID, int placedObjectIndex, int kindOfIndex, Quaternion rotation, Grid grid, bool isWall)
    {
        List<Vector3Int> positions = CalculatePosition(gridPosition, objectSize, rotation, grid);
        PlacementData data = new PlacementData(positions, ID, placedObjectIndex, kindOfIndex);

        /*foreach (var pos in positions)
        {
            if (placedObjects.ContainsKey(pos))
            {
                PlacementData existingObject = placedObjects[pos];
                // 설치하려는 오브젝트가 벽인 경우
                if (isWall)
                {
                    // 해당 위치에 이미 벽이 아닌 오브젝트가 있으면 예외 발생
                    if (!PlacementSystem.Instance.database.objectsData
                        .Find(data => data.ID == existingObject.ID).IsWall)
                    {
                        throw new Exception($"이 셀({pos})은 벽이 아닌 오브젝트로 이미 점유되어 있습니다.");
                    }
                }
                // 설치하려는 오브젝트가 벽이 아닌 경우
                else
                {
                    throw new Exception($"이 셀({pos})은 이미 딕셔너리에 포함되어있다");
                }
            }
            placedObjects[pos] = data; // 벽이면 기존 데이터를 덮어씀
        }*/
        
        foreach (var pos in positions)
        {
            if (!placedObjects.ContainsKey(pos))
            {
                placedObjects[pos] = new List<PlacementData>();
            }

            var existingObjects = placedObjects[pos];
            if (!isWall) // 가구인 경우
            {
                // 같은 위치에 가구가 이미 있으면 예외 발생
                if (existingObjects.Any(obj => !PlacementSystem.Instance.database.GetObjectData(obj.ID).IsWall))
                {
                    throw new Exception($"이 셀({pos})은 이미 가구로 점유되어 있습니다.");
                }
                
                // 같은 위치에 벽이 있고, 그 위치에 다른 가구가 이미 있으면 예외 발생
                if (existingObjects.Any(obj => PlacementSystem.Instance.database.GetObjectData(obj.ID).IsWall))
                {
                    // 다른 GridData(furnitureData)에서 해당 위치에 가구가 있는지 확인
                    var furnitureData = PlacementSystem.Instance.furnitureData; // furnitureData에 접근 (public으로 변경 필요)
                    if (furnitureData != this && furnitureData.placedObjects.ContainsKey(pos) &&
                        furnitureData.placedObjects[pos].Any(obj => !PlacementSystem.Instance.database.GetObjectData(obj.ID).IsWall))
                    {
                        throw new Exception($"이 셀({pos})은 벽과 가구가 이미 존재하여 추가 가구를 배치할 수 없습니다.");
                    }
                }
            }
            placedObjects[pos].Add(data);
        }

        /*foreach (var pos in positions)
        {
            if (placedObjects.ContainsKey(pos))
            {
                throw new Exception($"이 셀({pos})은 이미 딕셔너리에 포함되어있다");
            }
            placedObjects[pos] = data;
        }*/
    }
    #endregion

    #region 그리드 좌표 계산

    /// <summary>
    /// 설치할 오브젝트의 상하좌우 길이를 실시간으로 변경하여 딕셔너리와 비교할 수 있도록 계산한다.
    /// </summary>
    /// <param name="gridPosition"></param>
    /// <param name="objectSize"></param>
    /// <param name="rotation"></param>
    /// <param name="grid"></param>
    /// <returns></returns>
    public List<Vector3Int> CalculatePosition(Vector3Int gridPosition, Vector2Int objectSize, Quaternion rotation, Grid grid)
    {
        List<Vector3Int> positions = new List<Vector3Int>();

        // 회전 각도를 0, 90, 180, 270도로 반올림
        float angle = Mathf.Round(rotation.eulerAngles.y / 90) * 90;

        // 회전된 오브젝트 크기 계산
        Vector2Int size = objectSize;
        if (Mathf.Approximately(angle % 180, 90)) // 90도 또는 270도일 때
        {
            size = new Vector2Int(objectSize.y, objectSize.x); // 너비와 높이 교환
        }

        // 회전 각도에 따른 기준점 오프셋 계산
        Vector2Int offset;
        if (Mathf.Approximately(angle, 0))
        {
            offset = new Vector2Int(0, 0); // 0도: 기준점 그대로
        }
        else if (Mathf.Approximately(angle, 90))
        {
            offset = new Vector2Int(0, size.y - 1); // 90도: Z축으로 조정
        }
        else if (Mathf.Approximately(angle, 180))
        {
            offset = new Vector2Int(size.x - 1, size.y - 1); // 180도: X와 Z 모두 조정
        }
        else // 270도
        {
            offset = new Vector2Int(size.x - 1, 0); // 270도: X축으로 조정
        }

        // 기준 위치 조정 (왼쪽 상단 코너 기준)
        Vector3Int startPos = gridPosition - new Vector3Int(offset.x, 0, offset.y);

        // 점유 셀 위치 계산
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector3Int cellPos = startPos + new Vector3Int(x, 0, y);
                positions.Add(cellPos);
            }
        }

        return positions;
    }
    /*public List<Vector3Int> CalculatePosition(Vector3Int gridPosition, Vector2Int objectSize, Quaternion rotation, Grid grid)
    {
        List<Vector3Int> positions = new List<Vector3Int>();

        // 회전 각도 (0, 90, 180, 270 중 하나로 반올림)
        float angle = Mathf.Round(rotation.eulerAngles.y / 90) * 90;

        // 회전된 크기 계산
        Vector2Int size = objectSize;
        if (Mathf.Approximately(angle % 180, 90))
        {
            // 90도나 270도 회전 시 x와 y 교환
            size = new Vector2Int(objectSize.y, objectSize.x);
        }

        // 회전 각도에 따른 방향 벡터 계산
        Vector2Int dirX, dirY;

        if (Mathf.Approximately(angle, 0))
        {
            // 0도 회전 - 기본 방향
            dirX = new Vector2Int(1, 0);
            dirY = new Vector2Int(0, 1);
        }
        else if (Mathf.Approximately(angle, 90))
        {
            // 90도 회전
            dirX = new Vector2Int(0, 1);
            dirY = new Vector2Int(0, -1);
        }
        else if (Mathf.Approximately(angle, 180))
        {
            // 180도 회전
            dirX = new Vector2Int(-1, 0);
            dirY = new Vector2Int(0, -1);
        }
        else // 270도
        {
            // 270도 회전
            dirX = new Vector2Int(0, -1);
            dirY = new Vector2Int(0, 1);
        }

        // 시작 위치 계산 (항상 왼쪽 상단 코너에서 시작)
        Vector3Int startPos = gridPosition;

        // 모든 셀 위치 계산
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector3Int cellPos = new Vector3Int(
                    startPos.x + dirX.x * x + dirY.x * y,
                    0,
                    startPos.z + dirX.y * x + dirY.y * y
                );
                positions.Add(cellPos);
            }
        }

        return positions;
    }*/
    #endregion

    #region 점유 확인 여부 플래그

    /// <summary>
    /// 설치하려는 위치에 오브젝트가 존재 유무를 판단한다.
    /// </summary>
    /// <param name="gridPosition"></param>
    /// <param name="objectSize"></param>
    /// <param name="rotation"></param>
    /// <param name="grid"></param>
    /// <returns></returns>
    public bool CanPlaceObjectAt(Vector3Int gridPosition, Vector2Int objectSize, Quaternion rotation, Grid grid, bool isWall)
    {
        List<Vector3Int> positions = CalculatePosition(gridPosition, objectSize, rotation, grid);

        /*foreach (var pos in positions)
        {
            if (placedObjects.ContainsKey(pos))
            {
                PlacementData existingObject = placedObjects[pos];
                // 설치하려는 오브젝트가 벽인 경우
                if (isWall)
                {
                    // 해당 위치에 이미 벽이 아닌 오브젝트가 있으면 설치 불가
                    if (!PlacementSystem.Instance.database.objectsData
                        .Find(data => data.ID == existingObject.ID).IsWall)
                    {
                        return false; // 벽이 아닌 오브젝트와 겹치므로 설치 불가
                    }
                }
                // 설치하려는 오브젝트가 벽이 아닌 경우
                else
                {
                    return false; // 이미 점유된 위치이므로 설치 불가
                }
            }
        }*/
        
        foreach (var pos in positions)
        {
            if (placedObjects.ContainsKey(pos) && placedObjects[pos].Count > 0)
            {
                var existingObjects = placedObjects[pos];
                if (isWall)
                {
                    // 벽 배치 시, 가구가 이미 있으면 이후 가구 배치를 제한할 수 있음
                    // 현재는 벽과 가구 공존을 허용하되, 가구-벽-가구 패턴은 아래에서 체크
                    continue;
                }
                else // 가구인 경우
                {
                    // 같은 위치에 가구가 있으면 배치 불가
                    if (existingObjects.Any(obj => !PlacementSystem.Instance.database.GetObjectData(obj.ID).IsWall))
                    {
                        return false;
                    }

                    // 같은 위치에 벽이 있고, furnitureData에 이미 가구가 있으면 배치 불가
                    if (existingObjects.Any(obj => PlacementSystem.Instance.database.GetObjectData(obj.ID).IsWall))
                    {
                        var furnitureData = PlacementSystem.Instance.furnitureData; // furnitureData에 접근
                        if (furnitureData != this && furnitureData.placedObjects.ContainsKey(pos) &&
                            furnitureData.placedObjects[pos].Any(obj => !PlacementSystem.Instance.database.GetObjectData(obj.ID).IsWall))
                        {
                            return false;
                        }
                    }
                }
            }
        }
        return true;
    }
    #endregion
}