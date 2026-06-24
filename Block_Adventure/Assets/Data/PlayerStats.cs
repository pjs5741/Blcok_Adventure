[System.Serializable]
public class PlayerStats
{
    public float baseDamage = 100f;
    public float comboMultiplier = 0.1f;
    public int matchThreshold = 15;
    public int gold = 0;

    //--- 2026-06-23 유물 조건부 배율 (유물 미보유 시 1f → 무영향)
    public float chainComboBonus = 1f;   // 성좌의 사슬: 콤보 4 이상이면 적용
    public float glassHeartBonus = 1f;   // 유리 심장: 그리드 60% 이상 차면 적용
}
