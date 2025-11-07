using System.Collections;
using UnityEditor.Experimental.Licensing;
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
    [SerializeField] private float replacePause = 0.5f;
    [SerializeField] private float restorePause = 0.5f;
    [Header("是否开启随机替换")]
    [SerializeField] private bool isRunning = true;
    [Header("随机替换参数")]
    [SerializeField] private float replaceAre = 0.3f;

    private bool enableReplace = true;

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
        Vector3Int start = new Vector3Int(startTileX, startTileY, 0);
        Vector3Int end = new Vector3Int(endTileX, endTileY, 0);

        while (isRunning)
        {
            if (tilemapManager == null) yield break;

            int errorTilesNumber = tilemapManager.GetErrorTilesNumber();
            int selectedTilesNumber = tilemapManager.GetSelectedTilesNumber();
            float errorRatio = (float)errorTilesNumber / selectedTilesNumber;

            if (errorTilesNumber == 0 && !enableReplace)
            {
                enableReplace = true;
                continue;
            }

            if (errorRatio < 0.2f && enableReplace)
            {
                tilemapManager.ReplaceErrorTilesInArea(start, end, replaceAre);
                yield return new WaitForSeconds(replacePause);
            }
            else
            {
                enableReplace = false;

                if (Random.value < 0.5f)
                {
                    tilemapManager.ReplaceErrorTilesInArea(start, end, replaceAre);
                    yield return new WaitForSeconds(replacePause);
                }
                else
                {
                    tilemapManager.RestoreTilesInArea();
                    yield return new WaitForSeconds(restorePause);
                }
            }
        }
    }
}
