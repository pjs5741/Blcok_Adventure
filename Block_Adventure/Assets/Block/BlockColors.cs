using UnityEngine;

//--- 2026-06-29 블록 색(효과 id) 단일 소스. 색/이름/설명/아이콘을 한 곳에서 관리.
// spawner 없는 씬(보상 카드/상점)에서도 정적으로 색·설명을 얻기 위함.
// id: 1=칼 2=화염 3=독약 4=방패 5=폭탄 / 98=금(보물) 99=회색(garbage)
public static class BlockColors
{
    public const int Gold = 98;
    public const int Gray = 99;
    public const int MinEffect = 1, MaxEffect = 5;   // 카드/스폰에 쓰이는 효과 색 범위

    public static Color Get(int id)
    {
        switch (id)
        {
            case 1: return new Color(0.95f, 0.95f, 0.95f);   // 칼 — 흰색
            case 2: return new Color(0.85f, 0.2f, 0.2f);     // 화염 — 빨강
            case 3: return new Color(0.5f, 0.2f, 0.7f);      // 독약 — 보라
            case 4: return new Color(0.2f, 0.5f, 0.85f);     // 방패 — 파랑
            case 5: return new Color(0.3f, 0.2f, 0.1f);      // 폭탄 — 어두운 갈색
            case Gold: return new Color(1f, 0.84f, 0f);      // 금 블럭 — 노랑
            case Gray: return Color.gray;                     // garbage
            default: return Color.white;
        }
    }

    public static string Name(int id) => id switch
    {
        1 => "칼", 2 => "화염", 3 => "독약", 4 => "방패", 5 => "폭탄",
        Gold => "보물", Gray => "방해", _ => "?"
    };

    public static string Desc(int id) => id switch
    {
        1 => "매칭 시 블록당 기본 데미지를 준다.",
        //--- 2026-07-01 수치는 Tuning 상수에서 파생(문구-코드 불일치 방지)
        2 => $"화상을 쌓는다. 블록 1개당 {Tuning.DotStacksPerBlock}스택, 매 턴 스택만큼 데미지 (매 턴 -1).",
        3 => $"독을 쌓는다. 블록 1개당 {Tuning.DotStacksPerBlock}스택, 매 턴 스택만큼 데미지 (매 턴 -1).",
        4 => $"방패를 모은다. {Tuning.ShieldPerDelay}개마다 몬스터 공격을 1턴 지연.",
        5 => "깨질 때 주변 1칸을 함께 파괴한다. 단, 폭발로 부서진 블록은 효과가 발동하지 않는다.",
        Gold => "줄 클리어/폭탄으로 깨면 골드를 준다.",
        Gray => "매칭으로는 못 없앤다. 줄을 채워 지우거나 폭탄 등 다른 방법으로 제거.",
        _ => ""
    };

    public static string IconName(int id) => id switch
    {
        1 => "Sword", 2 => "Fire", 3 => "Potion", 4 => "Shield", 5 => "Bomb", _ => null
    };

    public static Sprite Icon(int id)
    {
        string n = IconName(id);
        return n == null ? null : Resources.Load<Sprite>($"BlockIcons/{n}");
    }
}
