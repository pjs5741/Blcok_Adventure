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
        UIScale.FixAll();   //--- 2026-07-03 캔버스 스케일 통일(창 크기 달라도 UI 안 깨지게)
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

        if (CoopSession.Active)
        {
            //--- 2026-07-03 협동 맵: 세이브 없음. 서버 이벤트 구독(방장 진행 결과 goNode, 참여자 의견 emote).
            var c = CoopClient.Instance;
            if (c != null)
            {
                c.OnGoNode += HandleGoNode;
                c.OnMapEmote += HandleEmote;
                c.OnAdvanceReady += HandleAdvanceReady;       // 둘 다 완료 → 진행 가능
                c.OnWaitPartnerNode += HandleWaitPartnerNode; // 상대 아직 진행 중
                //--- 2026-07-07 맵 복귀 = 현재 노드 완료 신호. 둘 다 완료해야 방장이 다음 진행 가능.
                _coopCanAdvance = false;
                c.SendNodeDone();
            }
            ShowCoopHint();
        }
        else
        {
            //--- 2026-07-01 노드 단위 자동 저장(이어하기). 맵에 올 때마다 현재 진행 상태 저장.
            SaveSystem.Save();
        }
    }

    //--- 2026-07-03 협동 맵 처리 ----
    void HandleGoNode(int nodeId, int hp, int maxHp, int seed)
    {
        Run.mapState.EnterNode(nodeId);
        var node = Run.mapState.GetNode(nodeId);
        NodeType t = node != null ? node.type : NodeType.Battle;
        switch (t)
        {
            case NodeType.Shop:  SceneManager.LoadScene("ShopScene"); break;
            case NodeType.Rest:  SceneManager.LoadScene("RestScene"); break;
            case NodeType.Event: ShowEvent(); break;   // 맵 오버레이(각자 개별 해결)
            default:                                    // Start/Battle/Elite/Boss
                CoopSession.MonsterHp = hp;
                CoopSession.MonsterMaxHp = maxHp;
                CoopSession.MonsterSeed = seed;
                SceneManager.LoadScene("GameScene");
                break;
        }
    }

    void HandleEmote(string from, int nodeId)
    {
        if (_mapContent == null) return;
        var target = _mapContent.Find($"Node_{nodeId}");
        if (target == null) return;
        var go = new GameObject("Emote", typeof(RectTransform), typeof(Text));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(target, false);
        var txt = go.GetComponent<Text>();
        txt.text = $"{from} 여기!";
        txt.font = UIFont.Regular; txt.fontSize = 30; txt.color = new Color(1f, 0.9f, 0.4f);
        txt.alignment = TextAnchor.MiddleCenter; txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0, 8); rt.sizeDelta = new Vector2(200, 44);
        Destroy(go, 2.5f);
    }

    int ComputeCoopHp(NodeType type)
    {
        var p = MonsterRegistry.GetForNode(type);
        float hp = p != null ? p.maxHp : 300f;
        return Mathf.RoundToInt(hp * 2f);   // 2인 협동 보정
    }

    private bool _coopCanAdvance;   //--- 2026-07-07 둘 다 노드 완료 시 true → 방장 진행 가능
    private Text _coopHint;

    void ShowCoopHint()
    {
        var canvas = mapPanel != null ? mapPanel.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        _coopHint = canvas.transform.Find("CoopHint")?.GetComponent<Text>();
        if (_coopHint == null)
        {
            _coopHint = UIBuilder.Text(canvas.transform, "CoopHint", "", 26, new Color(0.8f, 0.9f, 1f), TextAnchor.MiddleCenter);
            UIBuilder.SetAnchors(_coopHint.rectTransform, new Vector2(0.15f, 0.94f), new Vector2(0.85f, 0.99f));
        }
        UpdateCoopHint();
    }

    void UpdateCoopHint()
    {
        if (_coopHint == null) return;
        bool host = CoopSession.PlayerId == 0;
        if (!_coopCanAdvance) _coopHint.text = "상대가 아직 진행 중 — 둘 다 완료해야 진행돼요";
        else _coopHint.text = host ? "협동 — 방장: 노드를 눌러 진행" : "협동 — 참여자: 노드 누르면 의견(진행은 방장)";
    }

    void HandleAdvanceReady() { _coopCanAdvance = true; UpdateCoopHint(); }        // 둘 다 노드 완료
    void HandleWaitPartnerNode() { _coopCanAdvance = false; UpdateCoopHint(); }    // 상대 아직 진행 중

    void OnDestroy()
    {
        var c = CoopClient.Instance;
        if (c != null)
        {
            c.OnGoNode -= HandleGoNode; c.OnMapEmote -= HandleEmote;
            c.OnAdvanceReady -= HandleAdvanceReady; c.OnWaitPartnerNode -= HandleWaitPartnerNode;
        }
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

        //--- 2026-07-03 맵 스크롤+확대: 노드/선을 큰 콘텐츠(_mapContent)에 담고 MapPanel을 뷰포트로 스크롤
        var content = EnsureMapContent();
        foreach (Transform child in content) Destroy(child.gameObject);

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

        ScrollToCurrent();   //--- 2026-07-03 진행 위치(현재 노드, 없으면 START)로 자동 스크롤
    }

    // 현재 노드(진행 위치)가 뷰포트 중앙에 오도록 스크롤. 시작 전이면 START(레이어0).
    void ScrollToCurrent()
    {
        var scroll = mapPanel.GetComponent<ScrollRect>();
        if (scroll == null || _mapContent == null) return;

        MapNode target = Run.mapState.GetNode(Run.mapState.currentNodeId);
        if (target == null) target = Run.mapState.nodes.Find(n => n.layer == 0);
        if (target == null) return;

        Canvas.ForceUpdateCanvases();
        Vector2 p = target.uiPosition * MapZoom;
        float W = _mapContent.sizeDelta.x, H = _mapContent.sizeDelta.y;
        var vp = (RectTransform)mapPanel;
        float vpW = vp.rect.width, vpH = vp.rect.height;

        float contentX = p.x + W * 0.5f;   // 콘텐츠 좌측 기준 위치
        float contentY = p.y + H * 0.5f;   // 콘텐츠 하단 기준 위치
        scroll.horizontalNormalizedPosition = (W - vpW) > 1f ? Mathf.Clamp01((contentX - vpW * 0.5f) / (W - vpW)) : 0f;
        scroll.verticalNormalizedPosition   = (H - vpH) > 1f ? Mathf.Clamp01((contentY - vpH * 0.5f) / (H - vpH)) : 0.5f;
    }

    //--- 2026-07-03 맵 확대 배율/노드 크기 + 스크롤 콘텐츠
    const float MapZoom = 1.7f;
    const float NodeSize = 150f;
    RectTransform _mapContent;

    RectTransform EnsureMapContent()
    {
        if (_mapContent != null) return _mapContent;

        var scroll = mapPanel.GetComponent<ScrollRect>() ?? mapPanel.gameObject.AddComponent<ScrollRect>();
        if (mapPanel.GetComponent<RectMask2D>() == null) mapPanel.gameObject.AddComponent<RectMask2D>();

        var go = new GameObject("MapContent", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        _mapContent = (RectTransform)go.transform;
        _mapContent.SetParent(mapPanel, false);
        _mapContent.anchorMin = _mapContent.anchorMax = new Vector2(0.5f, 0.5f);
        _mapContent.pivot = new Vector2(0.5f, 0.5f);
        _mapContent.anchoredPosition = Vector2.zero;

        float maxX = 0f, maxY = 0f;
        foreach (var n in Run.mapState.nodes) { maxX = Mathf.Max(maxX, Mathf.Abs(n.uiPosition.x)); maxY = Mathf.Max(maxY, Mathf.Abs(n.uiPosition.y)); }
        _mapContent.sizeDelta = new Vector2(maxX * 2f * MapZoom + NodeSize + 240f, maxY * 2f * MapZoom + NodeSize + 240f);

        scroll.content = _mapContent;
        scroll.viewport = mapPanel;
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;   // 내용이 뷰포트 넘을 때만 스크롤(넘으면 보스쪽으로)
        scroll.scrollSensitivity = 40f;
        return _mapContent;   // 초기/진행 스크롤 위치는 BuildMapUI 끝의 ScrollToCurrent가 처리
    }

    // 두 노드를 잇는 선 (회전한 얇은 Image). 노드보다 뒤에 그려짐.
    void CreateEdge(Vector2 a, Vector2 b, bool highlight)
    {
        GameObject line = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(_mapContent, false);
        line.layer = LayerMask.NameToLayer("UI");

        Vector2 az = a * MapZoom, bz = b * MapZoom;
        RectTransform rt = line.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 dir = bz - az;
        rt.sizeDelta = new Vector2(dir.magnitude, highlight ? 12f : 7f);
        rt.anchoredPosition = (az + bz) * 0.5f;
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        Image img = line.GetComponent<Image>();
        img.color = highlight ? new Color(1f, 0.85f, 0.3f, 0.95f) : new Color(0.5f, 0.5f, 0.55f, 0.5f);
        img.raycastTarget = false;
    }

    void CreateNodeButton(MapNode node, bool isAvailable, bool isCompleted)
    {
        GameObject go = new GameObject($"Node_{node.id}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_mapContent, false);
        go.layer = LayerMask.NameToLayer("UI");

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);   // 중앙 기준(선 그리기와 좌표 일치)
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = node.uiPosition * MapZoom;
        rt.sizeDelta = new Vector2(NodeSize, NodeSize);

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
        //--- 2026-07-03 협동: 방장은 노드 선택(진행), 참여자는 이모트(의견). 실제 진행은 서버 goNode로.
        if (CoopSession.Active)
        {
            var c = CoopClient.Instance;
            if (c == null) return;
            if (CoopSession.PlayerId == 0)
            {
                //--- 2026-07-07 둘 다 현재 노드 완료해야 다음 진행. 아직이면 대기 안내.
                if (!_coopCanAdvance) { HandleWaitPartnerNode(); return; }

                //--- 2026-07-06 노드 타입에 따라 전투/비전투(상점·휴식·이벤트) 라우팅. 전투면 몬스터 시드로 동기화.
                bool isBattle = node.type == NodeType.Start || node.type == NodeType.Battle
                             || node.type == NodeType.Elite || node.type == NodeType.Boss;
                int seed = UnityEngine.Random.Range(1, int.MaxValue);
                int hp = 0, interval = 3;
                string[] intents = null;   //--- 2026-07-20 협동도 솔로처럼 몹별 인텐트 풀 사용 → 방장이 뽑아 서버로 전송
                if (isBattle)
                {
                    var prof = MonsterRegistry.GetForNode(node.type, seed);
                    hp = Mathf.RoundToInt((prof != null ? prof.maxHp : 300f) * 2f);
                    interval = prof != null ? prof.attackInterval : 3;
                    //--- 판 회전(Pivot)은 협동 미지원(그리드 크기 변경 → 파트너 미니뷰 불가)이라 풀에서 제외
                    if (prof != null && prof.intentPool != null)
                    {
                        var list = new System.Collections.Generic.List<string>();
                        foreach (var it in prof.intentPool)
                            if (it != MonsterIntent.Pivot) list.Add(it.ToString());
                        intents = list.ToArray();
                    }
                }
                c.MapSelect(node.id, isBattle, hp, seed, interval, intents);
            }
            else c.MapEmote(node.id);
            return;
        }

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
