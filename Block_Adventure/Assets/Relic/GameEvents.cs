using System;

public static class GameEvents
{
    public static event Action OnTurnStart;
    public static event Action OnTurnEnd;
    public static event Action OnMonsterDeath;

    public static void RaiseTurnStart() => OnTurnStart?.Invoke();
    public static void RaiseTurnEnd() => OnTurnEnd?.Invoke();
    public static void RaiseMonsterDeath() => OnMonsterDeath?.Invoke();
}
