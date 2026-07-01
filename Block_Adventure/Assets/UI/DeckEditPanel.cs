using UnityEngine;
using UnityEngine.UI;

//--- 2026-06-30 덱 보기 + 카드 제거 오버레이. 휴식/상점에서 사용.
// 무료 제거 횟수(removalsAllowed) 또는 장당 골드비용(goldCost) 중 하나로 동작.
public class DeckEditPanel : MonoBehaviour
{
    private int _removalsLeft;
    private int _goldCost;
    private bool _viewOnly;
    private System.Collections.Generic.List<(string shape, int color)> _entries;  // 지정 리스트(안쓴/쓴 카드). null이면 Run.deck 전체
    private string _titleOverride;
    private System.Action _onChanged;
    private RectTransform _grid;
    private Text _header;
    private GameObject _root;

    public void Open(Transform canvas, int removalsAllowed, int goldCostPerRemoval, System.Action onChanged)
    {
        _removalsLeft = removalsAllowed;
        _goldCost = goldCostPerRemoval;
        _viewOnly = (removalsAllowed <= 0 && goldCostPerRemoval <= 0);   // 둘 다 0이면 보기 전용
        _onChanged = onChanged;
        Build(canvas);
        Rebuild();
    }

    // 지정 리스트(안 쓴/쓴 카드 등)를 카드로 보기 전용 표시
    public void OpenList(Transform canvas, System.Collections.Generic.List<(string shape, int color)> entries, string title)
    {
        _entries = entries;
        _titleOverride = title;
        _viewOnly = true;
        Build(canvas);
        Rebuild();
    }

    void Build(Transform canvas)
    {
        _root = UIBuilder.NewUI("DeckEditPanel", canvas, typeof(RectTransform), typeof(Image));
        UIBuilder.Stretch((RectTransform)_root.transform);
        _root.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.97f);

        _header = UIBuilder.Text(_root.transform, "Header", "", 40, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
        UIBuilder.SetAnchors(_header.rectTransform, new Vector2(0.1f, 0.88f), new Vector2(0.9f, 0.97f));

        //--- 2026-07-01 카드가 많아지면(로그라이크라 덱 성장) 세로 스크롤. 뷰포트(마스크) + 콘텐츠(그리드).
        var scrollGO = UIBuilder.NewUI("Scroll", _root.transform, typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
        UIBuilder.SetAnchors((RectTransform)scrollGO.transform, new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.86f));

        var contentGO = UIBuilder.NewUI("Content", scrollGO.transform, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        _grid = (RectTransform)contentGO.transform;
        _grid.anchorMin = new Vector2(0f, 1f);   // 상단 스트레치 → 위에서부터 아래로 늘어남
        _grid.anchorMax = new Vector2(1f, 1f);
        _grid.pivot = new Vector2(0.5f, 1f);
        _grid.offsetMin = new Vector2(0f, 0f);
        _grid.offsetMax = new Vector2(0f, 0f);
        _grid.anchoredPosition = Vector2.zero;

        var glg = contentGO.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(210, 240);
        glg.spacing = new Vector2(18, 18);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 4;
        glg.childAlignment = TextAnchor.UpperCenter;

        var fitter = contentGO.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;   // 폭은 앵커 스트레치
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;     // 높이는 카드 수에 맞춰 늘어남

        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.content = _grid;
        scroll.viewport = (RectTransform)scrollGO.transform;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var close = UIBuilder.Button(_root.transform, "Close", "닫기", 34, Close, new Color(0.4f, 0.4f, 0.4f));
        UIBuilder.SetAnchors((RectTransform)close.transform, new Vector2(0.4f, 0.04f), new Vector2(0.6f, 0.12f));
    }

    void Rebuild()
    {
        foreach (Transform c in _grid) Destroy(c.gameObject);

        if (_entries != null)
            foreach (var e in _entries) MakeCard(e.shape, e.color, -1);   // 리스트 뷰(제거 불가)
        else
            for (int i = 0; i < Run.deck.Count; i++) MakeCard(Run.deck[i].blockName, Run.deck[i].colorID, i);

        UpdateHeader();
    }

    // 카드 1장: 프레임 + 실제 모양(셀) + 효과 이름 + 호버 툴팁. removeIdx>=0이면 클릭 제거.
    void MakeCard(string shape, int colorID, int removeIdx)
    {
        UnityEngine.Events.UnityAction onClick = removeIdx >= 0 ? (() => TryRemove(removeIdx)) : (UnityEngine.Events.UnityAction)null;
        var btn = UIBuilder.Button(_grid, "Card", null, 0, onClick, new Color(0.12f, 0.13f, 0.17f));
        var slot = (RectTransform)btn.transform;

        var tip = btn.gameObject.AddComponent<TooltipTarget>();
        tip.title = BlockColors.Name(colorID);   // 모양 글자(S/J/L 등) 빼고 효과 이름만
        tip.body = BlockColors.Desc(colorID);

        var shapeHost = UIBuilder.NewUI("Shape", slot, typeof(RectTransform));
        UIBuilder.SetAnchors((RectTransform)shapeHost.transform, new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.95f));
        ShapeMiniView.Build((RectTransform)shapeHost.transform, shape, colorID, 34f);

        var lbl = UIBuilder.Text(slot, "Eff", BlockColors.Name(colorID), 30, new Color(0.92f, 0.92f, 0.96f), TextAnchor.MiddleCenter);
        lbl.raycastTarget = false;
        lbl.horizontalOverflow = HorizontalWrapMode.Wrap;   // 긴 이름 박스 밖 넘침 방지
        UIBuilder.SetAnchors(lbl.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.33f));
    }

    void UpdateHeader()
    {
        if (_titleOverride != null)
            _header.text = $"{_titleOverride} ({(_entries != null ? _entries.Count : Run.deck.Count)}장)";
        else if (_viewOnly)
            _header.text = $"덱 보기 — 전체 {Run.deck.Count}장";
        else if (_goldCost > 0)
            _header.text = $"카드 제거 — 장당 {_goldCost}골드 (보유 {Run.stats.gold})";
        else
            _header.text = $"카드 제거 — 남은 횟수 {_removalsLeft}";
    }

    void TryRemove(int idx)
    {
        if (_viewOnly) return;   // 보기 전용은 제거 불가
        if (idx < 0 || idx >= Run.deck.Count) return;
        if (Run.deck.Count <= 1) return;   // 덱이 비면 안 되므로 최소 1장 유지

        if (_goldCost > 0)
        {
            if (Run.stats.gold < _goldCost) return;
            Run.stats.gold -= _goldCost;
        }
        else
        {
            if (_removalsLeft <= 0) return;
            _removalsLeft--;
        }

        Run.deck.RemoveAt(idx);
        _onChanged?.Invoke();
        Rebuild();
    }

    void Close()
    {
        if (_root != null) Destroy(_root);
        Destroy(gameObject);
    }
}
