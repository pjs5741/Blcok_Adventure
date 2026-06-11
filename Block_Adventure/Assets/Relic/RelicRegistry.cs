using System;

public static class RelicRegistry
{
    public static readonly Func<Relic>[] All = new Func<Relic>[]
    {
        () => new Relic_Gladius(),
        () => new Relic_Fury(),
        () => new Relic_Reaper(),
        () => new Relic_Insight(),
        () => new Relic_ComboMaster(),
    };

    public static Relic GetRandom() => All[UnityEngine.Random.Range(0, All.Length)]();
}
