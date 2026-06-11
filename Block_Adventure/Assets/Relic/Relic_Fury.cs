using UnityEngine;

public class Relic_Fury : Relic
{
    public override string Name => "분노";
    public override string Description => "매 턴 시작 시 공격력 +5 (영구 누적)";
    public override string IconResourcePath => "RelicIcons/Gladius";

    private PlayerStats _stats;

    public override void OnAcquire(PlayerStats stats)
    {
        _stats = stats;
        GameEvents.OnTurnStart += HandleTurnStart;
    }

    public override void OnRemove(PlayerStats stats)
    {
        GameEvents.OnTurnStart -= HandleTurnStart;
    }

    void HandleTurnStart()
    {
        _stats.baseDamage += 5;
        Debug.Log($"⚔ 분노 발동 → 공격력 {_stats.baseDamage}");
    }
}
