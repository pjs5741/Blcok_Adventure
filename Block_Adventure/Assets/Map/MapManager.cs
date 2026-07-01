using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{
    public RectTransform mapPanel;
    public Text goldText;

    void Start()
    {
        if (!Run.IsInitialized) Run.StartNew();
        if (mapPanel == null)
            mapPanel = GameObject.Find("MapPanel")?.GetComponent<RectTransform>();
        if (goldText == null)
        {
            var go = GameObject.Find("GoldText");
            if (go != null) goldText = go.GetComponent<Text>();
        }

        // 전 씬(배틀/상점)에서 돌아온 거라면 현재 노드 완료 처리
        Run.mapState.CompleteCurrentNode();

        UpdateGoldUI();
        BuildMapUI();
        CreateDeckButton();
        CreateHelpButton();

        //--- 2026-07-01 노드 단위 자동 저장(이어하기). 맵에 올 때마다 현재 진행 상태 저장.
        SaveSystem.Save();
    }

    //--- 2026-07-01 튜토리얼 재열람 버튼
    void CreateHelpButton()
    {
        var canvas = mapPanel != null ? mapPanel.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
        if (canvas == null || canvas.transform.Find("HelpBtn") != null) return;
        var btn = UIBuilder.Button(canvas.transform, "HelpBtn", "도움말", 34,
            () => TutorialOverlay.Create(canvas.transform), new Color(0.3f, 0.5f, 0.4f));
        UIBuilder.SetAnchors((RectTransform)btn.transform, new Vector2(0.2f, 0.02f), new Vector2(0.34f, 0.09f));
    }

    //--- 2026-06-30 맵에서 덱 보기 (보기 전용 DeckEditPanel)
    void CreateDeckButton()
    {
        var canvas = mapPanel != null ? mapPanel.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
        if (canvas == null || canvas.transform.Find("DeckViewBtn") != null) return;
        var btn = UIBuilder.Button(canvas.transform, "DeckViewBtn", "덱 보기", 34, OpenDeckView, new Color(0.25f, 0.4f, 0.55f));
        UIBuilder.SetAnchors((RectTransform)btn.transform, new Vector2(0.02f, 0.02f), new Vector2(0.18f, 0.09f));
    }

    void OpenDeckView()
    {
        var canvas = mapPanel != null ? mapPanel.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        var go = new GameObject("DeckView");
        go.AddComponent<DeckEditPanel>().Open(canvas.transform, 0, 0, null);   // 보기 전용
    }

    void UpdateGoldUI()
    {
        if (goldText != null) goldText.text = $"골드 {Run.stats.gold}";
    }

    void BuildMapUI()
    {
        if (mapPanel == null) { Debug.LogError("MapPanel 못 찾음"); return; }

        foreach (Transform child in mapPanel) Destroy(child.gameObject);

        var available = new HashSet<int>(Run.mapState.GetAvailableNextNodes());

        //--- 2026-07-01 [Slay the Spire식] 노드 사이 연결선 먼저 그림(노드 뒤). 현재 노드→선택 가능 경로는 강조.
        int cur = Run.mapState.currentNodeId;
        foreach (var node in Run.mapState.nodes)
            foreach (int nextId in node.nextNodeIds)
            {
                var next = Run.mapState.GetNode(nextId);
                if (next == null) continue;
                bool highlight = node.id == cur;   // 노드 진입 후에만 경로 강조(START 상태에선 노란선 없음)
                CreateEdge(node.uiPosition, next.uiPosition, highlight);
            }

        foreach (var node in Run.mapState.nodes)
            CreateNodeButton(node, available.Contains(node.id), Run.mapState.completedNodeIds.Contains(node.id));
    }

    // 두 노드를 잇는 선 (회전한 얇은 Image). 노드보다 뒤에 그려짐.
    void CreateEdge(Vector2 a, Vector2 b, bool highlight)
    {
        GameObject line = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(mapPanel, false);
        line.layer = LayerMask.NameToLayer("UI");

        RectTransform rt = line.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 dir = b - a;
        rt.sizeDelta = new Vector2(dir.magnitude, highlight ? 10f : 6f);
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        Image img = line.GetComponent<Image>();
        img.color = highlight ? new Color(1f, 0.85f, 0.3f, 0.95f) : new Color(0.5f, 0.5f, 0.55f, 0.5f);
        img.raycastTarget = false;
    }

    void CreateNodeButton(MapNode node, bool isAvailable, bool isCompleted)
    {
        GameObject go = new GameObject($"Node_{node.id}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(mapPanel, false);
        go.layer = LayerMask.NameToLayer("UI");

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);   // 중앙 기준(선 그리기와 좌표 일치)
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = node.uiPosition;
        rt.sizeDelta = new Vector2(100, 100);

        Image img = go.GetComponent<Image>();
        img.color = GetNodeColor(node.type, isAvailable, isCompleted);

        Button btn = go.GetComponent<Button>();
        btn.interactable = isAvailable;
        if (isAvailable)
            btn.onClick.AddListener(() => OnNodeClicked(node));

        CreateLabel(go.transform, GetNodeLabel(node.type));
    }

    void CreateLabel(Transform parent, string text)
    {
        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(parent, false);
        Text label = labelGo.GetComponent<Text>();
        label.text = text;
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 40;
        label.color = Color.white;
        label.font = UIFont.Regular;
        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.anchoredPosition = Vector2.zero;
        labelRt.sizeDelta = Vector2.zero;
    }

    Color GetNodeColor(NodeType type, bool available, bool completed)
    {
        if (completed) return new Color(0.3f, 0.3f, 0.3f, 1f);
        if (!available) return new Color(0.4f, 0.4f, 0.4f, 0.7f);

        switch (type)
        {
            case NodeType.Start: return new Color(0.8f, 0.8f, 0.8f);
            case NodeType.Battle: return new Color(0.7f, 0.3f, 0.3f);
            case NodeType.Elite: return new Color(0.5f, 0.1f, 0.6f);
            case NodeType.Shop: return new Color(0.85f, 0.7f, 0.2f);
            case NodeType.Boss: return new Color(0.3f, 0.05f, 0.05f);
            case NodeType.Rest: return new Color(0.3f, 0.6f, 0.4f);
            case NodeType.Event: return new Color(0.25f, 0.5f, 0.7f);
            default: return Color.white;
        }
    }

    string GetNodeLabel(NodeType type)
    {
        switch (type)
        {
            case NodeType.Start: return "START";
            case NodeType.Battle: return "전투";
            case NodeType.Elite: return "엘리트";
            case NodeType.Shop: return "상점";
            case NodeType.Boss: return "보스";
            case NodeType.Rest: return "휴식";
            case NodeType.Event: return "이벤트";
            default: return "?";
        }
    }

    void OnNodeClicked(MapNode node)
    {
        Run.mapState.EnterNode(node.id);
        Debug.Log($"노드 진입: {node.type} (id={node.id})");

        switch (node.type)
        {
            case NodeType.Start:
            case NodeType.Battle:
            case NodeType.Elite:
            case NodeType.Boss:
                SceneManager.LoadScene("GameScene");
                break;
            case NodeType.Shop:
                SceneManager.LoadScene("ShopScene");
                break;
            case NodeType.Rest:
                SceneManager.LoadScene("RestScene");
                break;
            case NodeType.Event:
                ShowEvent();   // 씬 전환 없이 MapScene 위 전체화면 오버레이로 처리
                break;
        }
    }

    //--- 2026-06-29 이벤트 노드: 별도 씬 대신 오버레이 패널. 선택 해결 후 노드 완료 처리 + 맵 갱신
    void ShowEvent()
    {
        Canvas canvas = mapPanel != null ? mapPanel.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("이벤트: Canvas 못 찾음"); return; }

        GameObject go = new GameObject("EventPanel");
        go.transform.SetParent(canvas.transform, false);
        var panel = go.AddComponent<EventPanel>();
        panel.Show(EventRegistry.GetRandom(), () =>
        {
            Destroy(go);
            Run.mapState.CompleteCurrentNode();
            UpdateGoldUI();
            BuildMapUI();
        });
    }
}
