using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tilemap 管理器，提供瓦片查询、替换、清除、区域操作接口。
/// 支持世界坐标和格子坐标调用，可统一调试输出。
/// </summary>
public class TilemapManager : MonoBehaviour
{
    [Header("瓦片地图对象")]
    [SerializeField] private Tilemap tilemap;

    [Header("瓦片类型")]
    [SerializeField] private TileBase normalTileBase;
    [SerializeField] private TileBase errorTileBase;

    [Header("DeBug 信息")]
    [SerializeField] private bool enableDebug = false;
    [SerializeField] private bool enableDebugMore = false;

    private HashSet<Vector3Int> selectedTiles = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> errorTiles = new HashSet<Vector3Int>();
    private bool selectIsRunning = false;
    private bool replaceIsRunning = false;
    private bool restoreIsRunning = false;

    #region 公共接口

    /// <summary>
    /// 替换指定格子为错误瓦片
    /// </summary>
    public void ReplaceErrorTile(Vector3 worldPos)
    {
        if (!ValidateWorldPosition(worldPos)) return;
        ReplaceTile(WorldToCell(worldPos), errorTileBase);
    }

    public void ReplaceErrorTile(Vector3Int cellPos)
    {
        if (!ValidateCellPosition(cellPos)) return;
        ReplaceTile(cellPos, errorTileBase);
    }

    /// <summary>
    /// 替换指定格子为正常瓦片
    /// </summary>
    public void ReplaceNormalTile(Vector3 worldPos)
    {
        if (!ValidateWorldPosition(worldPos)) return;
        ReplaceTile(WorldToCell(worldPos), normalTileBase);
    }

    public void ReplaceNormalTile(Vector3Int cellPos)
    {
        if (!ValidateCellPosition(cellPos)) return;
        ReplaceTile(cellPos, normalTileBase);
    }

    /// <summary>
    /// 获取指定位置的 Tile
    /// </summary>
    public TileBase GetTile(Vector3 worldPos)
    {
        if (!ValidateWorldPosition(worldPos)) return null;
        return GetTile(WorldToCell(worldPos));
    }

    public TileBase GetTile(Vector3Int cellPos)
    {
        if (!CheckTilemap() || !ValidateCellPosition(cellPos)) return null;
        return tilemap.GetTile(cellPos);
    }

    /// <summary>
    /// 设置指定位置的 Tile
    /// </summary>
    public void SetTile(Vector3 worldPos, TileBase newTile)
    {
        if (!ValidateWorldPosition(worldPos)) return;
        SetTile(WorldToCell(worldPos), newTile);
    }

    public void SetTile(Vector3Int cellPos, TileBase newTile)
    {
        if (!CheckTilemap() || !ValidateCellPosition(cellPos)) return;

        if (newTile == null)
        {
            Debug.LogError($"[TilemapManager] 尝试设置的瓦片为null，位置: {cellPos}");
            return;
        }

        tilemap.SetTile(cellPos, newTile);
        DebugTileChange(cellPos, newTile);
    }

    /// <summary>
    /// 清除指定位置的 Tile
    /// </summary>
    public void ClearTile(Vector3 worldPos)
    {
        if (!ValidateWorldPosition(worldPos)) return;
        ClearTile(WorldToCell(worldPos));
    }

    public void ClearTile(Vector3Int cellPos)
    {
        if (!CheckTilemap() || !ValidateCellPosition(cellPos)) return;

        if (!tilemap.HasTile(cellPos))
        {
            Debug.LogWarning($"[TilemapManager] 尝试清除不存在的瓦片，位置: {cellPos}");
            return;
        }

        tilemap.SetTile(cellPos, null);
        DebugTileChange(cellPos, null);
    }

    /// <summary>
    /// 判断指定位置是否有 Tile
    /// </summary>
    public bool HasTile(Vector3 worldPos)
    {
        if (!ValidateWorldPosition(worldPos)) return false;
        return HasTile(WorldToCell(worldPos));
    }

    public bool HasTile(Vector3Int cellPos)
    {
        if (!CheckTilemap() || !ValidateCellPosition(cellPos)) return false;
        return tilemap.HasTile(cellPos);
    }

    /// <summary>
    /// 在区域内随机替换一块瓦片为错误瓦片
    /// </summary>
    /// <param name="worldStart">世界坐标起点</param>
    /// <param name="worldEnd">世界坐标终点</param>
    /// <param name="replaceArea">替换区域比例</param>
    public void ReplaceErrorTilesInArea(Vector3 worldStart, Vector3 worldEnd, float replaceArea)
    {
        if (!ValidateWorldPosition(worldStart) || !ValidateWorldPosition(worldEnd)) return;
        ReplaceErrorTilesInArea(WorldToCell(worldStart), WorldToCell(worldEnd), replaceArea);
    }

    public void ReplaceErrorTilesInArea(Vector3Int start, Vector3Int end, float replaceArea)
    {
        if (!CheckTilemap()) return;

        if (!ValidateAreaParameters(start, end, replaceArea)) return;

        if (replaceIsRunning)
        {
            DebugMoreInfo($"[TilemapManager] 替换操作正在进行中，请等待完成");
            return;
        }

        selectTileInArea(start, end, replaceArea);

        if (selectedTiles.Count == 0)
        {
            DebugMoreInfo($"[TilemapManager] 指定区域内没有选择替换的瓦片");
            return;
        }

        restoreIsRunning = true;

        bool replaced = false;
        foreach (Vector3Int pos in selectedTiles)
        {
            if (!tilemap.HasTile(pos)) continue;
            if (errorTiles.Contains(pos)) continue;
            if (Random.value > 0.5f)
            {
                SetTile(pos, errorTileBase);
                errorTiles.Add(pos);
                DebugTileChange(pos, errorTileBase);
                replaced = true;
                break;
            }
        }

        if (!replaced)
        {
            DebugMoreInfo($"[TilemapManager] 区域内没有找到合适的瓦片进行替换");
        }

        restoreIsRunning = false;
    }

    private void selectTileInArea(Vector3Int start, Vector3Int end, float replaceArea)
    {
        if (selectedTiles.Count != 0) return;

        if (selectIsRunning)
        {
            DebugMoreInfo($"[TilemapManager] 选择操作正在进行中，请等待完成");
            return;
        }

        selectIsRunning = true;

        Vector3Int realStart = new Vector3Int(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y), 0);
        Vector3Int realEnd = new Vector3Int(Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y), 0);

        HashSet<Vector3Int> allTilePositions = GetAllTilePositions(tilemap, realStart, realEnd);

        if (allTilePositions.Count == 0)
        {
            DebugMoreInfo($"[TilemapManager] 指定区域内没有瓦片");
            selectIsRunning = false;
            return;
        }

        int totalTiles = allTilePositions.Count;
        int targetCount = Mathf.Max(1, (int)(totalTiles * replaceArea));

        List<Vector3Int> tileList = new List<Vector3Int>(allTilePositions);
        ShuffleList(tileList);

        for (int i = 0; i < targetCount && i < tileList.Count; i++)
        {
            selectedTiles.Add(tileList[i]);
        }

        DebugMoreInfo($"[TilemapManager] 在区域内选择了 {selectedTiles.Count} 个瓦片，总共 {totalTiles} 个瓦片");
        selectIsRunning = false;
    }

    /// <summary>
    /// 在区域内随机恢复一块瓦片
    /// </summary>
    public void RestoreTilesInArea()
    {
        if (!CheckTilemap()) return;

        if (errorTiles.Count == 0)
        {
            DebugMoreInfo($"[TilemapManager] 没有错误瓦片需要恢复");
            return;
        }

        if (restoreIsRunning)
        {
            DebugMoreInfo($"[TilemapManager] 恢复操作正在进行中，请等待完成");
            return;
        }

        restoreIsRunning = true;

        HashSet<Vector3Int> tilesToRestore = new HashSet<Vector3Int>();
        int restoredCount = 0;

        foreach (Vector3Int pos in errorTiles)
        {
            if (!tilemap.HasTile(pos)) continue;

            if (Random.value > 0.5f)
            {
                SetTile(pos, normalTileBase);
                tilesToRestore.Add(pos);
                restoredCount++;
                DebugTileChange(pos, normalTileBase);
                DebugMoreInfo($"[TilemapManager] 成功恢复了 {pos} 错误瓦片");
                break;
            }
        }

        foreach (Vector3Int pos in tilesToRestore)
        {
            errorTiles.Remove(pos);
            selectedTiles.Remove(pos);
        }

        tilesToRestore.Clear();

        restoreIsRunning = false;
    }
    #endregion

    #region 验证函数

    /// <summary>
    /// 验证世界坐标参数
    /// </summary>
    private bool ValidateWorldPosition(Vector3 worldPos)
    {
        if (!CheckTilemap()) return false;

        if (worldPos == null)
        {
            Debug.LogError($"[TilemapManager] 世界坐标参数为null");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 验证格子坐标参数
    /// </summary>
    private bool ValidateCellPosition(Vector3Int cellPos)
    {
        if (!CheckTilemap()) return false;

        if (!tilemap.cellBounds.Contains(cellPos))
        {
            Debug.LogWarning($"[TilemapManager] 格子坐标超出地图范围: {cellPos}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 验证区域操作参数
    /// </summary>
    private bool ValidateAreaParameters(Vector3Int start, Vector3Int end, float replaceArea)
    {
        if (start == null || end == null)
        {
            Debug.LogError($"[TilemapManager] 区域坐标参数为null");
            return false;
        }

        if (replaceArea <= 0f || replaceArea > 1f)
        {
            Debug.LogError($"[TilemapManager] 替换区域比例必须在(0, 1]范围内，当前值: {replaceArea}");
            return false;
        }

        Vector3Int realStart = new Vector3Int(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y), 0);
        Vector3Int realEnd = new Vector3Int(Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y), 0);

        if (realStart.x == realEnd.x || realStart.y == realEnd.y)
        {
            Debug.LogError($"[TilemapManager] 区域大小无效，起始点和结束点不能相同");
            return false;
        }

        return true;
    }

    #endregion

    #region 工具函数

    /// <summary>
    /// 世界坐标转格子坐标
    /// </summary>
    private Vector3Int WorldToCell(Vector3 worldPos)
    {
        if (!CheckTilemap()) return Vector3Int.zero;
        return tilemap.WorldToCell(worldPos);
    }

    /// <summary>
    /// 格子坐标转世界坐标
    /// </summary>
    private Vector3 CellToWorld(Vector3Int cellPos)
    {
        if (!CheckTilemap()) return Vector3.zero;
        return tilemap.CellToWorld(cellPos);
    }

    /// <summary>
    /// 瓦片替换
    /// </summary>
    private void ReplaceTile(Vector3Int cellPos, TileBase tile)
    {
        if (!CheckTilemap() || !ValidateCellPosition(cellPos) || tile == null) return;

        tilemap.SetTile(cellPos, tile);
        DebugTileChange(cellPos, tile);
    }

    /// <summary>
    /// 检查 Tilemap 是否存在
    /// </summary>
    private bool CheckTilemap()
    {
        if (tilemap == null)
        {
            Debug.LogError($"[TilemapManager] Tilemap 未分配，请检查Inspector设置");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 检查当前瓦片是否是指定瓦片
    /// </summary>
    private bool CheckTileAtPosition(Vector3 worldPos, TileBase tile)
    {
        if (!ValidateWorldPosition(worldPos)) return false;

        Vector3Int cellPos = WorldToCell(worldPos);
        TileBase currentTile = GetTile(cellPos);
        return currentTile == tile;
    }

    /// <summary>
    /// 获取瓦片地图对象指定区域瓦片坐标信息
    /// </summary>
    private HashSet<Vector3Int> GetAllTilePositions(Tilemap tilemap, Vector3Int start, Vector3Int end)
    {
        if (tilemap == null)
        {
            Debug.LogError($"[TilemapManager] Tilemap 参数为null");
            return new HashSet<Vector3Int>();
        }

        HashSet<Vector3Int> allTilePositions = new HashSet<Vector3Int>();
        BoundsInt bounds = tilemap.cellBounds;

        foreach (var position in bounds.allPositionsWithin)
        {
            if (position.x < start.x || position.x > end.x) continue;
            if (position.y < start.y || position.y > end.y) continue;

            if (tilemap.HasTile(position))
            {
                allTilePositions.Add(position);
            }
        }

        return allTilePositions;
    }

    /// <summary>
    /// 将 HashSet 转化 List 并打乱
    /// </summary>
    private void ShuffleList<T>(List<T> list)
    {
        if (list == null || list.Count == 0)
        {
            Debug.LogWarning($"[TilemapManager] 尝试打乱空列表或null列表");
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    /// <summary>
    /// 获取错误瓦片数量
    /// </summary>
    public int GetErrorTilesNumber()
    {
        return errorTiles.Count;
    }

    /// <summary>
    /// 获取选中瓦片数量
    /// </summary>
    public int GetSelectedTilesNumber()
    {
        return selectedTiles.Count;
    }

    /// <summary>
    /// 调试瓦片变化
    /// </summary>
    private void DebugTileChange(Vector3Int cellPos, TileBase newTile)
    {
        string tileName = newTile == null ? "空" : newTile.name;
        DebugMoreInfo($"[TilemapManager] 瓦片变化 - 位置: {cellPos}, 新瓦片: {tileName}");
    }

    #endregion

    #region 调试和状态信息

    /// <summary>
    /// 获取管理器状态信息
    /// </summary>
    public void PrintStatus()
    {
        if (!enableDebug) return;
        Debug.Log($"[TilemapManager] 状态信息:");
        Debug.Log($"- 选中瓦片数量: {selectedTiles.Count}");
        Debug.Log($"- 错误瓦片数量: {errorTiles.Count}");
        Debug.Log($"- 操作状态: 选择={selectIsRunning}, 替换={replaceIsRunning}, 恢复={restoreIsRunning}");
    }

    /// <summary>
    /// 清空所有选中和错误瓦片
    /// </summary>
    public void ClearAllSelections()
    {
        selectedTiles.Clear();
        errorTiles.Clear();
        Debug.Log($"[TilemapManager] 已清空所有选中和错误瓦片");
    }

    /// <summary>
    /// 输出更多状态信息
    /// </summary>
    private void DebugMoreInfo(string info)
    {
        if (!enableDebugMore) return;
        Debug.Log(info);
    }

    #endregion
}