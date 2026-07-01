using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

//--- 2026-07-01 협동 매칭 오버레이. 타이틀에서 "협동" 선택 시 서버 접속 + 방참가 → 매칭되면 GameScene(협동)로 진입.
public class CoopMatchmaking : MonoBehaviour
{
    private GameObject _root;
    private Text _status;
    private CoopClient _client;

    public void Begin(Transform canvas)
    {
        BuildOverlay(canvas);

        _client = CoopClient.GetOrCreate();
        _client.OnWaiting     += HandleWaiting;
        _client.OnJoined      += HandleJoined;
        _client.OnStart       += HandleStart;
        _client.OnClosed      += HandleClosed;

        SetStatus("서버 연결 중...");
        _client.Connect(CoopSession.GetUrl());
        _client.Join();
    }

    void HandleWaiting()          => SetStatus("상대를 기다리는 중...");
    void HandleJoined(int id)     => SetStatus("매칭 완료! 전투 시작 준비...");
    void HandleClosed()           => SetStatus("서버 연결 실패/종료 — 서버가 켜져 있는지 확인하세요.");

    void HandleStart(int hp, int maxHp)
    {
        CoopSession.Active = true;
        CoopSession.PlayerId = _client.PlayerId;
        CoopSession.MonsterHp = hp;
        CoopSession.MonsterMaxHp = maxHp;
        Unsubscribe();
        if (!Run.IsInitialized) Run.StartNew();   // 협동은 이어하기 없이 새 런
        SceneManager.LoadScene("GameScene");
    }

    void BuildOverlay(Transform canvas)
    {
        _root = UIBuilder.NewUI("CoopMatch", canvas, typeof(RectTransform), typeof(Image));
        UIBuilder.Stretch((RectTransform)_root.transform);
        _root.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.96f);

        _status = UIBuilder.Text(_root.transform, "Status", "", 44, Color.white, TextAnchor.MiddleCenter);
        UIBuilder.SetAnchors(_status.rectTransform, new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.6f));

        var cancel = UIBuilder.Button(_root.transform, "Cancel", "취소", 36, OnCancel, new Color(0.45f, 0.3f, 0.3f));
        UIBuilder.SetAnchors((RectTransform)cancel.transform, new Vector2(0.4f, 0.28f), new Vector2(0.6f, 0.37f));
    }

    void OnCancel()
    {
        if (_client != null) _client.Leave();
        Unsubscribe();
        if (_root != null) Destroy(_root);
        Destroy(gameObject);
    }

    void SetStatus(string s) { if (_status != null) _status.text = s; }

    void Unsubscribe()
    {
        if (_client == null) return;
        _client.OnWaiting -= HandleWaiting;
        _client.OnJoined  -= HandleJoined;
        _client.OnStart   -= HandleStart;
        _client.OnClosed  -= HandleClosed;
    }

    void OnDestroy() => Unsubscribe();
}
