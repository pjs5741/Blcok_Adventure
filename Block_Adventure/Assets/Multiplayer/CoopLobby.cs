using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

//--- 2026-07-03 협동 로비/방 UI. 타이틀에서 "협동" → 서버 접속 → 로비(방 목록/생성/참가 + 채팅) → 방(준비/시작 + 채팅) → gameStart 시 전투.
public class CoopLobby : MonoBehaviour
{
    private CoopClient _c;
    private GameObject _root, _lobbyPanel, _roomPanel;
    private Text _myId, _status, _lobbyLog, _roomInfo, _roomLog;
    private RectTransform _roomList;
    private InputField _lobbyInput, _roomInput;
    private Button _readyBtn, _startBtn;
    private bool _isHost, _ready;

    //--- 2026-07-03 연결 타임아웃: hello(서버 응답)가 이 시간 안 안 오면 실패로 간주
    const float ConnectTimeout = 6f;
    private float _connectTimer;
    private bool _helloReceived, _timedOut;

    public void Begin(Transform canvas)
    {
        Build(canvas);
        _c = CoopClient.GetOrCreate();
        _c.OnHello       += id => { _helloReceived = true; _myId.text = $"내 ID: {id}"; SetStatus("로비 연결됨"); };
        _c.OnLobby       += RenderRooms;
        _c.OnRoomJoined  += (roomId, host, idx) => { _isHost = host; _ready = false; ShowRoom(roomId); };
        _c.OnRoomUpdate  += RenderRoom;
        _c.OnLobbyChat   += (f, t) => Append(_lobbyLog, $"{f}: {t}");
        _c.OnRoomChat    += (f, t) => Append(_roomLog, $"{f}: {t}");
        _c.OnJoinFail    += () => SetStatus("방 참가 실패(가득 찼거나 없음)");
        _c.OnPartnerLeft += () => { SetStatus("상대가 나갔습니다"); _c.LeaveRoom(); ShowLobby(); };
        _c.OnClosed      += () => SetStatus("서버 연결 끊김 — 서버가 켜져 있는지 확인");
        _c.OnGameStart   += OnGameStart;

        SetStatus("서버 연결 중...");
        _c.Connect(CoopSession.GetUrl());
        ShowLobby();
    }

    void Update()
    {
        if (_helloReceived || _timedOut) return;
        _connectTimer += Time.unscaledDeltaTime;
        if (_connectTimer >= ConnectTimeout)
        {
            _timedOut = true;
            SetStatus($"서버 연결 실패 ({ConnectTimeout:0}초 초과) — 서버(8888)가 켜져 있는지 확인하세요.");
        }
    }

    void OnGameStart(CoopClient.GameStartData d)
    {
        CoopSession.Active = true;
        CoopSession.PlayerId = d.playerIndex;
        CoopSession.Seed = d.seed;
        Unsubscribe();
        Run.StartNew();                                    // 협동 새 런(덱/스탯/유물 초기화)
        Run.mapState = MapState.GenerateRandom(d.seed);    // 시드로 공유 맵(양쪽 동일)
        SceneManager.LoadScene("MapScene");
    }

    // ---------- 렌더 ----------
    void RenderRooms(CoopClient.RoomInfo[] rooms)
    {
        if (_roomList == null) return;
        foreach (Transform c in _roomList) Destroy(c.gameObject);
        if (rooms.Length == 0) { UIBuilder.Text(_roomList, "empty", "열린 방이 없습니다. '방 만들기'를 눌러보세요.", 24, new Color(0.7f,0.7f,0.75f), TextAnchor.MiddleCenter); return; }
        foreach (var r in rooms)
        {
            string label = $"방 {r.host}  ({r.count}/2){(r.started ? " · 진행중" : "")}";
            var btn = UIBuilder.Button(_roomList, "Room_" + r.id, label, 26, () => _c.JoinRoom(r.id), new Color(0.25f, 0.35f, 0.5f));
            btn.interactable = !r.started && r.count < 2;
            var le = btn.gameObject.AddComponent<LayoutElement>(); le.minHeight = 60;
        }
    }

    void RenderRoom(CoopClient.RoomState s)
    {
        if (_roomInfo == null) return;
        string h = string.IsNullOrEmpty(s.host) ? "-" : s.host + (s.hostReady ? " ✔" : "");
        string g = string.IsNullOrEmpty(s.guest) ? "(대기중)" : s.guest + (s.guestReady ? " ✔" : "");
        _roomInfo.text = $"방장: {h}\n참여자: {g}";
        bool bothReady = !string.IsNullOrEmpty(s.guest) && s.hostReady && s.guestReady;
        if (_startBtn != null) _startBtn.interactable = _isHost && bothReady;
    }

    // ---------- UI 구성 ----------
    void Build(Transform canvas)
    {
        _root = UIBuilder.NewUI("CoopLobby", canvas, typeof(RectTransform), typeof(Image));
        UIBuilder.Stretch((RectTransform)_root.transform);
        _root.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.98f);

        _myId = UIBuilder.Text(_root.transform, "MyId", "내 ID: ...", 26, new Color(0.7f,0.9f,1f), TextAnchor.MiddleRight);
        UIBuilder.SetAnchors(_myId.rectTransform, new Vector2(0.55f, 0.93f), new Vector2(0.98f, 0.99f));
        _status = UIBuilder.Text(_root.transform, "Status", "", 24, new Color(1f,0.85f,0.4f), TextAnchor.MiddleLeft);
        UIBuilder.SetAnchors(_status.rectTransform, new Vector2(0.02f, 0.93f), new Vector2(0.55f, 0.99f));

        BuildLobbyPanel();
        BuildRoomPanel();
    }

    void BuildLobbyPanel()
    {
        _lobbyPanel = UIBuilder.NewUI("LobbyPanel", _root.transform, typeof(RectTransform));
        UIBuilder.SetAnchors((RectTransform)_lobbyPanel.transform, new Vector2(0f,0f), new Vector2(1f,0.92f));

        UIBuilder.Text(_lobbyPanel.transform, "T", "로비", 40, new Color(1f,0.85f,0.3f), TextAnchor.UpperCenter)
            .rectTransform.anchorMin = new Vector2(0,0.9f);

        // 방 목록(왼쪽) — 스크롤
        var scroll = UIBuilder.NewUI("RoomScroll", _lobbyPanel.transform, typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D), typeof(Image));
        UIBuilder.SetAnchors((RectTransform)scroll.transform, new Vector2(0.04f, 0.14f), new Vector2(0.5f, 0.88f));
        scroll.GetComponent<Image>().color = new Color(1f,1f,1f,0.04f);
        var content = UIBuilder.NewUI("Content", scroll.transform, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        _roomList = (RectTransform)content.transform;
        _roomList.anchorMin = new Vector2(0,1); _roomList.anchorMax = new Vector2(1,1); _roomList.pivot = new Vector2(0.5f,1);
        _roomList.offsetMin = Vector2.zero; _roomList.offsetMax = Vector2.zero;
        var vlg = content.GetComponent<VerticalLayoutGroup>(); vlg.spacing = 8; vlg.padding = new RectOffset(8,8,8,8); vlg.childControlHeight = true; vlg.childForceExpandHeight = false; vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = scroll.GetComponent<ScrollRect>(); sr.content = _roomList; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped;

        var make = UIBuilder.Button(_lobbyPanel.transform, "Make", "방 만들기", 30, () => _c.CreateRoom(), new Color(0.3f,0.5f,0.4f));
        UIBuilder.SetAnchors((RectTransform)make.transform, new Vector2(0.04f, 0.05f), new Vector2(0.5f, 0.12f));

        // 로비 채팅(오른쪽)
        _lobbyLog = MakeChatLog(_lobbyPanel.transform, new Vector2(0.53f, 0.14f), new Vector2(0.96f, 0.88f));
        _lobbyInput = MakeInput(_lobbyPanel.transform, "메시지 입력...", new Vector2(0.53f, 0.05f), new Vector2(0.82f, 0.12f));
        var send = UIBuilder.Button(_lobbyPanel.transform, "Send", "전송", 28, () => { _c.LobbyChat(_lobbyInput.text); _lobbyInput.text = ""; _lobbyInput.ActivateInputField(); }, new Color(0.3f,0.4f,0.55f));
        UIBuilder.SetAnchors((RectTransform)send.transform, new Vector2(0.83f, 0.05f), new Vector2(0.96f, 0.12f));

        var back = UIBuilder.Button(_lobbyPanel.transform, "Back", "← 타이틀", 24, OnBackToTitle, new Color(0.4f,0.3f,0.3f));
        UIBuilder.SetAnchors((RectTransform)back.transform, new Vector2(0.04f, 0.9f), new Vector2(0.16f, 0.97f));
    }

    void BuildRoomPanel()
    {
        _roomPanel = UIBuilder.NewUI("RoomPanel", _root.transform, typeof(RectTransform));
        UIBuilder.SetAnchors((RectTransform)_roomPanel.transform, new Vector2(0f,0f), new Vector2(1f,0.92f));

        UIBuilder.Text(_roomPanel.transform, "T", "대기실", 40, new Color(1f,0.85f,0.3f), TextAnchor.UpperCenter)
            .rectTransform.anchorMin = new Vector2(0,0.9f);

        _roomInfo = UIBuilder.Text(_roomPanel.transform, "Info", "", 32, Color.white, TextAnchor.UpperLeft);
        UIBuilder.SetAnchors(_roomInfo.rectTransform, new Vector2(0.06f, 0.6f), new Vector2(0.5f, 0.86f));

        _readyBtn = UIBuilder.Button(_roomPanel.transform, "Ready", "준비", 30, ToggleReady, new Color(0.3f,0.5f,0.4f));
        UIBuilder.SetAnchors((RectTransform)_readyBtn.transform, new Vector2(0.06f, 0.48f), new Vector2(0.26f, 0.56f));

        _startBtn = UIBuilder.Button(_roomPanel.transform, "Start", "시작(방장)", 30, () => _c.StartGame(), new Color(0.35f,0.45f,0.6f));
        UIBuilder.SetAnchors((RectTransform)_startBtn.transform, new Vector2(0.28f, 0.48f), new Vector2(0.5f, 0.56f));
        _startBtn.interactable = false;

        var leave = UIBuilder.Button(_roomPanel.transform, "Leave", "나가기", 28, () => { _c.LeaveRoom(); ShowLobby(); }, new Color(0.45f,0.3f,0.3f));
        UIBuilder.SetAnchors((RectTransform)leave.transform, new Vector2(0.06f, 0.38f), new Vector2(0.26f, 0.46f));

        // 방 채팅
        _roomLog = MakeChatLog(_roomPanel.transform, new Vector2(0.53f, 0.14f), new Vector2(0.96f, 0.86f));
        _roomInput = MakeInput(_roomPanel.transform, "메시지 입력...", new Vector2(0.53f, 0.05f), new Vector2(0.82f, 0.12f));
        var send = UIBuilder.Button(_roomPanel.transform, "RSend", "전송", 28, () => { _c.RoomChat(_roomInput.text); _roomInput.text = ""; _roomInput.ActivateInputField(); }, new Color(0.3f,0.4f,0.55f));
        UIBuilder.SetAnchors((RectTransform)send.transform, new Vector2(0.83f, 0.05f), new Vector2(0.96f, 0.12f));
    }

    void ToggleReady()
    {
        _ready = !_ready;
        _c.SetReady(_ready);
        var t = _readyBtn.GetComponentInChildren<Text>();
        if (t != null) t.text = _ready ? "준비 완료 ✔" : "준비";
    }

    void OnBackToTitle()
    {
        Unsubscribe();
        if (_root != null) Destroy(_root);
        Destroy(gameObject);
        // 타이틀 씬 새로고침(연결은 유지되지만 UI 정리) — 그냥 재로드
        SceneManager.LoadScene("TitleScene");
    }

    void ShowLobby() { if (_lobbyPanel) _lobbyPanel.SetActive(true); if (_roomPanel) _roomPanel.SetActive(false); }
    void ShowRoom(string roomId) { if (_lobbyPanel) _lobbyPanel.SetActive(false); if (_roomPanel) _roomPanel.SetActive(true); _ready = false; var t = _readyBtn?.GetComponentInChildren<Text>(); if (t) t.text = "준비"; SetStatus($"방 {roomId} 입장"); }

    void SetStatus(string s) { if (_status != null) _status.text = s; }

    // ---------- 헬퍼 ----------
    Text MakeChatLog(Transform parent, Vector2 min, Vector2 max)
    {
        var bg = UIBuilder.NewUI("ChatBg", parent, typeof(RectTransform), typeof(Image));
        UIBuilder.SetAnchors((RectTransform)bg.transform, min, max);
        bg.GetComponent<Image>().color = new Color(0f,0f,0f,0.25f);
        var log = UIBuilder.Text(bg.transform, "Log", "", 24, new Color(0.9f,0.9f,0.92f), TextAnchor.LowerLeft);
        UIBuilder.Stretch(log.rectTransform);
        log.horizontalOverflow = HorizontalWrapMode.Wrap;
        log.raycastTarget = false;
        return log;
    }

    InputField MakeInput(Transform parent, string placeholder, Vector2 min, Vector2 max)
    {
        var go = UIBuilder.NewUI("Input", parent, typeof(RectTransform), typeof(Image), typeof(InputField));
        UIBuilder.SetAnchors((RectTransform)go.transform, min, max);
        go.GetComponent<Image>().color = new Color(1f,1f,1f,0.9f);
        var input = go.GetComponent<InputField>();
        var text = UIBuilder.Text(go.transform, "Text", "", 26, Color.black, TextAnchor.MiddleLeft);
        UIBuilder.Stretch(text.rectTransform); text.supportRichText = false;
        var ph = UIBuilder.Text(go.transform, "Placeholder", placeholder, 26, new Color(0,0,0,0.4f), TextAnchor.MiddleLeft);
        UIBuilder.Stretch(ph.rectTransform);
        input.textComponent = text;
        input.placeholder = ph;
        input.lineType = InputField.LineType.SingleLine;
        return input;
    }

    void Append(Text log, string line)
    {
        if (log == null) return;
        string txt = string.IsNullOrEmpty(log.text) ? line : log.text + "\n" + line;
        var arr = txt.Split('\n');
        if (arr.Length > 14) txt = string.Join("\n", arr, arr.Length - 14, 14);
        log.text = txt;
    }

    void Unsubscribe()
    {
        if (_c == null) return;
        _c.OnHello = null; _c.OnLobby = null; _c.OnRoomJoined = null; _c.OnRoomUpdate = null;
        _c.OnLobbyChat = null; _c.OnRoomChat = null; _c.OnJoinFail = null; _c.OnPartnerLeft = null;
        _c.OnClosed = null; _c.OnGameStart = null;
    }

    void OnDestroy() { Unsubscribe(); }
}
