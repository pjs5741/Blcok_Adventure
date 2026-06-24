public class Relic_GlassHeart : Relic
{
    public override string Name => "유리 심장";
    public override string Description => "그리드가 60% 이상 차면 그 턴 데미지 ×1.3";
    public override string IconResourcePath => "RelicIcons/GlassHeart";

    public override void OnAcquire(PlayerStats stats) => stats.glassHeartBonus = 1.3f;
    public override void OnRemove(PlayerStats stats) => stats.glassHeartBonus = 1f;
}
