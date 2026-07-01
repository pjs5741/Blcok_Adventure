using UnityEngine;
using UnityEngine.EventSystems;

//--- 2026-06-29 이 컴포넌트가 붙은 UI 위에 마우스를 올리면 툴팁 표시. title/body를 코드/인스펙터에서 지정.
public class TooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string title;
    [TextArea] public string body;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(body))
            TooltipUI.Instance.ShowTip(title, body);
    }

    public void OnPointerExit(PointerEventData eventData) => TooltipUI.HideIfShown();
    void OnDisable() => TooltipUI.HideIfShown();
}
