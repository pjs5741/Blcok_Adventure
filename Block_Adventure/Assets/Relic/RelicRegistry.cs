using System;
using System.Collections.Generic;

public static class RelicRegistry
{
    public static readonly Func<Relic>[] All = new Func<Relic>[]
    {
        () => new Relic_Gladius(),
        () => new Relic_Fury(),
        () => new Relic_Reaper(),
        () => new Relic_Insight(),
        () => new Relic_ComboMaster(),
        () => new Relic_GuardianHand(),
        () => new Relic_GlassHeart(),
        () => new Relic_StarChain(),
        () => new Relic_Clairvoyance(),
        () => new Relic_Hold(),
    };

    public static Relic GetRandom() => All[UnityEngine.Random.Range(0, All.Length)]();

    //--- 2026-06-23 유물 중복 획득 금지(종류당 1개). 이미 보유한 타입은 제외하고 뽑음. 다 모았으면 null
    public static Relic GetRandomExcluding(IEnumerable<Relic> owned)
    {
        var ownedTypes = new HashSet<Type>();
        if (owned != null)
            foreach (var r in owned)
                if (r != null) ownedTypes.Add(r.GetType());

        var pool = new List<Relic>();
        foreach (var make in All)
        {
            var relic = make();
            if (!ownedTypes.Contains(relic.GetType())) pool.Add(relic);
        }

        if (pool.Count == 0) return null;
        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }
}
