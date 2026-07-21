using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//--- 2026-07-09 컨텍스트 튜토리얼: 특정 상황이 "처음 일어나는 순간" 게임을 멈추고(timeScale=0)
// 화면을 어둡게 + 해당 위치만 원형 스포트라이트로 원래 색 유지 + 설명 표시. 클릭하면 계속.
// 시작하자마자 몰아서 보여주던 도움말(TutorialOverlay)은 다 무시하는 문제 → 상황별 1회 학습 방식.
// 행동별 평생 1회(PlayerPrefs). 협동에서는 미표시(상대가 턴 타임아웃까지 기다리게 되므로).
public class ContextTutorial : MonoBehaviour
{
    const string KeyPrefix = "CtxTuto_";
    const float DimAlpha = 0.78f;      // 어둠 농도
    const int TexSize = 512;           // 스포트라이트 텍스처 크기
    const float TexHole = 128f;        // 텍스처 내 구멍 반지름(px)
    const float TexSoft = 64f;         // 구멍 가장자리 소프트(px)

    static ContextTutorial _inst;
    static Sprite _spotSprite;

    //--- 게임 일시정지 여부 (블록 조작 등 입력 차단용)
    public static bool IsPaused => _inst != null && _inst._showing;

    readonly Queue<(Vector3 pos, float radius, string title, string body)> _queue
        = new Queue<(Vector3, float, string, string)>();
    bool _showing;
    float _savedTimeScale = 1f;

    Canvas _canvas;
    GameObject _root;
    Text _title, _body;

    public static bool WasShown(string key) => PlayerPrefs.GetInt(KeyPrefix + key, 0) == 1;

    //--- 상황 발생 지점에서 호출. key별 평생 1회. worldPos 중심 worldRadius 반경만 밝게 남김.
    public static void Show(string key, Vector3 worldPos, float worldRadius, string title, string body)
    {
        if (CoopSession.Active) return;   // 협동: 일시정지가 상대 대기를 유발 → 미표시
        if (WasShown(key)) return;
        PlayerPrefs.SetInt(KeyPrefix + key, 1);   // 큐 진입 즉시 소진 처리(같은 턴 중복 방지)
        PlayerPrefs.Save();

        if (_inst == null) _inst = new GameObject("ContextTutorial").AddComponent<ContextTutorial>();
        _inst._queue.Enqueue((worldPos, worldRadius, title, body));
        if (!_inst._showing) _inst.Next();
    }

    // [TEST] 다시 보고 싶을 때 (타이틀 옵션 등에서 호출 가능)
    public static void ResetAll()
    {
        foreach (var k in new[] { "match", "lineclear", "gray", "freeze", "timebomb", "pivot" })
            PlayerPrefs.DeleteKey(KeyPrefix + k);
        //--- 2026-07-15 인트로 튜토리얼(TutorialOverlay, "tutorialSeen")도 함께 초기화 — 리셋 한 번으로 전부 다시 뜨게
        PlayerPrefs.DeleteKey("tutorialSeen");
        PlayerPrefs.Save();
    }

    void Update()
    {
        if (!_showing) return;
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            Next();
    }

    void Next()
    {
        if (_queue.Count == 0) { Close(); return; }

        if (!_showing)
        {
            _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;   // 그 "순간"에 정지 (연출 코루틴 전부 멈춤)
            _showing = true;
        }
        var (pos, radius, title, body) = _queue.Dequeue();
        //--- 2026-07-10 오버레이 생성 실패가 게임을 영구 정지시키지 않게 안전장치
        try { BuildOverlay(pos, radius, title, body); }
        catch (System.Exception e) { Debug.LogError($"ContextTutorial 오버레이 실패: {e.Message}"); Close(); }
    }

    void Close()
    {
        _showing = false;
        Time.timeScale = _savedTimeScale;
        if (_root != null) Destroy(_root);
    }

    void OnDestroy()
    {
        if (_showing) Time.timeScale = _savedTimeScale;   // 씬 전환 등 안전장치
        if (_inst == this) _inst = null;
    }

    // ---------- 오버레이 구성 ----------
    void BuildOverlay(Vector3 worldPos, float worldRadius, string title, string body)
    {
        if (_root != null) Destroy(_root);

        if (_canvas == null)
        {
            var cgo = UIBuilder.NewUI("CtxTutoCanvas", transform, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasScaler));
            _canvas = cgo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 900;   // 모든 UI 위
            UIScale.Configure(cgo.GetComponent<CanvasScaler>());
            Canvas.ForceUpdateCanvases();   //--- 2026-07-10 생성 직후 rect가 0이라 스포트라이트 위치가 틀어지던 문제 → 레이아웃 강제 갱신
        }

        _root = UIBuilder.NewUI("Overlay", _canvas.transform, typeof(RectTransform));
        UIBuilder.Stretch((RectTransform)_root.transform);

        // 월드 → 캔버스 로컬 좌표
        var cam = Camera.main;
        Vector2 screenPt = cam != null ? (Vector2)cam.WorldToScreenPoint(worldPos) : new Vector2(Screen.width / 2f, Screen.height / 2f);
        float screenR = cam != null
            ? Vector2.Distance(screenPt, cam.WorldToScreenPoint(worldPos + cam.transform.right * worldRadius))
            : 150f;
        var canvasRT = (RectTransform)_canvas.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPt, null, out Vector2 local);
        float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        float holeR = Mathf.Max(40f, screenR / scale);            // 캔버스 단위 구멍 반지름
        float side = holeR * (TexSize / TexHole);                 // 스포트라이트 사각형 한 변 (구멍 스케일에 맞춤)
        Vector2 cs = canvasRT.rect.size;
        if (cs.x < 1f || cs.y < 1f) cs = new Vector2(1920f, 1080f);   // 레이아웃 전이면 기준 해상도 폴백

        // 화면 밖으로 나가지 않게 클램프
        local.x = Mathf.Clamp(local.x, -cs.x / 2f + holeR, cs.x / 2f - holeR);
        local.y = Mathf.Clamp(local.y, -cs.y / 2f + holeR, cs.y / 2f - holeR);

        // ① 스포트라이트(구멍 뚫린 어둠) — 대상 중심 사각형
        var spot = UIBuilder.NewUI("Spot", _root.transform, typeof(RectTransform), typeof(Image));
        var spotImg = spot.GetComponent<Image>();
        spotImg.sprite = SpotSprite();
        spotImg.raycastTarget = true;
        var spotRt = (RectTransform)spot.transform;
        spotRt.anchorMin = spotRt.anchorMax = new Vector2(0.5f, 0.5f);
        spotRt.anchoredPosition = local;
        spotRt.sizeDelta = new Vector2(side, side);

        // ② 사각형 밖 4면 어둠 (프레임)
        float L = local.x - side / 2f, R = local.x + side / 2f;
        float B = local.y - side / 2f, T = local.y + side / 2f;
        MakeDim("DimL", new Vector2(-cs.x / 2f, -cs.y / 2f), new Vector2(L, cs.y / 2f));
        MakeDim("DimR", new Vector2(R, -cs.y / 2f), new Vector2(cs.x / 2f, cs.y / 2f));
        MakeDim("DimB", new Vector2(L, -cs.y / 2f), new Vector2(R, B));
        MakeDim("DimT", new Vector2(L, T), new Vector2(R, cs.y / 2f));

        // ③ 설명 패널 — 스포트라이트가 위쪽이면 아래, 아래쪽이면 위에
        bool below = local.y > 0f;
        var panel = UIBuilder.NewUI("Panel", _root.transform, typeof(RectTransform), typeof(Image));
        panel.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.11f, 0.97f);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(760f, 210f);
        float py = below ? B - 140f : T + 140f;
        py = Mathf.Clamp(py, -cs.y / 2f + 120f, cs.y / 2f - 120f);
        prt.anchoredPosition = new Vector2(Mathf.Clamp(local.x, -cs.x / 2f + 400f, cs.x / 2f - 400f), py);

        _title = UIBuilder.Text(panel.transform, "Title", title, 36, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
        _title.font = UIFont.Bold;
        UIBuilder.SetAnchors(_title.rectTransform, new Vector2(0f, 0.66f), new Vector2(1f, 1f));

        _body = UIBuilder.Text(panel.transform, "Body", body, 27, Color.white, TextAnchor.UpperCenter);
        UIBuilder.SetAnchors(_body.rectTransform, new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.66f));

        var hint = UIBuilder.Text(panel.transform, "Hint", "클릭해서 계속 ▶", 20, new Color(0.7f, 0.75f, 0.85f), TextAnchor.MiddleRight);
        UIBuilder.SetAnchors(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.97f, 0.16f));
    }

    void MakeDim(string name, Vector2 min, Vector2 max)
    {
        if (max.x - min.x <= 0f || max.y - min.y <= 0f) return;
        var go = UIBuilder.NewUI(name, _root.transform, typeof(RectTransform), typeof(Image));
        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, DimAlpha);
        img.raycastTarget = true;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = (min + max) / 2f;
        rt.sizeDelta = max - min;
    }

    // 스포트라이트 스프라이트: 중앙 원은 투명, 바깥은 어둠 (가장자리 소프트). 1회 절차 생성.
    static Sprite SpotSprite()
    {
        if (_spotSprite != null) return _spotSprite;
        var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
        var px = new Color[TexSize * TexSize];
        Vector2 c = new Vector2(TexSize / 2f, TexSize / 2f);
        for (int y = 0; y < TexSize; y++)
            for (int x = 0; x < TexSize; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TexHole, TexHole + TexSoft, d));
                px[y * TexSize + x] = new Color(0f, 0f, 0f, DimAlpha * a);
            }
        tex.SetPixels(px);
        tex.Apply();
        _spotSprite = Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0.5f));
        return _spotSprite;
    }
}
