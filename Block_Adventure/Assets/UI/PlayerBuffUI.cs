using UnityEngine;
using UnityEngine.UI;

//--- 2026-07-01 플레이어 버프(방패·공격) 표시. 몬스터 디버프 뱃지와 "크기·스타일 통일"(색 사각형 + 우하단 숫자 + 툴팁).
// 화면 좌하단(플레이어 쪽)에 활성 버프만 왼쪽부터 빈틈없이 배치. 값은 Run.stats에서 매 프레임 읽음. 이미지는 막판 리소스 작업.
public class PlayerBuffUI : MonoBehaviour
{
    private Canvas _canvas;
    private GameObject _shieldBadge, _attackBadge;
    private Text _shieldCount, _attackCount;

    const float BaseX = 60f, BaseY = 60f;   // 좌하단 시작점

    void Start() => EnsureBuilt();

    void EnsureBuilt()
    {
        if (_canvas != null) return;
        _canvas = FindFirstObjectByType<Canvas>();
        if (_canvas == null) return;

        _shieldBadge = MakeBadge("Buff_Shield", new Color(0.2f, 0.5f, 0.85f), out _shieldCount,
            "방패", $"누적된 방패. 몬스터가 공격할 때마다 {Tuning.ShieldPerDelay}개를 소모해 공격을 1턴 지연시킨다.");
        _attackBadge = MakeBadge("Buff_Attack", new Color(0.9f, 0.5f, 0.2f), out _attackCount,
            "공격력 버프", "누적된 추가 공격력. 몬스터의 디스펠에 당하면 사라진다.");
    }

    // 디버프 뱃지(Monster.CreateDebuffBadge)와 동일 스타일: 색 사각형 + 우하단 숫자 + 호버 툴팁
    GameObject MakeBadge(string name, Color c, out Text count, string tipTitle, string tipBody)
    {
        var go = UIBuilder.NewUI(name, _canvas.transform, typeof(RectTransform), typeof(Image), typeof(TooltipTarget));
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;      // 좌하단 기준
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(Tuning.BadgeSize, Tuning.BadgeSize);
        go.GetComponent<Image>().color = c;

        var tip = go.GetComponent<TooltipTarget>();
        tip.title = tipTitle; tip.body = tipBody;

        var cGO = UIBuilder.NewUI("Count", go.transform, typeof(RectTransform), typeof(Text));
        count = cGO.GetComponent<Text>();
        count.font = UIFont.Bold;
        count.fontSize = 22;
        count.alignment = TextAnchor.LowerRight;
        count.color = Color.white;
        count.raycastTarget = false;
        count.horizontalOverflow = HorizontalWrapMode.Overflow;
        count.verticalOverflow = VerticalWrapMode.Overflow;
        var crt = count.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 0f);   // 우하단
        crt.pivot = new Vector2(1f, 0f);
        crt.anchoredPosition = new Vector2(6, -4);
        crt.sizeDelta = new Vector2(40, 26);
        return go;
    }

    void Update()
    {
        EnsureBuilt();
        if (_shieldBadge == null || !Run.IsInitialized) return;

        int shield = Run.stats.shieldStacks;
        int atk = Mathf.RoundToInt(Run.stats.buffAttack);

        // 활성 버프만 왼쪽부터 빈틈없이 (디버프와 동일 규칙)
        int slot = 0;
        Place(_shieldBadge, _shieldCount, shield > 0, shield.ToString(), ref slot);
        Place(_attackBadge, _attackCount, atk > 0, "+" + atk, ref slot);
    }

    void Place(GameObject badge, Text count, bool active, string text, ref int slot)
    {
        badge.SetActive(active);
        if (!active) return;
        ((RectTransform)badge.transform).anchoredPosition = new Vector2(BaseX + slot * Tuning.BadgeSpacing, BaseY);
        count.text = text;
        slot++;
    }
}
