using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class BlockGrid
{
    public void ConvertRandomBlocksToGray(int count)
    {
        var candidates = new List<Vector2Int>();
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
                if (data.gridArray[x, y] != null)
                {
                    BlockColor bc = data.gridArray[x, y].GetComponent<BlockColor>();
                    if (bc != null && bc.colorID != 99)
                        candidates.Add(new Vector2Int(x, y));
                }

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int converted = Mathf.Min(count, candidates.Count);
        for (int i = 0; i < converted; i++)
        {
            Transform cell = data.gridArray[candidates[i].x, candidates[i].y];
            BlockColor bc = cell.GetComponent<BlockColor>();
            if (bc != null) bc.SetColorInfo(99, Color.gray);
        }
    }

    public IEnumerator ProcessTurn()
    {
        yield return new WaitForSeconds(0.05f);

        bool hasEvent = false;
        comboCount = 0;
        currentDamageMultiplier = 1f;

        do
        {
            hasEvent = false;

            HashSet<Transform> lineBlocks = GetLineClearBlocks();
            HashSet<Transform> matchBlocks = GetColorMatchBlocks();

            HashSet<Transform> allToDestroy = new HashSet<Transform>(lineBlocks);
            allToDestroy.UnionWith(matchBlocks);

            if (allToDestroy.Count > 0)
            {
                hasEvent = true;
                comboCount++;

                if (lineBlocks.Count > 0 && matchBlocks.Count > 0)
                {
                    currentDamageMultiplier = 2.0f;
                    Debug.Log($"대박! 줄+색깔 동시 폭발! (데미지 {currentDamageMultiplier}배)");
                }
                else
                {
                    currentDamageMultiplier = 1.0f + (comboCount * playerStats.comboMultiplier);
                    Debug.Log($"{comboCount}콤보! ({allToDestroy.Count}개 파괴)");
                }

                OnAttackTriggered?.Invoke(currentDamageMultiplier);

                foreach (Transform t in allToDestroy)
                {
                    if (t == null) continue;

                    int tx = Mathf.RoundToInt(t.position.x);
                    int ty = Mathf.RoundToInt(t.position.y);
                    if (IsValidIndex(tx, ty) && data.gridArray[tx, ty] == t)
                        data.gridArray[tx, ty] = null;

                    StartCoroutine(AnimateAndDestroy(t));
                }

                if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(0.2f, 0.3f);
                yield return new WaitForSeconds(destroyDuration + 0.05f);

                if (matchBlocks.Count > 0)
                    ApplyGravity();
                else
                    ApplyBlockGravity();

                yield return new WaitForSeconds(dropDuration + 0.1f);
            }

        } while (hasEvent);
    }

    HashSet<Transform> GetLineClearBlocks()
    {
        HashSet<Transform> targetBlocks = new HashSet<Transform>();

        for (int y = 0; y < data.height; ++y)
        {
            if (IsLineFull(y))
            {
                for (int x = 0; x < data.width; ++x)
                {
                    if (data.gridArray[x, y] != null) targetBlocks.Add(data.gridArray[x, y]);
                }
            }
        }
        return targetBlocks;
    }

    HashSet<Transform> GetColorMatchBlocks()
    {
        bool[,] visited = new bool[data.width, data.height];
        HashSet<Transform> targetBlocks = new HashSet<Transform>();

        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { 1, -1, 0, 0 };

        for (int x = 0; x < data.width; ++x)
        {
            for (int y = 0; y < data.height; ++y)
            {
                if (data.gridArray[x, y] == null || visited[x, y]) continue;

                int startColor = GetColorID(data.gridArray[x, y]);
                if (startColor == 0 || startColor == 99) continue;

                List<Transform> currentGroup = new List<Transform>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();

                queue.Enqueue(new Vector2Int(x, y));
                visited[x, y] = true;
                currentGroup.Add(data.gridArray[x, y]);

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = current.x + dx[i];
                        int ny = current.y + dy[i];
                        if (!IsValidIndex(nx, ny)) continue;
                        if (visited[nx, ny] || data.gridArray[nx, ny] == null) continue;
                        if (GetColorID(data.gridArray[nx, ny]) != startColor) continue;

                        visited[nx, ny] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                        currentGroup.Add(data.gridArray[nx, ny]);
                    }
                }

                if (currentGroup.Count >= playerStats.matchThreshold)
                {
                    foreach (Transform t in currentGroup) targetBlocks.Add(t);
                }
            }
        }
        return targetBlocks;
    }

    int GetColorID(Transform block)
    {
        BlockColor info = block.GetComponent<BlockColor>();
        if (info != null) return info.colorID;
        return 0;
    }
}
