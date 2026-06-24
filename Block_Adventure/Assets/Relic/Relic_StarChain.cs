public class Relic_StarChain : Relic
{
    public override string Name => "성좌의 사슬";
    public override string Description => "콤보 4 이상이면 그 매칭 데미지 ×1.5";
    public override string IconResourcePath => "RelicIcons/StarChain";

    public override void OnAcquire(PlayerStats stats) => stats.chainComboBonus = 1.5f;
    public override void OnRemove(PlayerStats stats) => stats.chainComboBonus = 1f;
}
