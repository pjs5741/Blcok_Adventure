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

    // 멀티줄 보너스 배율
    static float GetLineMultiplier(int lineCount)
    {
        switch (lineCount)
        {
            case 0: return 0f;
            case 1: return 0.3f;
            case 2: return 0.6f;
            case 3: return 1.1f;
            case 4: return 1.6f;
            default: return 2.0f; // 5줄 이상
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

            // 1단계: 폭탄 감지 + 폭발 (있으면 우선 처리)
            HashSet<Transform> bombDestroyed = DetonateBombs();
            if (bombDestroyed.Count > 0)
            {
                Debug.Log($"💣 폭탄 폭발 — {bombDestroyed.Count}칸 파괴");
                if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(0.3f, 0.4f);
                yield return new WaitForSeconds(destroyDuration + 0.05f);
                ApplyGravity();
                yield return new WaitForSeconds(dropDuration + 0.1f);
                hasEvent = true;
                continue; // 다음 이터레이션에서 매칭 체크
            }

            HashSet<Transform> lineBlocks = GetLineClearBlocks();
            HashSet<Transform> matchBlocks = GetColorMatchBlocks();

            HashSet<Transform> allToDestroy = new HashSet<Transform>(lineBlocks);
            allToDestroy.UnionWith(matchBlocks);

            if (allToDestroy.Count > 0)
            {
                hasEvent = true;
                comboCount++;

                AttackContext ctx = new AttackContext { comboCount = comboCount };

                for (int y = 0; y < data.height; ++y)
                    if (IsLineFull(y)) ctx.lineClearCount++;

                ctx.lineMultiplier = GetLineMultiplier(ctx.lineClearCount);
                ctx.isDoubleHit = (lineBlocks.Count > 0 && matchBlocks.Count > 0);

                float perBlock = playerStats.baseDamage / playerStats.matchThreshold;
                float comboMult = 1.0f + comboCount * playerStats.comboMultiplier;

                int shieldFromColor = 0, shieldFromLine = 0;
                int poisonFromColor = 0, poisonFromLine = 0;
                int fistFromColor = 0;
                float colorBlockDamage = 0f;
                float lineBlockDamage = 0f;

                // 색깔 매칭 블록 — 풀 효과
                foreach (Transform block in matchBlocks)
                {
                    int icon = GetColorID(block);
                    AddCount(ctx.iconMatchCounts, icon);
                    switch (icon)
                    {
                        case 1: colorBlockDamage += perBlock; break;           // 칼
                        case 2: colorBlockDamage += perBlock * 1.2f; fistFromColor++; break; // 분노
                        case 3: poisonFromColor++; break;                       // 독약
                        case 4: shieldFromColor++; break;                       // 방패
                        case 5: /* 폭탄: 별도 처리 */ break;
                        default: colorBlockDamage += perBlock; break;
                    }
                }

                // 줄 클리어 전용 블록 (색깔 매칭 미포함) — 감소 효과
                HashSet<Transform> lineOnly = new HashSet<Transform>(lineBlocks);
                lineOnly.ExceptWith(matchBlocks);

                int lineFistCount = 0;
                foreach (Transform block in lineOnly)
                {
                    int icon = GetColorID(block);
                    AddCount(ctx.iconMatchCounts, icon);
                    switch (icon)
                    {
                        case 1: lineBlockDamage += perBlock; break;
                        case 2: lineBlockDamage += perBlock * 1.2f; lineFistCount++; break;
                        case 3: poisonFromLine++; break;
                        case 4: shieldFromLine++; break;
                        case 5: /* 폭탄 */ break;
                        default: lineBlockDamage += perBlock; break;
                    }
                }

                // 줄 데미지에 라인 배율 적용
                lineBlockDamage *= ctx.lineMultiplier;

                // 독/방패 — 줄 클리어는 라인 배율 적용 후 반내림
                int totalPoison = poisonFromColor + Mathf.FloorToInt(poisonFromLine * ctx.lineMultiplier);
                int totalShield = shieldFromColor + Mathf.FloorToInt(shieldFromLine * ctx.lineMultiplier);

                ctx.shieldCleared = totalShield;
                ctx.poisonStacks = totalPoison;

                float totalBaseDamage = (colorBlockDamage + lineBlockDamage) * comboMult;
                ctx.damageMultiplier = totalBaseDamage / Mathf.Max(playerStats.baseDamage, 1f);
                currentDamageMultiplier = ctx.damageMultiplier;

                Debug.Log($"{comboCount}콤보 | 줄{ctx.lineClearCount}({ctx.lineMultiplier:F1}배) | 색매칭 {matchBlocks.Count}개 | 합 데미지 ~{totalBaseDamage:F0} | 방패 {totalShield} 독 {totalPoison}");

                OnMatchCompleted?.Invoke(ctx);

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

    static void AddCount(Dictionary<int, int> dict, int key)
    {
        if (key <= 0) return;
        if (!dict.ContainsKey(key)) dict[key] = 0;
        dict[key]++;
    }

    // 폭탄(iconID=5) 셀들 찾아서 반경 1 폭발
    HashSet<Transform> DetonateBombs()
    {
        HashSet<Transform> destroyed = new HashSet<Transform>();
        List<Vector2Int> bombs = new List<Vector2Int>();

        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
                if (data.gridArray[x, y] != null && GetColorID(data.gridArray[x, y]) == 5)
                    bombs.Add(new Vector2Int(x, y));

        if (bombs.Count == 0) return destroyed;

        foreach (Vector2Int b in bombs)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = b.x + dx;
                    int ny = b.y + dy;
                    if (!IsValidIndex(nx, ny)) continue;
                    if (data.gridArray[nx, ny] != null) destroyed.Add(data.gridArray[nx, ny]);
                }
        }

        foreach (Transform t in destroyed)
        {
            if (t == null) continue;
            int tx = Mathf.RoundToInt(t.position.x);
            int ty = Mathf.RoundToInt(t.position.y);
            if (IsValidIndex(tx, ty) && data.gridArray[tx, ty] == t)
                data.gridArray[tx, ty] = null;
            StartCoroutine(AnimateAndDestroy(t));
        }

        return destroyed;
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
