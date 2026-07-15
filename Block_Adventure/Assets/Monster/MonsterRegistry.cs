using UnityEngine;

// 노드 타입별 몬스터 풀. 줄추가(RowAttack)는 모든 몹 공통 기본(높은 빈도), 나머지는 몹별 특수.
// HP/보상 수치는 임시 — 밸런스는 나중에 조정.
public static class MonsterRegistry
{
    //--- 2026-06-30 보상 모양 드랍 가중치 (보상 카드마다 독립 추첨). 1자(Block_I)가 등급 높은 모양.
    static ShapeDrop[] NormalShapes => new[] {
        new ShapeDrop("Block_I", 30), new ShapeDrop("Block_L", 18), new ShapeDrop("Block_J", 18),
        new ShapeDrop("Block_S", 14), new ShapeDrop("Block_Z", 14), new ShapeDrop("Block_T", 6),
    };
    static ShapeDrop[] EliteShapes => new[] {
        new ShapeDrop("Block_I", 80), new ShapeDrop("Block_L", 5), new ShapeDrop("Block_J", 5),
        new ShapeDrop("Block_T", 5), new ShapeDrop("Block_Square", 5),
    };
    static ShapeDrop[] BossShapes => new[] {
        new ShapeDrop("Block_I", 38), new ShapeDrop("Block_X", 16), new ShapeDrop("Block_Plus", 16),
        new ShapeDrop("Block_T", 12), new ShapeDrop("Block_Square", 10), new ShapeDrop("Block_L", 8),
    };

    static MonsterProfile[] Normal() => new[]
    {
        new MonsterProfile {
            name = "슬라임", maxHp = 300, goldReward = 20, tint = new Color(0.5f, 0.85f, 0.5f),
            //--- 2026-07-13 시그니처 패턴 '삼키기' 추가 (입문몹이라 줄추가 가중치 높게 유지)
            intentPool = new[] { MonsterIntent.RowAttack, MonsterIntent.RowAttack, MonsterIntent.Devour },
            rewardShapes = NormalShapes,
            //--- 2026-07-14 전용 픽셀아트 (크기는 눈보고 artScale 조정)
            animPath = "MonsterAnim/Slime/SlimeController", artScale = 0.65f
        },
        new MonsterProfile {
            name = "좀비", maxHp = 500, goldReward = 25, tint = new Color(0.6f, 0.4f, 0.7f),
            intentPool = new[] { MonsterIntent.RowAttack, MonsterIntent.RowAttack,
                                 MonsterIntent.ConvertBlocks, MonsterIntent.Blind,
                                 MonsterIntent.SelfCleanse },   // 독/화상 빌드 견제
            rewardShapes = NormalShapes,
            //--- 2026-07-14 전용 픽셀아트
            animPath = "MonsterAnim/Zombie/ZombieController", artScale = 0.65f
        },
    };

    static MonsterProfile[] Elite() => new[]
    {
        new MonsterProfile {
            name = "골렘", maxHp = 900, goldReward = 50, tint = new Color(0.6f, 0.6f, 0.65f),
            intentPool = new[] { MonsterIntent.RowAttack, MonsterIntent.RowAttack,
                                 MonsterIntent.GravityShift, MonsterIntent.GravityShiftRight,
                                 MonsterIntent.Dispel, MonsterIntent.SelfCleanse,
                                 MonsterIntent.Freeze },   //--- 2026-07-09 얼림(매칭 방해) 추가
            rewardShapes = EliteShapes,
            attackInterval = 2,  // 엘리트: 2턴마다 공격
            //--- 2026-07-15 전용 픽셀아트
            animPath = "MonsterAnim/Golem/GolemController", artScale = 0.65f
        },
    };

    static MonsterProfile[] Boss() => new[]
    {
        new MonsterProfile {
            name = "보스", maxHp = 1600, goldReward = 100, tint = new Color(0.9f, 0.3f, 0.3f),
            intentPool = new[] { MonsterIntent.RowAttack, MonsterIntent.RowAttack,
                                 MonsterIntent.Pivot, MonsterIntent.ConvertBlocks,
                                 MonsterIntent.Dispel,
                                 MonsterIntent.TimeBomb },   //--- 2026-07-09 시한폭탄 추가
            rewardShapes = BossShapes,
            attackInterval = 1,  // 보스: 매 턴 공격
            enrage = true        //--- 2026-07-09 체력 50% 이하 광폭화(줄추가 2개)
        },
    };

    public static MonsterProfile GetForNode(NodeType type)
    {
        var pool = PoolFor(type);
        return pool[Random.Range(0, pool.Length)];
    }

    //--- 2026-07-03 협동: 시드로 결정적 선택(양쪽 클라가 같은 몬스터 스폰)
    public static MonsterProfile GetForNode(NodeType type, int seed)
    {
        var pool = PoolFor(type);
        return pool[new System.Random(seed).Next(pool.Length)];
    }

    static MonsterProfile[] PoolFor(NodeType type) => type switch
    {
        NodeType.Elite => Elite(),
        NodeType.Boss  => Boss(),
        _              => Normal(),
    };
}
