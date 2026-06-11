using System.Collections.Generic;

public class AttackContext
{
    public int comboCount;
    public int lineClearCount;
    public Dictionary<int, int> colorMatchCounts = new Dictionary<int, int>();
    public float damageMultiplier = 1f;
    public bool isDoubleHit;
}
