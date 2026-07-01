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

    public override void OnLoad(PlayerStats stats) => OnAcquire(stats);   //--- 2026-07-01 로드 시 재구독(스탯 변경 없음)

    void HandleTurnStart()
    {
        //--- 2026-07-01 baseDamage 영구 누적 → 전투 단위 버프(buffAttack)로. 디스펠로 제거 가능 + 버프 표시.
        _stats.buffAttack += 5;
        Debug.Log($"⚔ 분노 발동 → 공격 버프 +{_stats.buffAttack}");
    }
}
