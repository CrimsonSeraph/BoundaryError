using System.Collections;
using UnityEngine;

public class NormalBackground : MonoBehaviour
{
    private TilemapManager tilemapManager;
    [Header("开始与结束点")]
    [SerializeField] private int startTileX = -9;
    [SerializeField] private int startTileY = -5;
    [SerializeField] private int endTileX = 9;
    [SerializeField] private int endTileY = 5;
    [Header("停顿时间")]
    [SerializeField] private float pause = 2f;
    [Header("是否开启随机替换")]
    [SerializeField] private bool isRunning = true;
    [Header("随机替换概率参数")]
    [SerializeField] private float replaceChance = 0.1f;

    void Start()
    {
        tilemapManager = GetComponent<TilemapManager>();
        StartCoroutine(RandomReplaceLoop());
    }

    /// <summary>
    /// 在区域内随机替换瓦片
    /// </summary>
    IEnumerator RandomReplaceLoop()
    {
        while (isRunning)
        {
            if (tilemapManager != null)
            {
                Vector3Int start = new Vector3Int(startTileX, startTileY, 0);
                Vector3Int end = new Vector3Int(endTileX, endTileY, 0);

                tilemapManager.ReplaceErrorTilesInArea(start, end, replaceChance);
                yield return new WaitForSeconds(pause);

                tilemapManager.ReplaceNormalTilesInArea(start, end, replaceChance);
                yield return new WaitForSeconds(pause);
            }
            else
            {
                yield break;
            }
        }
    }
}
