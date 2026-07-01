using System;
using UnityEngine;
using UnityEngine.UI;

//--- 2026-06-29 이벤트 노드 전체화면 오버레이. MapManager가 Canvas 아래 생성해 Show() 호출.
// 제목 + 본문 + 선택지 버튼 → 선택 시 효과 적용 → 결과 텍스트 + 계속 버튼 → onClose 콜백.
// 배경은 단색 플레이스홀더(나중 EventDef.backgroundName으로 스프라이트 교체).
public class EventPanel : MonoBehaviour
{
    private Font _font;
    private Action _onClose;
    private RectTransform _choicesBox;
    private Text _bodyText;
    private Text _resultText;
    private GameObject _continueButton;

    public void Show(EventDef def, Action onClose)
    {
        _onClose = onClose;
        _font = UIFont.Regular;

        // 패널 자체를 전체화면으로
        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        Stretch(rt);

        // 배경(클릭 차단 + 플레이스홀더 색). 나중에 sprite 교체 지점.
        var bg = NewUI("BG", transform, typeof(Image));
        Stretch(bg.GetComponent<RectTransform>());
        var bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0.06f, 0.08f, 0.12f, 0.97f);
        // if (!string.IsNullOrEmpty(def.backgroundName)) bgImg.sprite = Resources.Load<Sprite>(def.backgroundName);

        // 제목
        var title = NewText("Title", transform, def.title, 56, TextAnchor.MiddleCenter);
        Anchor(title.rectTransform, new Vector2(0.1f, 0.80f), new Vector2(0.9f, 0.93f));
        title.color = new Color(0.95f, 0.9f, 0.6f);

        // 본문
        _bodyText = NewText("Body", transform, def.body, 34, TextAnchor.UpperCenter);
        Anchor(_bodyText.rectTransform, new Vector2(0.12f, 0.55f), new Vector2(0.88f, 0.78f));

        // 선택지 컨테이너
        var box = NewUI("Choices", transform, typeof(RectTransform));
        _choicesBox = box.GetComponent<RectTransform>();
        Anchor(_choicesBox, new Vector2(0.2f, 0.10f), new Vector2(0.8f, 0.5f));
        BuildChoices(def);

        // 결과 텍스트(처음엔 숨김)
        _resultText = NewText("Result", transform, "", 34, TextAnchor.UpperCenter);
        Anchor(_resultText.rectTransform, new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.74f));
        _resultText.gameObject.SetActive(false);

        // 계속 버튼(처음엔 숨김)
        _continueButton = NewButton("Continue", transform, "계속", () => _onClose?.Invoke());
        Anchor((RectTransform)_continueButton.transform, new Vector2(0.35f, 0.10f), new Vector2(0.65f, 0.20f));
        _continueButton.SetActive(false);
    }

    void BuildChoices(EventDef def)
    {
        int n = def.choices.Count;
        float slot = 1f / n;
        for (int i = 0; i < n; i++)
        {
            EventChoice c = def.choices[i];
            bool affordable = Run.stats.gold >= c.goldCost;
            string label = c.label + (affordable || c.goldCost == 0 ? "" : "  (골드 부족)");

            var btnGO = NewButton($"Choice{i}", _choicesBox, label, () => OnChoose(c));
            float top = 1f - i * slot;
            Anchor((RectTransform)btnGO.transform, new Vector2(0.02f, top - slot + 0.02f), new Vector2(0.98f, top - 0.02f));
            var btn = btnGO.GetComponent<Button>();
            btn.interactable = affordable;
            btnGO.GetComponent<Image>().color = affordable ? new Color(0.2f, 0.35f, 0.5f) : new Color(0.2f, 0.2f, 0.2f);
        }
    }

    void OnChoose(EventChoice c)
    {
        string detail = EventRegistry.Apply(c);

        if (_choicesBox != null) _choicesBox.gameObject.SetActive(false);
        if (_bodyText != null) _bodyText.gameObject.SetActive(false);

        _resultText.text = c.resultText + (string.IsNullOrEmpty(detail) ? "" : "\n\n" + detail);
        _resultText.gameObject.SetActive(true);
        _continueButton.SetActive(true);
    }

    // ---- UI 헬퍼 ----

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    GameObject NewUI(string name, Transform parent, params Type[] comps)
    {
        var go = new GameObject(name, comps);
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    Text NewText(string name, Transform parent, string content, int fontSize, TextAnchor align)
    {
        var go = NewUI(name, parent, typeof(RectTransform), typeof(Text));
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.font = _font;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    GameObject NewButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = NewUI(name, parent, typeof(RectTransform), typeof(Image), typeof(Button));
        go.GetComponent<Image>().color = new Color(0.2f, 0.35f, 0.5f);
        go.GetComponent<Button>().onClick.AddListener(onClick);

        var lbl = NewText("Label", go.transform, label, 30, TextAnchor.MiddleCenter);
        Stretch(lbl.rectTransform);
        return go;
    }
}
