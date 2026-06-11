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
    }

    void UpdateGoldUI()
    {
        if (goldText != null) goldText.text = $"💰 {Run.stats.gold}";
    }

    void BuildMapUI()
    {
        if (mapPanel == null) { Debug.LogError("MapPanel 못 찾음"); return; }

        foreach (Transform child in mapPanel) Destroy(child.gameObject);

        var available = new HashSet<int>(Run.mapState.GetAvailableNextNodes());

        foreach (var node in Run.mapState.nodes)
            CreateNodeButton(node, available.Contains(node.id), Run.mapState.completedNodeIds.Contains(node.id));
    }

    void CreateNodeButton(MapNode node, bool isAvailable, bool isCompleted)
    {
        GameObject go = new GameObject($"Node_{node.id}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(mapPanel, false);
        go.layer = LayerMask.NameToLayer("UI");

        RectTransform rt = go.GetComponent<RectTransform>();
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
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        }
    }
}
