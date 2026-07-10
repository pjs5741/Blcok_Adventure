using UnityEngine;
using UnityEngine.UI;

//--- 2026-06-29 호버 툴팁. 어디서든 TooltipUI.Instance.ShowTip(제목, 본문) / HideTip() 호출.
// 자체 ScreenSpaceOverlay 캔버스(최상위)에 자동크기 패널을 만들고 마우스를 따라다닌다. 최초 호출 시 지연 생성.
public class TooltipUI : MonoBehaviour
{
    static TooltipUI _instance;
    public static TooltipUI Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("TooltipUI");
                _instance = go.AddComponent<TooltipUI>();
            }
            return _instance;
        }
    }

    RectTransform _panel;
    Canvas _canvas;   //--- 2026-07-10 scaleFactor 보정용
    Text _title, _body;
    bool _visible;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        Build();
        HideTip();
    }

    void Build()
    {
        var font = UIFont.Regular;

        var canvasGO = new GameObject("TooltipCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;   // 항상 최상위
        UIScale.Configure(canvasGO.GetComponent<CanvasScaler>());   //--- 2026-07-03 해상도 스케일 통일
        _canvas = canvas;

        var panelGO = new GameObject("Panel", typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelGO.transform.SetParent(canvasGO.transform, false);
        _panel = panelGO.GetComponent<RectTransform>();
        _panel.anchorMin = _panel.anchorMax = Vector2.zero;   // 좌하단 앵커 → anchoredPosition = 스크린 픽셀로 배치(안정적)
        _panel.pivot = new Vector2(0f, 1f);                   // 좌상단 기준 → 커서 우하단에 표시
        _panel.sizeDelta = new Vector2(600, 0);

        var bg = panelGO.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.06f, 0.09f, 0.96f);
        bg.raycastTarget = false;

        var vlg = panelGO.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(28, 28, 22, 22);
        vlg.spacing = 12;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        var fit = panelGO.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;   // 폭은 380 고정
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;     // 높이는 내용에 맞춤

        _title = NewText("Title", panelGO.transform, font, 48, FontStyle.Bold, new Color(0.97f, 0.86f, 0.5f));
        _body = NewText("Body", panelGO.transform, font, 40, FontStyle.Normal, new Color(0.9f, 0.9f, 0.9f));
    }

    Text NewText(string name, Transform parent, Font font, int size, FontStyle style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font; t.fontSize = size; t.fontStyle = style; t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public void ShowTip(string title, string body)
    {
        if (_panel == null) return;
        _title.text = title;
        _body.text = body;
        _visible = true;
        _panel.gameObject.SetActive(true);
        PositionAtMouse();
    }

    public void HideTip()
    {
        _visible = false;
        if (_panel != null) _panel.gameObject.SetActive(false);
    }

    // 인스턴스가 이미 있을 때만 숨김 (종료/비활성 중 새 인스턴스 생성 방지)
    public static void HideIfShown()
    {
        if (_instance != null) _instance.HideTip();
    }

    void LateUpdate()
    {
        if (_visible) PositionAtMouse();
    }

    void PositionAtMouse()
    {
        //--- 2026-07-10 CanvasScaler 사용 시 anchoredPosition은 "캔버스 단위"인데 마우스는 "스크린 픽셀"이라
        // 해상도가 1920x1080이 아니면 scaleFactor만큼 어긋나(마우스에서 멀어짐) → 스케일로 나눠 보정
        float sf = (_canvas != null && _canvas.scaleFactor > 0f) ? _canvas.scaleFactor : 1f;
        Vector2 m = (Vector2)Input.mousePosition / sf;
        float screenW = Screen.width / sf, screenH = Screen.height / sf;

        Vector2 pos = m + new Vector2(16f, -16f);
        float w = _panel.rect.width, h = _panel.rect.height;
        // 화면 밖으로 안 나가게 클램프 (pivot 좌상단 기준: 패널은 [x, x+w] x [y-h, y] 차지)
        float x = Mathf.Clamp(pos.x, 0f, Mathf.Max(0f, screenW - w));
        float y = Mathf.Clamp(pos.y, h, screenH);
        _panel.anchoredPosition = new Vector2(x, y);   // 앵커 좌하단(0,0) 기준, 캔버스 단위
    }
}
