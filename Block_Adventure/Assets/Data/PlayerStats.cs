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

    //--- 2026-07-01 전투 단위 플레이어 버프(전투 시작 시 리셋, 몬스터 디스펠로 제거 가능)
    public int shieldStacks = 0;    // 누적 방패 — 몬스터 공격마다 Tuning.ShieldPerDelay 소모해 1턴 지연
    public float buffAttack = 0f;   // 누적 공격력 버프(분노/사신 등) — 디스펠로 제거

    public float EffectiveDamage => baseDamage + buffAttack;   // 실제 공격 계산에 쓰는 공격력

    // 전투 시작 시 버프 초기화
    public void ResetBattleBuffs()
    {
        shieldStacks = 0;
        buffAttack = 0f;
    }
}
