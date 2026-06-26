using UnityEngine;

// 몬스터 1종의 데이터 (외형색/HP/보상/패턴풀). 외형 이미지는 추후 spritePath 추가 예정(현재 색조 tint로 구분).
public class MonsterProfile
{
    public string name;
    public float maxHp;
    public int goldReward;
    public Color tint;
    public MonsterIntent[] intentPool;   // 가중치는 중복으로 표현 (줄추가 여러 개 = 높은 빈도)
}
