using UnityEngine;

public class Relic_Reaper : Relic
{
    public override string Name => "사신";
    public override string Description => "몬스터 처치 시 공격력 +10 (영구)";
    public override string IconResourcePath => "RelicIcons/Gladius";

    private PlayerStats _stats;

    public override void OnAcquire(PlayerStats stats)
    {
        _stats = stats;
        GameEvents.OnMonsterDeath += HandleKill;
    }

    public override void OnRemove(PlayerStats stats)
    {
        GameEvents.OnMonsterDeath -= HandleKill;
    }

    void HandleKill()
    {
        _stats.baseDamage += 10;
        Debug.Log($"☠ 사신 발동 → 공격력 {_stats.baseDamage}");
    }
}
