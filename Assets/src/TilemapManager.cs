using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapManager : MonoBehaviour
{
    [Header("瓦片地图对象")]
    public Tilemap tilemap;

    [Header("瓦片信息")]
    public TileBase normalTileBase;
    public TileBase errorTileBase;

    // 初始化
    void Start()
    {

    }

    // 更新
    void Update()
    {

    }

    // 错误替换
    public void ReplaceErrorTiles(Vector3 worldPos)
    {
        ReplaceErrorTiles(WorldToCell(worldPos));
    }

    public void ReplaceErrorTiles(Vector3Int cellpos)
    {
        SetTile(cellpos, errorTileBase);
    }

    // 正常替换
    public void ReplaceNormalTiles(Vector3 worldPos)
    {
        ReplaceNormalTiles(WorldToCell(worldPos));
    }

    public void ReplaceNormalTiles(Vector3Int cellpos)
    {
        SetTile(cellpos, normalTileBase);
    }

    // 获取指定位置的Tile
    public TileBase GetTile(Vector3 worldPos)
    {
        return GetTile(WorldToCell(worldPos));
    }

    public TileBase GetTile(Vector3Int cellPos)
    {
        return tilemap.GetTile(cellPos);
    }

    // 设置或替换指定位置的Tile
    public void SetTile(Vector3 worldPos, TileBase newTile)
    {
        SetTile(WorldToCell(worldPos), newTile);
    }

    public void SetTile(Vector3Int cellPos, TileBase newTile)
    {
        tilemap.SetTile(cellPos, newTile);
    }

    // 清除指定位置的Tile
    public void ClearTile(Vector3 worldPos)
    {
        ClearTile(WorldToCell(worldPos));
    }

    public void ClearTile(Vector3Int cellPos)
    {
        tilemap.SetTile(cellPos, null);
    }

    // 判断某个格子是否有Tile
    public bool HasTile(Vector3 worldPos)
    {
        return HasTile(WorldToCell(worldPos));
    }

    public bool HasTile(Vector3Int cellPos)
    {
        return tilemap.HasTile(cellPos);
    }

    // 在区域内随机替换
    public void ReplaceTilesInArea(Vector3 worldStart, Vector3 worldEnd, TileBase[] tilePool)
    {
        ReplaceTilesInArea(WorldToCell(worldStart), WorldToCell(worldEnd), tilePool);
    }

    public void ReplaceTilesInArea(Vector3Int start, Vector3Int end, TileBase[] tilePool)
    {
        for (int x = start.x; x <= end.x; x++)
        {
            for (int y = start.y; y <= end.y; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                if (tilemap.HasTile(pos))
                {
                    TileBase randomTile = tilePool[Random.Range(0, tilePool.Length)];
                    tilemap.SetTile(pos, randomTile);
                }
            }
        }
    }

    // 世界坐标转格子坐标
    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        return tilemap.WorldToCell(worldPos);
    }

    //格子坐标转世界坐标
    public Vector3 CellToWorld(Vector3Int cellPos)
    {
        return tilemap.CellToWorld(cellPos);
    }
}
