using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class RestManager : MonoBehaviour
{
    [Header("Settings")]
    public int maxClears = 3;
    public int gridWidth = 11;
    public int gridHeight = 21;

    private int clearsRemaining;
    private Text counterText;
    private RectTransform rowButtonContainer;
    private RectTransform gridDisplay;

    void Start()
    {
        if (!Run.IsInitialized) Run.StartNew();
        if (Run.gridSnapshot == null) Run.gridSnapshot = new int[gridWidth, gridHeight];

        clearsRemaining = maxClears;

        SetupCanvas();
        BuildUI();
    }

    void SetupCanvas()
    {
        var canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogError("RestScene Canvas 없음"); return; }
        Transform canvas = canvasGO.transform;

        counterText = FindOrCreateText(canvas, "CounterText", new Vector2(0.3f, 0.92f), new Vector2(0.7f, 1f), 50, TextAnchor.MiddleCenter);
        gridDisplay = FindOrCreateRect(canvas, "GridDisplay", new Vector2(0.05f, 0.05f), new Vector2(0.55f, 0.9f));
        rowButtonContainer = FindOrCreateRect(canvas, "RowButtons", new Vector2(0.6f, 0.05f), new Vector2(0.95f, 0.9f));

        CreateDoneButton(canvas);
    }

    RectTransform FindOrCreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<RectTransform>();

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    Text FindOrCreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize, TextAnchor align)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<Text>();

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        Text t = go.GetComponent<Text>();
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }

    void CreateDoneButton(Transform parent)
    {
        if (parent.Find("DoneButton") != null) return;

        GameObject go = new GameObject("DoneButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.4f, 0.92f);
        rt.anchorMax = new Vector2(0.6f, 0.99f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.4f, 0.4f, 0.4f);

        Button btn = go.GetComponent<Button>();
        btn.onClick.AddListener(ReturnToMap);

        // label
        GameObject lbl = new GameObject("Label", typeof(RectTransform), typeof(Text));
        lbl.transform.SetParent(go.transform, false);
        RectTransform lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.sizeDelta = Vector2.zero;
        Text lt = lbl.GetComponent<Text>();
        lt.text = "휴식 종료";
        lt.alignment = TextAnchor.MiddleCenter;
        lt.fontSize = 40;
        lt.color = Color.white;
        lt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void BuildUI()
    {
        if (gridDisplay != null)
        {
            foreach (Transform c in gridDisplay) Destroy(c.gameObject);
            BuildGridDisplay();
        }
        if (rowButtonContainer != null)
        {
            foreach (Transform c in rowButtonContainer) Destroy(c.gameObject);
            BuildRowButtons();
        }
        UpdateCounter();
    }

    void BuildGridDisplay()
    {
        float cellW = 1f / gridWidth;
        float cellH = 1f / gridHeight;

        for (int y = 0; y < gridHeight; y++)
            for (int x = 0; x < gridWidth; x++)
            {
                int colorID = Run.gridSnapshot[x, y];
                if (colorID == 0) continue;

                GameObject cell = new GameObject($"Cell_{x}_{y}", typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(gridDisplay, false);
                cell.layer = LayerMask.NameToLayer("UI");

                RectTransform rt = cell.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(x * cellW, y * cellH);
                rt.anchorMax = new Vector2((x + 1) * cellW, (y + 1) * cellH);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(-2, -2);

                Image img = cell.GetComponent<Image>();
                img.color = ColorByID(colorID);
            }
    }

    void BuildRowButtons()
    {
        float btnH = 1f / gridHeight;
        for (int y = 0; y < gridHeight; y++)
        {
            int row = y;
            int blocks = 0;
            for (int x = 0; x < gridWidth; x++) if (Run.gridSnapshot[x, y] != 0) blocks++;

            GameObject btnGO = new GameObject($"Row_{y}", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(rowButtonContainer, false);
            btnGO.layer = LayerMask.NameToLayer("UI");

            RectTransform rt = btnGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, y * btnH);
            rt.anchorMax = new Vector2(1, (y + 1) * btnH);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(-4, -4);

            Image img = btnGO.GetComponent<Image>();
            bool empty = blocks == 0;
            img.color = empty ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.3f, 0.5f, 0.4f);

            Button btn = btnGO.GetComponent<Button>();
            btn.interactable = !empty && clearsRemaining > 0;
            btn.onClick.AddListener(() => ClearRow(row));

            GameObject lbl = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lbl.transform.SetParent(btnGO.transform, false);
            RectTransform lrt = lbl.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.sizeDelta = Vector2.zero;
            Text lt = lbl.GetComponent<Text>();
            lt.text = $"R{y} ({blocks})";
            lt.alignment = TextAnchor.MiddleCenter;
            lt.fontSize = 24;
            lt.color = Color.white;
            lt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    void ClearRow(int y)
    {
        if (clearsRemaining <= 0) return;
        for (int x = 0; x < gridWidth; x++) Run.gridSnapshot[x, y] = 0;
        clearsRemaining--;
        BuildUI();
        if (clearsRemaining <= 0) ReturnToMap();
    }

    void UpdateCounter()
    {
        if (counterText != null)
            counterText.text = $"휴식 — 정리할 줄 선택 (남은: {clearsRemaining}/{maxClears})";
    }

    void ReturnToMap()
    {
        SceneManager.LoadScene("MapScene");
    }

    Color ColorByID(int id)
    {
        switch (id)
        {
            case 1: return Color.red;
            case 2: return Color.blue;
            case 3: return Color.green;
            case 4: return Color.yellow;
            case 5: return new Color(0.6f, 0.2f, 0.9f);
            case 99: return Color.gray;
            default: return Color.white;
        }
    }
}
