using UnityEngine;
using UnityEngine.EventSystems;

//--- 2026-06-30 월드 스프라이트(미리보기/홀드 미니블록 등)용 호버 툴팁. 자식 렌더러 범위 안에 마우스가 오면 표시.
// 콜라이더 없이 bounds로 판정. title/body는 코드에서 지정.
public class WorldHoverTooltip : MonoBehaviour
{
    public string title;
    public string body;

    private Camera _cam;
    private bool _showing;
    private bool _hasBounds;
    private Bounds _bounds;

    void Start() => ComputeBounds();

    void ComputeBounds()
    {
        var rends = GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) { _hasBounds = false; return; }
        _bounds = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) _bounds.Encapsulate(rends[i].bounds);
        _hasBounds = true;
    }

    void Update()
    {
        if (!_hasBounds) { ComputeBounds(); if (!_hasBounds) return; }
        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) { Clear(); return; }

        Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition);
        bool over = w.x >= _bounds.min.x && w.x <= _bounds.max.x && w.y >= _bounds.min.y && w.y <= _bounds.max.y;
        if (over) { TooltipUI.Instance.ShowTip(title, body); _showing = true; }
        else Clear();
    }

    void Clear() { if (_showing) { TooltipUI.HideIfShown(); _showing = false; } }
    void OnDisable() => Clear();
}
