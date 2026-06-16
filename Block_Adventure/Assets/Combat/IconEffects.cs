// 아이콘 ID → 효과 매핑
// 1: 칼 (기본 데미지)
// 2: 분노 (+20% 데미지 — BlockGrid.Matching에서 per-block 계산 시 처리)
// 3: 독약 (독 N스택, 즉시 데미지 X)
// 4: 방패 (데미지 X, 10개당 몬스터 1턴 딜레이)
// 5: 폭탄 (락 시 폭발 — BlockMovement에서 별도 처리)
// 99: 회색 garbage (매칭 X, 줄/폭탄으로만 제거)

public static class IconEffects
{
    public static void Apply(AttackContext ctx, Monster target)
    {
        if (target == null) return;

        // 독약 스택 적용
        if (ctx.poisonStacks > 0)
            target.ApplyPoison(ctx.poisonStacks);

        // 방패 딜레이 적용 — 10개당 1턴
        int delayTurns = ctx.shieldCleared / 10;
        if (delayTurns > 0)
            target.AddAttackDelay(delayTurns);
    }
}
