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

        // 락 직후 폭탄 자동 폭발 (반경1 AoE) — 줄/색매칭 검사 전에 먼저 처리
        yield return StartCoroutine(ExplodeBombs());

        do
        {
            hasEvent = false;

            HashSet<Transform> lineBlocks = GetLineClearBlocks();
            HashSet<Transform> matchBlocks = GetColorMatchBlocks();

            HashSet<Transform> allToDestroy = new HashSet<Transform>(lineBlocks);
            allToDestroy.UnionWith(matchBlocks);

            // [비활성화] 깨질 때 폭탄 AoE — 이제 폭탄은 락 시점에 자동 폭발(ExplodeBombs)함.
            // 완성 후에도 락-폭발 방식 유지 시 아래 블록 삭제.
            // HashSet<Transform> bombAoE = new HashSet<Transform>();
            // foreach (Transform b in allToDestroy)
            // {
            //     if (b == null || GetColorID(b) != 5) continue;
            //     int bx = Mathf.RoundToInt(b.position.x);
            //     int by = Mathf.RoundToInt(b.position.y);
            //     for (int dx = -1; dx <= 1; dx++)
            //         for (int dy = -1; dy <= 1; dy++)
            //         {
            //             int nx = bx + dx, ny = by + dy;
            //             if (!IsValidIndex(nx, ny)) continue;
            //             if (data.gridArray[nx, ny] != null) bombAoE.Add(data.gridArray[nx, ny]);
            //         }
            // }
            // if (bombAoE.Count > 0)
            // {
            //     allToDestroy.UnionWith(bombAoE);
            //     Debug.Log($"💣 폭탄 AoE +{bombAoE.Count}칸");
            // }

            if (allToDestroy.Count > 0)
            {
                hasEvent = true;
                comboCount++;

                AttackContext ctx = new AttackContext { comboCount = comboCount };

                for (int y = 0; y < data.height; ++y)
                    if (IsLineClearable(y)) ctx.lineClearCount++;

                ctx.lineMultiplier = GetLineMultiplier(ctx.lineClearCount);
                ctx.isDoubleHit = (lineBlocks.Count > 0 && matchBlocks.Count > 0);

                float perBlock = playerStats.baseDamage / playerStats.matchThreshold;
                float comboMult = 1.0f + comboCount * playerStats.comboMultiplier;

                int shieldFromColor = 0, shieldFromLine = 0;
                int poisonFromColor = 0, poisonFromLine = 0;
                int burnFromColor = 0, burnFromLine = 0;
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
                        case 2: burnFromColor++; break;                         // 화염 (화상 스택)
                        case 3: poisonFromColor++; break;                       // 독약
                        case 4: shieldFromColor++; break;                       // 방패
                        case 5: /* 폭탄: AoE는 별도 처리 */ break;
                        default: colorBlockDamage += perBlock; break;
                    }
                }

                // 줄 클리어 전용 블록 (색깔 매칭 미포함) — 감소 효과
                HashSet<Transform> lineOnly = new HashSet<Transform>(lineBlocks);
                lineOnly.ExceptWith(matchBlocks);

                foreach (Transform block in lineOnly)
                {
                    int icon = GetColorID(block);
                    AddCount(ctx.iconMatchCounts, icon);
                    switch (icon)
                    {
                        case 1: lineBlockDamage += perBlock; break;
                        case 2: burnFromLine++; break;
                        case 3: poisonFromLine++; break;
                        case 4: shieldFromLine++; break;
                        case 5: /* 폭탄 */ break;
                        default: lineBlockDamage += perBlock; break;
                    }
                }

                // 줄 데미지에 라인 배율 적용
                lineBlockDamage *= ctx.lineMultiplier;

                // 디버프 스택 — 줄 클리어는 라인 배율 적용 후 반내림
                int totalPoison = poisonFromColor + Mathf.FloorToInt(poisonFromLine * ctx.lineMultiplier);
                int totalBurn = burnFromColor + Mathf.FloorToInt(burnFromLine * ctx.lineMultiplier);
                int totalShield = shieldFromColor + Mathf.FloorToInt(shieldFromLine * ctx.lineMultiplier);

                ctx.shieldCleared = totalShield;
                ctx.poisonStacks = totalPoison;
                ctx.burnStacks = totalBurn;

                float totalBaseDamage = (colorBlockDamage + lineBlockDamage) * comboMult;

                //--- 2026-06-23 유물 조건부 배율 (미보유 시 ×1 → 무영향)
                if (comboCount >= 4) totalBaseDamage *= playerStats.chainComboBonus;        // 성좌의 사슬
                if (GetFillRatio() >= 0.6f) totalBaseDamage *= playerStats.glassHeartBonus; // 유리 심장

                ctx.damageMultiplier = totalBaseDamage / Mathf.Max(playerStats.baseDamage, 1f);
                currentDamageMultiplier = ctx.damageMultiplier;

                Debug.Log($"{comboCount}콤보 | 줄{ctx.lineClearCount}({ctx.lineMultiplier:F1}배) | 색매칭 {matchBlocks.Count}개 | 합 데미지 ~{totalBaseDamage:F0} | 방패 {totalShield} 독 {totalPoison} 화상 {totalBurn}");

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

    // 락 직후 폭탄(id 5) 자동 폭발 — 보드에 폭탄이 있으면 반경1을 파괴하고 중력 적용.
    // 폭탄은 락 시점에만 생기므로 보통 1회, 떨어져 생긴 폭탄까지 연쇄 처리하려고 루프.
    IEnumerator ExplodeBombs()
    {
        while (true)
        {
            HashSet<Transform> bombs = new HashSet<Transform>();
            for (int x = 0; x < data.width; x++)
                for (int y = 0; y < data.height; y++)
                {
                    Transform cell = data.gridArray[x, y];
                    if (cell != null && GetColorID(cell) == 5) bombs.Add(cell);
                }

            if (bombs.Count == 0) yield break;

            HashSet<Transform> toDestroy = new HashSet<Transform>();
            foreach (Transform b in bombs)
            {
                int bx = Mathf.RoundToInt(b.position.x);
                int by = Mathf.RoundToInt(b.position.y);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = bx + dx, ny = by + dy;
                        if (!IsValidIndex(nx, ny)) continue;
                        if (data.gridArray[nx, ny] != null) toDestroy.Add(data.gridArray[nx, ny]);
                    }
            }

            Debug.Log($"💣 폭탄 {bombs.Count}개 자동 폭발 → {toDestroy.Count}칸 파괴");

            foreach (Transform t in toDestroy)
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

            // 폭탄 폭발은 중력 미적용 — 터진 자리는 빈칸으로 유지 (줄 클리어와 동일 정책)
        }
    }

    static void AddCount(Dictionary<int, int> dict, int key)
    {
        if (key <= 0) return;
        if (!dict.ContainsKey(key)) dict[key] = 0;
        dict[key]++;
    }


    HashSet<Transform> GetLineClearBlocks()
    {
        HashSet<Transform> targetBlocks = new HashSet<Transform>();

        for (int y = 0; y < data.height; ++y)
        {
            if (IsLineClearable(y))
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

    // 그리드 충전율 (채워진 칸 / 전체 칸) — 유리 심장 유물 조건 판정용
    float GetFillRatio()
    {
        int filled = 0;
        for (int x = 0; x < data.width; x++)
            for (int y = 0; y < data.height; y++)
                if (data.gridArray[x, y] != null) filled++;
        return (float)filled / (data.width * data.height);
    }

    int GetColorID(Transform block)
    {
        BlockColor info = block.GetComponent<BlockColor>();
        if (info != null) return info.colorID;
        return 0;
    }
}
