public class Relic_Gladius : Relic
{
    public override string Name => "글라디우스";
    public override string Description => "공격력 +20";
    public override string IconResourcePath => "RelicIcons/Gladius";

    public override void OnAcquire(PlayerStats stats) => stats.baseDamage += 20;
    public override void OnRemove(PlayerStats stats) => stats.baseDamage -= 20;
}
