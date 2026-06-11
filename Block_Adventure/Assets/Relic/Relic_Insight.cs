using UnityEngine;

public class Relic_Insight : Relic
{
    const int MinThreshold = 5;

    public override string Name => "통찰";
    public override string Description => "턴 끝마다 매칭 임계값 -1 (최소 5)";
    public override string IconResourcePath => "RelicIcons/Gladius";

    private PlayerStats _stats;

    public override void OnAcquire(PlayerStats stats)
    {
        _stats = stats;
        GameEvents.OnTurnEnd += HandleTurnEnd;
    }

    public override void OnRemove(PlayerStats stats)
    {
        GameEvents.OnTurnEnd -= HandleTurnEnd;
    }

    void HandleTurnEnd()
    {
        if (_stats.matchThreshold > MinThreshold)
        {
            _stats.matchThreshold--;
            Debug.Log($"👁 통찰 → 매칭 임계값 {_stats.matchThreshold}");
        }
    }
}
