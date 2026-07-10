using UnityEngine;

//--- 2026-06-30 보상 모양 드랍 가중치 (모양 등급별 출현 확률). 보상 카드마다 독립적으로 가중 추첨.
public class ShapeDrop
{
    public string shape;   // 프리팹 이름 (Block_I 등)
    public float weight;   // 상대 가중치
    public ShapeDrop(string shape, float weight) { this.shape = shape; this.weight = weight; }
}

// 몬스터 1종의 데이터 (외형색/HP/보상/패턴풀). 외형 이미지는 추후 spritePath 추가 예정(현재 색조 tint로 구분).
public class MonsterProfile
{
    public string name;
    public float maxHp;
    public int goldReward;
    public Color tint;
    public MonsterIntent[] intentPool;   // 가중치는 중복으로 표현 (줄추가 여러 개 = 높은 빈도)
    public ShapeDrop[] rewardShapes;     // 보상 카드 모양 드랍 가중치 (null이면 기본 풀)
    public int attackInterval = 3;       //--- 2026-07-03 공격 주기(턴). 일반 3 / 엘리트 2 / 보스 1
    public bool enrage;                  //--- 2026-07-09 광폭화(체력 50% 이하 1회: 줄추가 강화) — 보스용
}
