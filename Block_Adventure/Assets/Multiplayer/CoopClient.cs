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

//--- 2026-07-01 협동 멀티 클라이언트(전송 계층). 씬 넘어가도 유지(DontDestroyOnLoad).
// 에디터/스탠드얼론=네이티브 ClientWebSocket, WebGL=브라우저 WebSocket(.jslib) 자동 분기.
// 수신은 스레드/콜백 → 큐 → 메인 스레드(Update)에서 이벤트로 디스패치.
public class CoopClient : MonoBehaviour
{
    public static CoopClient Instance { get; private set; }

    public int PlayerId { get; private set; } = -1;
    public bool Connected { get; private set; }

    // 이벤트 (전부 메인 스레드에서 발생)
    public event Action OnWaiting;              // 상대 대기 중
    public event Action<int> OnJoined;          // 매칭됨(playerId 0/1)
    public event Action<int, int> OnStart;      // 전투 시작(공유몹 hp, maxHp)
    public event Action<ResolveData> OnResolve; // 턴 해결(합산 데미지 반영 + 상대 그리드)
    public event Action OnWaitPartner;          // 내 ready 완료, 상대 대기
    public event Action OnPartnerLeft;          // 상대 이탈
    public event Action OnClosed;               // 연결 종료

    public class ResolveData
    {
        public int monsterHp, monsterMaxHp;
        public bool monsterDead;
        public string intent;
        public int[] partnerGrid;
    }

    [Serializable]
    class CoopMsg
    {
        public string type;
        public int playerId;
        public int monsterHp;
        public int monsterMaxHp;
        public bool monsterDead;
        public string intent;
        public int[] partnerGrid;
    }

    readonly ConcurrentQueue<string> _inbox = new ConcurrentQueue<string>();

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

    public void Join()  => Send("{\"type\":\"join\"}");
    public void Leave() => Send("{\"type\":\"leave\"}");

    // 이번 턴 내가 준 데미지 + 내 그리드(colorID 평탄화 배열)를 전송
    public void SendReady(int damage, int[] grid)
    {
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"ready\",\"damage\":").Append(damage).Append(",\"grid\":[");
        if (grid != null)
            for (int i = 0; i < grid.Length; i++) { if (i > 0) sb.Append(','); sb.Append(grid[i]); }
        sb.Append("]}");
        Send(sb.ToString());
    }

    void Send(string json)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CoopSend(json);
#else
        SendNative(json);
#endif
    }

    // ---------- 수신 디스패치(메인 스레드) ----------
    void Update()
    {
        while (_inbox.TryDequeue(out var json)) Dispatch(json);
    }

    void Dispatch(string json)
    {
        CoopMsg m;
        try { m = JsonUtility.FromJson<CoopMsg>(json); }
        catch { return; }
        if (m == null || string.IsNullOrEmpty(m.type)) return;

        switch (m.type)
        {
            case "waiting":     OnWaiting?.Invoke(); break;
            case "joined":      PlayerId = m.playerId; OnJoined?.Invoke(m.playerId); break;
            case "start":       OnStart?.Invoke(m.monsterHp, m.monsterMaxHp); break;
            case "waitPartner": OnWaitPartner?.Invoke(); break;
            case "resolve":
                OnResolve?.Invoke(new ResolveData
                {
                    monsterHp = m.monsterHp, monsterMaxHp = m.monsterMaxHp,
                    monsterDead = m.monsterDead, intent = m.intent, partnerGrid = m.partnerGrid
                });
                break;
            case "partnerLeft": OnPartnerLeft?.Invoke(); break;
            case "__closed":    Connected = false; OnClosed?.Invoke(); break;
        }
    }

    // ---------- WebGL (jslib) ----------
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void CoopConnect(string url);
    [DllImport("__Internal")] static extern void CoopSend(string msg);
    [DllImport("__Internal")] static extern void CoopClose();

    // jslib에서 SendMessage로 호출됨
    public void OnSocketOpen()               { Connected = true; }
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
        catch (Exception)
        {
            Connected = false;
            _inbox.Enqueue("{\"type\":\"__closed\"}");
        }
    }

    void SendNative(string json)
    {
        if (_ws == null || _ws.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(json);
        _ = _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
    }

    void OnDestroy()
    {
        try { _cts?.Cancel(); _ws?.Dispose(); } catch { }
    }
#endif
}
