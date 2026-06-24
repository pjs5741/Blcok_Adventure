using UnityEngine;

public class Relic_GuardianHand : Relic
{
    public override string Name => "수호의 손";
    public override string Description => "7턴마다 몬스터 공격을 1턴 지연시킨다";
    public override string IconResourcePath => "RelicIcons/GuardianHand";

    private int _turnCounter;

    public override void OnAcquire(PlayerStats stats)
    {
        _turnCounter = 0;
        GameEvents.OnTurnStart += HandleTurnStart;
    }

    public override void OnRemove(PlayerStats stats)
    {
        GameEvents.OnTurnStart -= HandleTurnStart;
    }

    void HandleTurnStart()
    {
        _turnCounter++;
        if (_turnCounter % 7 != 0) return;

        var monster = GameManager.Instance?.battleManager?.currentMonster;
        if (monster != null && monster.gameObject.activeSelf)
        {
            monster.AddAttackDelay(1);
            Debug.Log("🛡 수호의 손 발동 → 몬스터 공격 1턴 지연");
        }
    }
}
