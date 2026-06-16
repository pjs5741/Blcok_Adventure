using System.Collections.Generic;

public class AttackContext
{
    public int comboCount;
    public int lineClearCount;
    public Dictionary<int, int> iconMatchCounts = new Dictionary<int, int>();  // iconID → 파괴된 블록 수 (색깔 매칭 + 줄 클리어 합산)
    public int poisonStacks;                                                   // 이 매칭으로 적용할 독 스택
    public int shieldCleared;                                                  // 이 매칭으로 깨진 방패 수
    public float lineMultiplier = 1f;                                          // 줄 클리어 멀티줄 배율
    public float damageMultiplier = 1f;                                        // 색깔 효과 배율 (분노 등)
    public bool isDoubleHit;
}
