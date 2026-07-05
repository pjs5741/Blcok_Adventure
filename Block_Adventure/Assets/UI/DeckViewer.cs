using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//--- 2026-07-01 [Slay the Spire 참고: 전투 중 뽑을 더미/버린 더미/덱 보기]
// 전투 좌측에 버튼 3개(뽑을 카드=draw pile, 쓴 카드=discard, 덱 전체). 카드 뷰(DeckEditPanel.OpenList)로 표시.
public class DeckViewer : MonoBehaviour
{
    private BlockSpawner _spawner;
    private Transform _canvas;

    void Start() => BuildButtons();

    void BuildButtons()
    {
        var canvasGO = new GameObject("DeckButtonsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15000;
        UIScale.Configure(canvasGO.GetComponent<CanvasScaler>());   //--- 2026-07-03 해상도 스케일(빌드에서 글씨 안 보이던 문제)
        _canvas = canvasGO.transform;

        MakeButton("DrawBtn", "뽑을 카드", 0.56f, ShowDraw);
        MakeButton("UsedBtn", "쓴 카드", 0.50f, ShowUsed);
        MakeButton("DeckAllBtn", "덱 전체", 0.44f, ShowDeck);
    }

    void MakeButton(string name, string label, float y, UnityEngine.Events.UnityAction onClick)
    {
        var btn = UIBuilder.Button(_canvas, name, label, 26, onClick, new Color(0.2f, 0.35f, 0.5f));
        UIBuilder.SetAnchors((RectTransform)btn.transform, new Vector2(0.005f, y), new Vector2(0.11f, y + 0.05f));
    }

    void EnsureSpawner() { if (_spawner == null) _spawner = FindFirstObjectByType<BlockSpawner>(); }

    void ShowDraw()
    {
        EnsureSpawner();
        if (_spawner != null) Open(_spawner.GetDrawPile(), "뽑을 카드(안 쓴)");
    }

    void ShowUsed()
    {
        EnsureSpawner();
        if (_spawner != null) Open(Used(), "쓴 카드");
    }

    void ShowDeck()
    {
        EnsureSpawner();
        if (_spawner != null) Open(_spawner.GetDeckAll(), "덱 전체");
    }

    // 쓴 카드 = 전체 - 뽑을 더미 (개수 기준)
    List<(string shape, int color)> Used()
    {
        var all = _spawner.GetDeckAll();
        var draw = _spawner.GetDrawPile();
        foreach (var d in draw)
        {
            int i = all.FindIndex(u => u.shape == d.shape && u.color == d.color);
            if (i >= 0) all.RemoveAt(i);
        }
        return all;
    }

    void Open(List<(string shape, int color)> list, string title)
    {
        var go = new GameObject("DeckList");
        go.AddComponent<DeckEditPanel>().OpenList(_canvas, list, title);
    }
}
