using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

//--- 2026-06-30 UI 생성 공용 헬퍼. 매니저마다 중복 구현하던 Text/Rect/Button 생성을 한 곳으로.
// (장기적으로는 프리팹화가 맞지만, 우선 코드 중복 제거 + 일관성 확보)
public static class UIBuilder
{
    public static GameObject NewUI(string name, Transform parent, params System.Type[] comps)
    {
        var go = new GameObject(name, comps);
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    public static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min; rt.anchorMax = max;
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = Vector2.zero;
    }

    public static void Stretch(RectTransform rt) => SetAnchors(rt, Vector2.zero, Vector2.one);

    public static Text Text(Transform parent, string name, string content, int fontSize, Color color, TextAnchor align)
    {
        var go = NewUI(name, parent, typeof(RectTransform), typeof(Text));
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.font = UIFont.Regular;
        return t;
    }

    public static Image Image(Transform parent, string name, Color color)
    {
        var go = NewUI(name, parent, typeof(RectTransform), typeof(Image));
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    // 버튼(Image+Button) + 가운데 라벨. label이 null이면 라벨 없음.
    public static Button Button(Transform parent, string name, string label, int labelSize, UnityAction onClick, Color bg)
    {
        var go = NewUI(name, parent, typeof(RectTransform), typeof(Image), typeof(Button));
        go.GetComponent<Image>().color = bg;
        var btn = go.GetComponent<Button>();
        if (onClick != null) btn.onClick.AddListener(onClick);
        if (label != null)
        {
            var t = Text(go.transform, "Label", label, labelSize, Color.white, TextAnchor.MiddleCenter);
            Stretch(t.rectTransform);
        }
        return btn;
    }

    // 자식에서 name을 찾으면 그 RectTransform, 없으면 생성(앵커 지정)
    public static RectTransform FindOrCreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<RectTransform>();
        var go = NewUI(name, parent, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        SetAnchors(rt, anchorMin, anchorMax);
        return rt;
    }

    public static Text FindOrCreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize, TextAnchor align)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<Text>();
        var t = Text(parent, name, "", fontSize, Color.white, align);
        SetAnchors(t.rectTransform, anchorMin, anchorMax);
        return t;
    }

    // 자식 텍스트(앵커 지정, 가운데 정렬)
    public static Text ChildText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string content, int fontSize, Color color)
    {
        var t = Text(parent, name, content, fontSize, color, TextAnchor.MiddleCenter);
        SetAnchors(t.rectTransform, anchorMin, anchorMax);
        return t;
    }
}
