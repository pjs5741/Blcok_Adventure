using System;
using System.Collections.Concurrent;
using System.Text;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
#endif

//--- 2026-07-03 협동 클라이언트(전송 계층 + 로비/방/전투 프로토콜). 씬 넘어가도 유지(DontDestroyOnLoad).
// 에디터/스탠드얼론=네이티브 ClientWebSocket, WebGL=브라우저 WebSocket(.jslib) 자동 분기.
// 수신은 스레드/콜백 → 큐 → 메인 스레드(Update)에서 이벤트로 디스패치. 송신은 연결 열리기 전이면 큐잉 후 flush.
public class CoopClient : MonoBehaviour
{
    public static CoopClient Instance { get; private set; }

    public int PlayerId { get; private set; } = -1;   // 0=방장, 1=참여자
    public string MyId { get; private set; } = "";     // 표시용 짧은 ID
    public bool Connected { get; private set; }

    // ---- 로비/방 이벤트 ----
    public Action<string> OnHello;                 // 내 표시 ID
    public Action<RoomInfo[]> OnLobby;             // 방 목록
    public Action<string, bool, int> OnRoomJoined; // roomId, 내가방장, playerIndex
    public Action<RoomState> OnRoomUpdate;         // 방 인원/준비 상태
    public Action<string, string> OnLobbyChat;     // from, text
    public Action<string, string> OnRoomChat;      // from, text
    public Action OnJoinFail;
    public Action<GameStartData> OnGameStart;      // seed, playerIndex (맵 진입)
    // ---- 맵 이벤트 ----
    public Action<int, int, int, int> OnGoNode;    // nodeId, monsterHp, monsterMaxHp, monsterSeed (전투 시작)
    public Action<int> OnBattleEnd;                // nodeId (전투 승리 → 맵 복귀)
    public Action<string, int> OnMapEmote;         // from, nodeId (의견 이모트)
    public Action OnAdvanceReady;                  // 둘 다 노드 완료 → 다음 진행 가능
    public Action OnWaitPartnerNode;               // 상대가 아직 현재 노드 진행 중
    // ---- 전투 이벤트 ----
    public Action<ResolveData> OnResolve;
    public Action<int[]> OnPartnerGrid;
    public Action OnWaitPartner;
    public Action OnPartnerLeft;
    public Action OnClosed;

    [Serializable] public class RoomInfo { public string id; public string host; public int count; public bool started; }
    public class RoomState { public string host, guest; public bool hostReady, guestReady; }
    public class GameStartData { public int seed, playerIndex, monsterHp, monsterMaxHp; }
    public class ResolveData { public int monsterHp, monsterMaxHp, partnerDamage, attackCountdown; public bool monsterDead; public string intent; public string nextIntent; public int[] partnerGrid; }

    [Serializable]
    class CoopMsg
    {
        public string type;
        public string id;
        public RoomInfo[] rooms;
        public string roomId; public bool youAreHost; public int playerIndex;
        public string host, guest; public bool hostReady, guestReady;
        public string from, text;
        public int seed, monsterHp, monsterMaxHp, nodeId, partnerDamage, monsterSeed, attackCountdown; public bool monsterDead;
        public string intent; public string nextIntent; public int[] partnerGrid;
    }

    readonly ConcurrentQueue<string> _inbox = new ConcurrentQueue<string>();
    readonly ConcurrentQueue<string> _outbox = new ConcurrentQueue<string>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        gameObject.name = "CoopClient";   // jslib SendMessage 대상 이름 고정
        DontDestroyOnLoad(gameObject);
    }

    public static CoopClient GetOrCreate()
    {
        if (Instance == null) new GameObject("CoopClient").AddComponent<CoopClient>();
        return Instance;
    }

    // ---------- 공개 API ----------
    public void Connect(string url)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CoopConnect(url);
#else
        ConnectNative(url);
#endif
    }

    public void CreateRoom()          => Send("{\"type\":\"createRoom\"}");
    public void JoinRoom(string id)   => Send($"{{\"type\":\"joinRoom\",\"roomId\":\"{Esc(id)}\"}}");
    public void LeaveRoom()           => Send("{\"type\":\"leaveRoom\"}");
    public void SetReady(bool ready)  => Send($"{{\"type\":\"ready\",\"ready\":{(ready ? "true" : "false")}}}");
    public void StartGame()           => Send("{\"type\":\"startGame\"}");
    //--- 2026-07-20 intents: 이 몹의 인텐트 풀(이름 배열). 방장이 MonsterRegistry에서 뽑아 전달 → 서버가 이 풀로 인텐트 선택(솔로와 동일).
    public void MapSelect(int nodeId, bool isBattle, int monsterHp, int monsterSeed, int attackInterval, string[] intents = null)
    {
        string intentsJson = (intents != null && intents.Length > 0)
            ? "[\"" + string.Join("\",\"", intents) + "\"]" : "[]";
        Send($"{{\"type\":\"mapSelect\",\"nodeId\":{nodeId},\"isBattle\":{(isBattle ? "true" : "false")},\"monsterHp\":{monsterHp},\"monsterSeed\":{monsterSeed},\"attackInterval\":{attackInterval},\"intentPool\":{intentsJson}}}");
    }
    public void MapEmote(int nodeId)  => Send($"{{\"type\":\"mapEmote\",\"nodeId\":{nodeId}}}");
    public void SendNodeDone()        => Send("{\"type\":\"nodeDone\"}");
    public void LobbyChat(string t)   => Send($"{{\"type\":\"lobbyChat\",\"text\":\"{Esc(t)}\"}}");
    public void RoomChat(string t)    => Send($"{{\"type\":\"roomChat\",\"text\":\"{Esc(t)}\"}}");

    // 전투: 이번 턴 데미지 + 그리드
    public void SendTurnReady(int damage, int[] grid)
    {
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"turnReady\",\"damage\":").Append(damage).Append(",\"grid\":[");
        AppendInts(sb, grid);
        sb.Append("]}");
        Send(sb.ToString());
    }

    // 실시간 그리드 미러링
    public void SendGrid(int[] grid)
    {
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"grid\",\"grid\":[");
        AppendInts(sb, grid);
        sb.Append("]}");
        Send(sb.ToString());
    }

    static void AppendInts(StringBuilder sb, int[] a)
    {
        if (a == null) return;
        for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(','); sb.Append(a[i]); }
    }

    // JSON 문자열 이스케이프(채팅 등)
    static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
    }

    // ---------- 송신(연결 전이면 큐잉) ----------
    void Send(string json)
    {
        if (!Connected) { _outbox.Enqueue(json); return; }
        RawSend(json);
    }

    void RawSend(string json)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CoopSend(json);
#else
        SendNative(json);
#endif
    }

    void FlushOutbox() { while (_outbox.TryDequeue(out var json)) RawSend(json); }

    // ---------- 수신 디스패치(메인 스레드) ----------
    void Update() { while (_inbox.TryDequeue(out var json)) Dispatch(json); }

    void Dispatch(string json)
    {
        CoopMsg m;
        try { m = JsonUtility.FromJson<CoopMsg>(json); } catch { return; }
        if (m == null || string.IsNullOrEmpty(m.type)) return;

        switch (m.type)
        {
            case "hello":       MyId = m.id; OnHello?.Invoke(m.id); break;
            case "lobby":       OnLobby?.Invoke(m.rooms ?? new RoomInfo[0]); break;
            case "roomJoined":  PlayerId = m.playerIndex; OnRoomJoined?.Invoke(m.roomId, m.youAreHost, m.playerIndex); break;
            case "roomUpdate":  OnRoomUpdate?.Invoke(new RoomState { host = m.host, guest = m.guest, hostReady = m.hostReady, guestReady = m.guestReady }); break;
            case "lobbyChat":   OnLobbyChat?.Invoke(m.from, m.text); break;
            case "roomChat":    OnRoomChat?.Invoke(m.from, m.text); break;
            case "joinFail":    OnJoinFail?.Invoke(); break;
            case "gameStart":   PlayerId = m.playerIndex; OnGameStart?.Invoke(new GameStartData { seed = m.seed, playerIndex = m.playerIndex, monsterHp = m.monsterHp, monsterMaxHp = m.monsterMaxHp }); break;
            case "goNode":      CoopSession.AttackCountdown = m.attackCountdown; OnGoNode?.Invoke(m.nodeId, m.monsterHp, m.monsterMaxHp, m.monsterSeed); break;
            case "battleEnd":   OnBattleEnd?.Invoke(m.nodeId); break;
            case "emote":       OnMapEmote?.Invoke(m.from, m.nodeId); break;
            case "advanceReady":   OnAdvanceReady?.Invoke(); break;
            case "waitPartnerNode": OnWaitPartnerNode?.Invoke(); break;
            case "waitPartner": OnWaitPartner?.Invoke(); break;
            case "resolve":     OnResolve?.Invoke(new ResolveData { monsterHp = m.monsterHp, monsterMaxHp = m.monsterMaxHp, monsterDead = m.monsterDead, intent = m.intent, nextIntent = m.nextIntent, partnerGrid = m.partnerGrid, partnerDamage = m.partnerDamage, attackCountdown = m.attackCountdown }); break;
            case "grid":        OnPartnerGrid?.Invoke(m.partnerGrid); break;
            case "partnerLeft": OnPartnerLeft?.Invoke(); break;
            case "__closed":    Connected = false; OnClosed?.Invoke(); break;
        }
    }

    // ---------- WebGL (jslib) ----------
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void CoopConnect(string url);
    [DllImport("__Internal")] static extern void CoopSend(string msg);
    [DllImport("__Internal")] static extern void CoopClose();

    public void OnSocketOpen()               { Connected = true; FlushOutbox(); }
    public void OnSocketMessage(string json) { _inbox.Enqueue(json); }
    public void OnSocketClose()              { _inbox.Enqueue("{\"type\":\"__closed\"}"); }

    void OnDestroy() { CoopClose(); }
#else
    // ---------- 에디터/스탠드얼론 (네이티브) ----------
    ClientWebSocket _ws;
    CancellationTokenSource _cts;

    async void ConnectNative(string url)
    {
        try
        {
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            await _ws.ConnectAsync(new Uri(url), _cts.Token);
            Connected = true;
            FlushOutbox();
            _ = ReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Coop] 연결 실패: {e.Message}");
            Connected = false;
            _inbox.Enqueue("{\"type\":\"__closed\"}");
        }
    }

    async Task ReceiveLoop()
    {
        var buf = new byte[8192];
        var ms = new System.IO.MemoryStream();
        try
        {
            while (_ws != null && _ws.State == WebSocketState.Open)
            {
                ms.SetLength(0);
                WebSocketReceiveResult r;
                do
                {
                    r = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), _cts.Token);
                    if (r.MessageType == WebSocketMessageType.Close) { Connected = false; _inbox.Enqueue("{\"type\":\"__closed\"}"); return; }
                    ms.Write(buf, 0, r.Count);
                } while (!r.EndOfMessage);
                _inbox.Enqueue(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length));
            }
        }
        catch (Exception) { Connected = false; _inbox.Enqueue("{\"type\":\"__closed\"}"); }
    }

    void SendNative(string json)
    {
        if (_ws == null || _ws.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(json);
        _ = _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
    }

    void OnDestroy() { try { _cts?.Cancel(); _ws?.Dispose(); } catch { } }
#endif
}
