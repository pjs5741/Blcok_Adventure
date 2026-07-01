using UnityEngine;
using UnityEngine.EventSystems;

//--- 2026-06-30 그리드 블록 호버 툴팁. 마우스 월드좌표를 그리드 칸으로 변환해 해당 칸 블록의 색/효과를 툴팁으로 표시.
// 블록은 월드 스프라이트(콜라이더 없음)라 UI 레이캐스트 대신 좌표 매핑으로 감지. 동적 생성/제거에도 자동 대응.
public class BlockHoverTooltip : MonoBehaviour
{
    public BlockGrid grid;
    private Camera _cam;
    private bool _showing;

    void Update()
    {
        if (grid == null || grid.data == null) return;
        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

        // UI 위(유물/카드 등)에 있으면 그쪽 툴팁에 양보
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearIfShowing();
            return;
        }

        Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition);
        int x = Mathf.RoundToInt(w.x);
        int y = Mathf.RoundToInt(w.y);

        Transform cell = grid.IsValidIndex(x, y) ? grid.data.gridArray[x, y] : null;
        // 은폐(블라인드) 등으로 숨겨진 블록(렌더러 꺼짐)은 툴팁도 안 뜨게 — 정보 누출 방지
        var sr = cell != null ? cell.GetComponent<SpriteRenderer>() : null;
        bool hidden = sr != null && !sr.enabled;
        if (cell != null && !hidden)
        {
            var bc = cell.GetComponent<BlockColor>();
            int id = bc != null ? bc.colorID : 0;
            TooltipUI.Instance.ShowTip(BlockColors.Name(id), BlockColors.Desc(id));
            _showing = true;
        }
        else
        {
            ClearIfShowing();
        }
    }

    void ClearIfShowing()
    {
        if (_showing) { TooltipUI.HideIfShown(); _showing = false; }
    }

    void OnDisable() => ClearIfShowing();
}
