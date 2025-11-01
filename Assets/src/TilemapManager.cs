using System.Collections;
using System.Collections.Generic;
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

    [Header("调试设置")]
    [SerializeField] private bool enableDebug = false;

    #region 公共接口

    /// <summary>
    /// 替换指定格子为错误瓦片
    /// </summary>
    public void ReplaceErrorTile(Vector3 worldPos) => ReplaceTile(WorldToCell(worldPos), errorTileBase);

    public void ReplaceErrorTile(Vector3Int cellPos) => ReplaceTile(cellPos, errorTileBase);

    /// <summary>
    /// 替换指定格子为正常瓦片
    /// </summary>
    public void ReplaceNormalTile(Vector3 worldPos) => ReplaceTile(WorldToCell(worldPos), normalTileBase);

    public void ReplaceNormalTile(Vector3Int cellPos) => ReplaceTile(cellPos, normalTileBase);

    /// <summary>
    /// 获取指定位置的 Tile
    /// </summary>
    public TileBase GetTile(Vector3 worldPos) => GetTile(WorldToCell(worldPos));

    public TileBase GetTile(Vector3Int cellPos)
    {
        if (!CheckTilemap()) return null;
        return tilemap.GetTile(cellPos);
    }

    /// <summary>
    /// 设置指定位置的 Tile
    /// </summary>
    public void SetTile(Vector3 worldPos, TileBase newTile) => SetTile(WorldToCell(worldPos), newTile);

    public void SetTile(Vector3Int cellPos, TileBase newTile)
    {
        if (!CheckTilemap() || newTile == null) return;
        tilemap.SetTile(cellPos, newTile);
        DebugTileChange(cellPos, newTile);
    }

    /// <summary>
    /// 清除指定位置的 Tile
    /// </summary>
    public void ClearTile(Vector3 worldPos) => ClearTile(WorldToCell(worldPos));

    public void ClearTile(Vector3Int cellPos)
    {
        if (!CheckTilemap()) return;
        tilemap.SetTile(cellPos, null);
        DebugTileChange(cellPos, null);
    }

    /// <summary>
    /// 判断指定位置是否有 Tile
    /// </summary>
    public bool HasTile(Vector3 worldPos) => HasTile(WorldToCell(worldPos));

    public bool HasTile(Vector3Int cellPos)
    {
        if (!CheckTilemap()) return false;
        return tilemap.HasTile(cellPos);
    }

    /// <summary>
    /// 在区域内随机替换错误瓦片
    /// </summary>
    public void ReplaceErrorTilesInArea(Vector3 worldStart, Vector3 worldEnd, float replaceChance)
        => ReplaceTilesInArea(worldStart, worldEnd, errorTileBase, replaceChance);

    public void ReplaceErrorTilesInArea(Vector3Int start, Vector3Int end, float replaceChance)
        => ReplaceTilesInArea(start, end, errorTileBase, replaceChance);

    /// <summary>
    /// 在区域内随机替换正常瓦片
    /// </summary>
    public void ReplaceNormalTilesInArea(Vector3 worldStart, Vector3 worldEnd, float replaceChance)
        => ReplaceTilesInArea(worldStart, worldEnd, normalTileBase, replaceChance);

    public void ReplaceNormalTilesInArea(Vector3Int start, Vector3Int end, float replaceChance)
        => ReplaceTilesInArea(start, end, normalTileBase, replaceChance);

    /// <summary>
    /// 在区域内随机替换瓦片
    /// </summary>
    /// <param name="worldStart">世界坐标起点</param>
    /// <param name="worldEnd">世界坐标终点</param>
    /// <param name="tilePool">随机瓦片池</param>
    public void ReplaceTilesInArea(Vector3 worldStart, Vector3 worldEnd, TileBase targetTile, float replaceChance)
        => ReplaceTilesInArea(WorldToCell(worldStart), WorldToCell(worldEnd), targetTile, replaceChance);

    public void ReplaceTilesInArea(Vector3Int start, Vector3Int end, TileBase targetTile, float replaceChance)
    {
        if (!CheckTilemap() || targetTile == null) return;

        Vector3Int realStart = new Vector3Int(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y), 0);
        Vector3Int realEnd = new Vector3Int(Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y), 0);

        for (int x = realStart.x; x <= realEnd.x; x++)
        {
            for (int y = realStart.y; y <= realEnd.y; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                if (tilemap.HasTile(pos))
                {
                    if (Random.value < replaceChance)
                    {
                        tilemap.SetTile(pos, targetTile);
                        DebugTileChange(pos, targetTile);
                    }
                }
            }
        }
    }

    #endregion

    #region 工具函数

    /// <summary>
    /// 世界坐标转格子坐标
    /// </summary>
    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        if (!CheckTilemap()) return Vector3Int.zero;
        return tilemap.WorldToCell(worldPos);
    }

    /// <summary>
    /// 格子坐标转世界坐标
    /// </summary>
    public Vector3 CellToWorld(Vector3Int cellPos)
    {
        if (!CheckTilemap()) return Vector3.zero;
        return tilemap.CellToWorld(cellPos);
    }

    /// <summary>
    /// 瓦片替换
    /// </summary>
    private void ReplaceTile(Vector3Int cellPos, TileBase tile)
    {
        if (!CheckTilemap() || tile == null) return;
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
            if (enableDebug) Debug.LogWarning("TilemapManager: tilemap 未设置！");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 输出瓦片变化
    /// </summary>
    private void DebugTileChange(Vector3Int cellPos, TileBase tile)
    {
        if (!enableDebug) return;
        string tileName = tile == null ? "null" : tile.name;
        Debug.Log($"TilemapManager: [{cellPos.x},{cellPos.y}] 设置为 {tileName}");
    }

    #endregion
}
