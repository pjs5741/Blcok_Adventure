using UnityEngine;
using UnityEngine.UI;

//--- 2026-07-01 협동: 상대 플레이어 그리드를 화면 우상단 빈 공간에 조그맣게 표시. resolve마다 Render로 갱신.
public class CoopPartnerView : MonoBehaviour
{
    public static CoopPartnerView Instance;

    const int W = 11, H = 21;      // 기본 그리드 크기(협동은 피벗 인텐트 제외라 고정)
    const float Cell = 8f;         // 셀 픽셀

    private RectTransform _host;
    private readonly System.Collections.Generic.List<GameObject> _cells = new System.Collections.Generic.List<GameObject>();

    void Awake() { Instance = this; }

    public void Ensure(Transform canvas)
    {
        if (_host != null || canvas == null) return;

        // 우상단 라벨 + 격자 배경
        var panel = UIBuilder.NewUI("CoopPartnerPanel", canvas, typeof(RectTransform), typeof(Image));
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(1f, 1f);
        prt.pivot = new Vector2(1f, 1f);
        prt.sizeDelta = new Vector2(W * Cell + 16, H * Cell + 44);
        prt.anchoredPosition = new Vector2(-16, -16);
        panel.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.1f, 0.85f);

        var label = UIBuilder.Text(panel.transform, "Label", "상대", 20, new Color(0.9f, 0.9f, 0.95f), TextAnchor.UpperCenter);
        UIBuilder.SetAnchors(label.rectTransform, new Vector2(0f, 0.88f), new Vector2(1f, 1f));
        label.raycastTarget = false;

        var gridGO = UIBuilder.NewUI("Grid", panel.transform, typeof(RectTransform));
        _host = (RectTransform)gridGO.transform;
        _host.anchorMin = _host.anchorMax = new Vector2(0f, 0f);   // 좌하단 기준으로 셀 배치
        _host.pivot = new Vector2(0f, 0f);
        _host.anchoredPosition = new Vector2(8, 8);
        _host.sizeDelta = new Vector2(W * Cell, H * Cell);
    }

    public void Render(int[] flat)
    {
        if (_host == null || flat == null) return;
        foreach (var g in _cells) if (g != null) Destroy(g);
        _cells.Clear();

        for (int i = 0; i < flat.Length; i++)
        {
            int colorID = flat[i];
            if (colorID == 0) continue;
            int x = i % W, y = i / W;
            var img = UIBuilder.Image(_host, "c", BlockColors.Get(colorID));
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(Cell - 1, Cell - 1);
            rt.anchoredPosition = new Vector2(x * Cell, y * Cell);
            _cells.Add(img.gameObject);
        }
    }
}
