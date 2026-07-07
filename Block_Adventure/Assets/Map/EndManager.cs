using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class EndManager : MonoBehaviour
{
    void Start()
    {
        //--- 2026-07-01 런 종료(승/패) → 세이브 삭제(끝난 런을 이어하기로 부활시키지 않음)
        SaveSystem.Delete();
        UIScale.FixAll();   //--- 2026-07-06 캔버스 스케일(글씨 안 보이던 문제)
        SetupCanvas();
    }

    void SetupCanvas()
    {
        var canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogError("EndScene Canvas 없음"); return; }
        Transform canvas = canvasGO.transform;

        bool victory = Run.lastResult == RunResult.Victory;
        string title = victory ? "라운드 클리어!" : "GAME OVER";
        Color titleColor = victory ? new Color(1f, 0.85f, 0.3f) : new Color(0.9f, 0.2f, 0.2f);

        var t = CreateText(canvas, "Title", new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.85f), title, 140, titleColor);
        t.alignment = TextAnchor.MiddleCenter;

        if (Run.stats != null)
        {
            CreateText(canvas, "Stats",
                new Vector2(0.2f, 0.35f), new Vector2(0.8f, 0.55f),
                $"보유 골드: {Run.stats.gold}\n보유 유물: {(Run.ownedRelics?.Count ?? 0)}개\n덱 블록 수: {(Run.deck?.Count ?? 0)}",
                50, Color.white);
        }

        CreateButton(canvas, "RestartButton",
            new Vector2(0.35f, 0.1f), new Vector2(0.65f, 0.25f),
            "새 라운드 시작",
            new Color(0.3f, 0.55f, 0.35f),
            OnRestart);
    }

    void OnRestart()
    {
        Run.StartNew();
        Run.lastResult = RunResult.None;
        SceneManager.LoadScene("MapScene");
    }

    Text CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string text, int fontSize, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        Text t = go.GetComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.font = UIFont.Regular;
        return t;
    }

    void CreateButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string label, Color bgColor, System.Action onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        go.GetComponent<Image>().color = bgColor;
        go.GetComponent<Button>().onClick.AddListener(() => onClick());

        GameObject lbl = new GameObject("Label", typeof(RectTransform), typeof(Text));
        lbl.layer = LayerMask.NameToLayer("UI");
        lbl.transform.SetParent(go.transform, false);
        RectTransform lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.sizeDelta = Vector2.zero;
        Text t = lbl.GetComponent<Text>();
        t.text = label;
        t.fontSize = 50;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.font = UIFont.Regular;
    }
}
