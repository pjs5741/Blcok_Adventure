using UnityEngine;

// 노드 타입별 몬스터 풀. 줄추가(RowAttack)는 모든 몹 공통 기본(높은 빈도), 나머지는 몹별 특수.
// HP/보상 수치는 임시 — 밸런스는 나중에 조정.
public static class MonsterRegistry
{
    static MonsterProfile[] Normal() => new[]
    {
        new MonsterProfile {
            name = "슬라임", maxHp = 3000, goldReward = 20, tint = new Color(0.5f, 0.85f, 0.5f),
            intentPool = new[] { MonsterIntent.RowAttack }   // 입문용: 줄추가만
        },
        new MonsterProfile {
            name = "좀비", maxHp = 4500, goldReward = 25, tint = new Color(0.6f, 0.4f, 0.7f),
            intentPool = new[] { MonsterIntent.RowAttack, MonsterIntent.RowAttack,
                                 MonsterIntent.ConvertBlocks, MonsterIntent.Blind }
        },
    };

    static MonsterProfile[] Elite() => new[]
    {
        new MonsterProfile {
            name = "골렘", maxHp = 8000, goldReward = 50, tint = new Color(0.6f, 0.6f, 0.65f),
            intentPool = new[] { MonsterIntent.RowAttack, MonsterIntent.RowAttack,
                                 MonsterIntent.GravityShift, MonsterIntent.GravityShiftRight }
        },
    };

    static MonsterProfile[] Boss() => new[]
    {
        new MonsterProfile {
            name = "보스", maxHp = 15000, goldReward = 100, tint = new Color(0.9f, 0.3f, 0.3f),
            intentPool = new[] { MonsterIntent.RowAttack, MonsterIntent.RowAttack,
                                 MonsterIntent.Pivot, MonsterIntent.ConvertBlocks }
        },
    };

    public static MonsterProfile GetForNode(NodeType type)
    {
        MonsterProfile[] pool = type switch
        {
            NodeType.Elite => Elite(),
            NodeType.Boss  => Boss(),
            _              => Normal(),
        };
        return pool[Random.Range(0, pool.Length)];
    }
}
