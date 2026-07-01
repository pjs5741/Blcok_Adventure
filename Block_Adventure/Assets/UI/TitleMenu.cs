using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

//--- 2026-07-01 타이틀 화면(앱 진입점). 새 게임 / 이어하기(세이브 있을 때만 활성). 자체 Canvas + EventSystem 생성 → 씬엔 이 스크립트 단독 배치면 됨.
public class TitleMenu : MonoBehaviour
{
    void Start()
    {
        EnsureEventSystem();
        Build();
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    void Build()
    {
        var canvasGO = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var bg = UIBuilder.NewUI("BG", canvasGO.transform, typeof(RectTransform), typeof(Image));
        UIBuilder.Stretch((RectTransform)bg.transform);
        bg.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 1f);

        var title = UIBuilder.Text(canvasGO.transform, "Title", "BLOCK ADVENTURE", 120, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
        UIBuilder.SetAnchors(title.rectTransform, new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.85f));

        bool hasSave = SaveSystem.HasSave();

        var cont = UIBuilder.Button(canvasGO.transform, "Continue", "이어하기", 46, OnContinue,
            hasSave ? new Color(0.25f, 0.45f, 0.35f) : new Color(0.2f, 0.2f, 0.22f));
        UIBuilder.SetAnchors((RectTransform)cont.transform, new Vector2(0.35f, 0.42f), new Vector2(0.65f, 0.52f));
        cont.interactable = hasSave;

        var neu = UIBuilder.Button(canvasGO.transform, "NewGame", "새 게임", 46, OnNewGame, new Color(0.3f, 0.4f, 0.55f));
        UIBuilder.SetAnchors((RectTransform)neu.transform, new Vector2(0.35f, 0.28f), new Vector2(0.65f, 0.38f));

        //--- 2026-07-01 협동(멀티) 진입
        var coop = UIBuilder.Button(canvasGO.transform, "Coop", "협동 (베타)", 46, OnCoop, new Color(0.45f, 0.35f, 0.5f));
        UIBuilder.SetAnchors((RectTransform)coop.transform, new Vector2(0.35f, 0.14f), new Vector2(0.65f, 0.24f));

        _canvas = canvasGO.transform;
    }

    private Transform _canvas;

    void OnContinue()
    {
        if (SaveSystem.Load()) SceneManager.LoadScene("MapScene");
    }

    void OnNewGame()
    {
        CoopSession.Reset();   // 싱글 새 게임은 협동 모드 해제
        SaveSystem.Delete();
        Run.StartNew();
        Run.lastResult = RunResult.None;
        SaveSystem.Save();
        SceneManager.LoadScene("MapScene");
    }

    void OnCoop()
    {
        CoopSession.Reset();
        new GameObject("CoopMatchmaking").AddComponent<CoopMatchmaking>().Begin(_canvas);
    }
}
