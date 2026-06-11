public class Relic_ComboMaster : Relic
{
    public override string Name => "콤보 마스터";
    public override string Description => "콤보 배율 +5% (영구)";
    public override string IconResourcePath => "RelicIcons/Gladius";

    public override void OnAcquire(PlayerStats stats) => stats.comboMultiplier += 0.05f;
    public override void OnRemove(PlayerStats stats) => stats.comboMultiplier -= 0.05f;
}
