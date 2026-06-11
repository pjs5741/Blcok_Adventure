using UnityEngine;
using System;
using System.Collections;
using Random = UnityEngine.Random;

public partial class BlockGrid : MonoBehaviour
{
    public GridData data;
    public BlockSpawner spawner;

    [Header("Animation Settings")]
    public float dropDuration = 0.2f;
    public float destroyDuration = 0.3f;

    [Header("Game Status")]
    public int comboCount = 0;
    public float currentDamageMultiplier = 1f;
    public event Action<AttackContext> OnMatchCompleted;

    private PlayerStats playerStats;

    void Awake()
    {
        data = new GridData(11, 21);
        spawner = FindFirstObjectByType<BlockSpawner>();
        playerStats = FindFirstObjectByType<Player>().stats;
    }

    void Start()
    {
        LoadSnapshot();
    }

    public void SaveSnapshot()
    {
        int[,] snap = new int[data.width, data.height];
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
                if (data.gridArray[x, y] != null)
                {
                    var bc = data.gridArray[x, y].GetComponent<BlockColor>();
                    snap[x, y] = bc != null ? bc.colorID : 0;
                }
        Run.gridSnapshot = snap;
    }

    public void LoadSnapshot()
    {
        if (Run.gridSnapshot == null) return;
        int[,] snap = Run.gridSnapshot;
        if (snap.GetLength(0) != data.width || snap.GetLength(1) != data.height) return;

        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
            {
                int colorID = snap[x, y];
                if (colorID == 0) continue;
                Color color = spawner.GetColorByID(colorID);
                GameObject block = spawner.SpawnStaticBlock(x, y, colorID, color);
                block.transform.SetParent(transform);
                data.gridArray[x, y] = block.transform;
            }
    }

    public bool IsValidPosition(Transform blockParent)
    {
        foreach (Transform child in blockParent)
        {
            Vector2 pos = child.position;
            int x = Mathf.RoundToInt(pos.x);
            int y = Mathf.RoundToInt(pos.y);

            if (x < 0 || x >= data.width || y < 0) return false;
            if (y >= data.height) continue;
            if (data.gridArray[x, y] != null) return false;
        }
        return true;
    }

    public void AddToGrid(Transform blockParent)
    {
        foreach (Transform child in blockParent)
        {
            int x = Mathf.RoundToInt(child.position.x);
            int y = Mathf.RoundToInt(child.position.y);

            if (x < 0 || x >= data.width || y < 0 || y >= data.height) continue;
            data.gridArray[x, y] = child;
        }
    }

    public bool IsGameOver()
    {
        for (int x = 0; x < data.width; x++)
        {
            if (data.gridArray[x, data.height - 1] != null)
                return true;
        }
        return false;
    }

    public bool IsValidIndex(int x, int y)
    {
        return x >= 0 && x < data.width && y >= 0 && y < data.height;
    }
}
