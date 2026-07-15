using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public partial class BlockGrid
{
    //--- 2026-06-29 금 블럭(보물): 깨질 때 골드 지급. 회색(99)과 달리 줄 클리어 가능(non-gray 취급), 색매칭 X, 데미지 X.
    public const int GoldBlockID = 98;
    public const int GoldBlockValue = 6;   // 금 블럭 1개당 골드
    //--- 2026-07-09 시한폭탄 블록: 줄 클리어 가능(non-gray 취급), 색매칭 X, 데미지 X. N턴 내 제거 못 하면 폭발(주변 회색화).
    public const int TimeBombID = 97;

    //--- 2026-07-13 오염 개편: "블록 N개" → "작은 원 범위 N군데". 예고(텔레그래프) 칸 우선 사용(미리 지우면 회피),
    // 예고가 없으면(협동 등) 같은 로직으로 즉석 선정.
    // (구) public void ConvertRandomBlocksToGray(int count) — 랜덤 블록 count개 개별 오염 → 범위형으로 대체됨
    public void ApplyCorruption(int spots, float radius)
    {
        if (!(_telegraphCells.Count > 0 && !_hasDevourTelegraph))
            TelegraphConvert(spots, radius);   // 예고 없이 실행된 경우 즉석 선정 (같은 규칙)

        int hit = 0;
        foreach (var cell in _telegraphCells)
        {
            var t = data.gridArray[cell.x, cell.y];
            if (t == null) continue;   // 플레이어가 미리 지움 → 회피
            var tbc = t.GetComponent<BlockColor>();
            if (tbc != null && tbc.colorID != 99 && tbc.colorID != GoldBlockID && tbc.colorID != TimeBombID)
            { tbc.SetColorInfo(99, Color.gray); hit++; }
        }
        ClearTelegraph();
        Debug.Log($"🟪 오염 {hit}칸 (예고 회피분 제외)");
    }

    // 멀티줄 보너스 배율 (블록 수는 줄마다 이미 늘어나므로 배율은 과하지 않게. 1줄도 0.3→1.0로 정상 데미지)
    static float GetLineMultiplier(int lineCount)
    {
        switch (lineCount)
        {
            case 0: return 0f;
            case 1: return 1.0f;
            case 2: return 1.2f;
            case 3: return 1.5f;
            case 4: return 2.0f;
            default: return 2.5f; // 5줄 이상
        }
    }

    public IEnumerator ProcessTurn()
    {
        yield return new WaitForSeconds(0.05f);

        bool hasEvent = false;
        comboCount = 0;
        currentDamageMultiplier = 1f;
        CoopLastAttackDmg = 0f;   //--- 2026-07-06 협동: 이번 턴 공격 데미지 누적(연출은 resolve에서)

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

                float perBlock = playerStats.EffectiveDamage / playerStats.matchThreshold;   //--- 2026-07-01 공격 버프 포함
                float comboMult = 1.0f + comboCount * playerStats.comboMultiplier;

                int shieldFromColor = 0, shieldFromLine = 0;
                int poisonFromColor = 0, poisonFromLine = 0;
                int burnFromColor = 0, burnFromLine = 0;
                float colorBlockDamage = 0f;
                float lineBlockDamage = 0f;

                //--- 2026-07-09 색매칭/줄클리어에 복붙돼 있던 동일 스위치문 → TallyIcon 헬퍼로 통합 (리팩토링)
                // 금 블럭도 두 경로 모두 데미지 X로 통일 (기존엔 색매칭 경로에서만 default로 데미지가 들어갈 수 있었음)
                // 색깔 매칭 블록 — 풀 효과
                foreach (Transform block in matchBlocks)
                {
                    int icon = GetColorID(block);
                    AddCount(ctx.iconMatchCounts, icon);
                    colorBlockDamage += TallyIcon(icon, perBlock, ref burnFromColor, ref poisonFromColor, ref shieldFromColor);
                }

                // 줄 클리어 전용 블록 (색깔 매칭 미포함) — 감소 효과
                HashSet<Transform> lineOnly = new HashSet<Transform>(lineBlocks);
                lineOnly.ExceptWith(matchBlocks);

                foreach (Transform block in lineOnly)
                {
                    int icon = GetColorID(block);
                    AddCount(ctx.iconMatchCounts, icon);
                    lineBlockDamage += TallyIcon(icon, perBlock, ref burnFromLine, ref poisonFromLine, ref shieldFromLine);
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
                if (GetFillRatio() >= Tuning.GlassHeartFillThreshold) totalBaseDamage *= playerStats.glassHeartBonus; // 유리 심장

                ctx.damageMultiplier = totalBaseDamage / Mathf.Max(playerStats.EffectiveDamage, 1f);
                currentDamageMultiplier = ctx.damageMultiplier;

                Debug.Log($"{comboCount}콤보 | 줄{ctx.lineClearCount}({ctx.lineMultiplier:F1}배) | 색매칭 {matchBlocks.Count}개 | 합 데미지 ~{totalBaseDamage:F0} | 방패 {totalShield} 독 {totalPoison} 화상 {totalBurn}");

                //--- 2026-07-09 컨텍스트 튜토리얼: 첫 색매칭/첫 줄클리어 "터지는 순간" 일시정지 + 해당 위치 스포트라이트
                if (matchBlocks.Count > 0 && !ContextTutorial.WasShown("match"))
                    ContextTutorial.Show("match", Centroid(matchBlocks), 2.2f, "색깔 매칭!",
                        $"같은 색깔 블록을 {playerStats.matchThreshold}개 이상 붙이면 터집니다.\n터진 뒤엔 모든 블록이 빈칸 없이 아래로 떨어져요.");
                if (ctx.lineClearCount > 0 && !ContextTutorial.WasShown("lineclear"))
                {
                    int clearY = 0;
                    for (int y = 0; y < data.height; y++) if (IsLineClearable(y)) { clearY = y; break; }
                    ContextTutorial.Show("lineclear", new Vector3((data.width - 1) / 2f, clearY, 0f), 3.2f, "줄 클리어!",
                        "가로 한 줄을 꽉 채우면 줄 전체가 지워집니다.\n회색 블록도 이 방법으로 같이 지울 수 있어요.");
                }

                foreach (Transform t in allToDestroy)
                {
                    if (t == null) continue;

                    if (GetColorID(t) == GoldBlockID) Run.stats.gold += GoldBlockValue;   // 금 블럭 파괴 → 골드

                    int tx = Mathf.RoundToInt(t.position.x);
                    int ty = Mathf.RoundToInt(t.position.y);
                    if (IsValidIndex(tx, ty) && data.gridArray[tx, ty] == t)
                        data.gridArray[tx, ty] = null;

                    StartCoroutine(AnimateAndDestroy(t));
                }

                //--- 2026-07-09 TriggerShake(duration, magnitude) 시그니처인데 (강도, 시간) 순서로 넘기던 버그 수정
                if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(Tuning.BlockDestroyShakeDuration, Tuning.BlockDestroyShakeStrength);
                yield return new WaitForSeconds(destroyDuration + 0.05f);

                //--- 2026-06-30 블록 깨진 뒤 공격. 협동은 공격 연출을 resolve(둘 다 조작 후)로 미룸 → 여기선 데미지만 계산+디버프 적용.
                var battle = GameManager.Instance != null ? GameManager.Instance.battleManager : null;
                if (CoopSession.Active)
                {
                    var mon = battle != null ? battle.currentMonster : null;
                    if (mon != null) IconEffects.Apply(ctx, mon);   // 독/화상/방패는 즉시(다음 DoT용)
                    CoopLastAttackDmg += playerStats.EffectiveDamage * ctx.damageMultiplier;   // 몹 HP는 안 깎음(resolve에서)
                }
                else if (battle != null) yield return StartCoroutine(battle.AttackRoutine(ctx));

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
                if (GetColorID(t) == GoldBlockID) Run.stats.gold += GoldBlockValue;   // 금 블럭 폭탄 파괴 → 골드
                int tx = Mathf.RoundToInt(t.position.x);
                int ty = Mathf.RoundToInt(t.position.y);
                if (IsValidIndex(tx, ty) && data.gridArray[tx, ty] == t)
                    data.gridArray[tx, ty] = null;
                StartCoroutine(AnimateAndDestroy(t));
            }

            if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(Tuning.BlockDestroyShakeStrength, Tuning.BlockDestroyShakeDuration);
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

    //--- 2026-07-09 아이콘별 효과 집계 단일 소스 (색매칭/줄클리어 공용, 스위치문 중복 제거)
    // 반환 = 데미지 기여분. 디버프(화상/독/방패)는 ref 카운트로 누적. 폭탄=AoE 별도, 금=골드 별도(데미지 X).
    static float TallyIcon(int icon, float perBlock, ref int burn, ref int poison, ref int shield)
    {
        switch (icon)
        {
            case 1: return perBlock;        // 칼
            case 2: burn++; return 0f;      // 화염 (화상 스택)
            case 3: poison++; return 0f;    // 독약
            case 4: shield++; return 0f;    // 방패
            case 5: return 0f;              // 폭탄: AoE는 별도 처리
            case GoldBlockID: return 0f;    // 금 블럭: 데미지 X (골드는 파괴 시 지급)
            case TimeBombID: return 0f;     //--- 2026-07-09 시한폭탄: 제거해도 데미지 X (해체가 목적)
            default: return perBlock;
        }
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
                if (startColor == 0 || startColor == 99 || startColor == GoldBlockID || startColor == TimeBombID) continue;   // 금·시한폭탄은 색매칭 X
                if (IsFrozenBlock(data.gridArray[x, y])) continue;   //--- 2026-07-09 빙결 블록은 매칭 시작점 불가

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
                        if (IsFrozenBlock(data.gridArray[nx, ny])) continue;   //--- 2026-07-09 빙결 블록은 매칭 연결 불가(줄클리어로만 제거)

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

    //--- 2026-07-09 [얼림] 빙결 여부 — 매칭 플러드필 제외 판정용
    static bool IsFrozenBlock(Transform block)
    {
        var bc = block.GetComponent<BlockColor>();
        return bc != null && bc.IsFrozen;
    }

    //--- 2026-07-09 블록 그룹 중심 좌표 (컨텍스트 튜토리얼 스포트라이트용)
    static Vector3 Centroid(HashSet<Transform> blocks)
    {
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var t in blocks) { if (t == null) continue; sum += t.position; n++; }
        return n > 0 ? sum / n : Vector3.zero;
    }
}
