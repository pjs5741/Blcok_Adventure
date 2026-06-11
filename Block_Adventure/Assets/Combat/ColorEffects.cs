public static class ColorEffects
{
    public static void Apply(AttackContext ctx, Monster target)
    {
        bool damaging = ctx.lineClearCount > 0;

        foreach (var pair in ctx.colorMatchCounts)
        {
            switch (pair.Key)
            {
                case 1: // 빨강
                    ctx.damageMultiplier *= 1.2f;
                    damaging = true;
                    break;
                case 5: // 보라
                    target.ApplyPoison(pair.Value);
                    break;
                default: // 파랑/초록/노랑 등 — placeholder, 기본 데미지만
                    damaging = true;
                    break;
            }
        }

        if (!damaging) ctx.damageMultiplier = 0f;
    }
}
