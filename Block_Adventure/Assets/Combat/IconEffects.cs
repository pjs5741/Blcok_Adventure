// 아이콘 ID → 효과 매핑
// 1: 칼 (기본 데미지)
// 2: 분노 (+20% 데미지 — BlockGrid.Matching에서 per-block 계산 시 처리)
// 3: 독약 (독 N스택, 즉시 데미지 X)
// 4: 방패 (데미지 X, 누적되어 몬스터 공격 시 Tuning.ShieldPerDelay 소모 → 1턴 지연)
// 5: 폭탄 (락 시 폭발 — BlockMovement에서 별도 처리)
// 99: 회색 garbage (매칭 X, 줄/폭탄으로만 제거)

public static class IconEffects
{
    public static void Apply(AttackContext ctx, Monster target)
    {
        if (target == null) return;

        // 독약 스택 적용 (블록 1개당 N스택 → 틱 데미지=스택 그대로라 직관적)
        if (ctx.poisonStacks > 0)
            target.ApplyPoison(ctx.poisonStacks * Tuning.DotStacksPerBlock);

        // 화상 스택 적용
        if (ctx.burnStacks > 0)
            target.ApplyBurn(ctx.burnStacks * Tuning.DotStacksPerBlock);

        //--- 2026-07-01 방패는 즉시 소모(N개당 1턴 지연) 대신 플레이어 버프로 누적.
        // 몬스터가 공격할 때마다 Tuning.ShieldPerDelay 만큼 소모되어 공격을 1턴 지연시킨다(BattleManager).
        if (ctx.shieldCleared > 0)
            Run.stats.shieldStacks += ctx.shieldCleared;
    }
}
