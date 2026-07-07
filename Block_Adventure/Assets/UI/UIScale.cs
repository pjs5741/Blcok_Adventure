using UnityEngine;
using UnityEngine.UI;

//--- 2026-07-03 모든 오버레이/카메라 캔버스를 ScaleWithScreenSize(1920x1080, match0.5)로 강제.
// 씬 캔버스가 Constant Pixel Size(800x600)라 창 크기/해상도 다르면 UI가 깨지던 문제(특히 협동 참여자 창) 해결.
public static class UIScale
{
    public static readonly Vector2 Reference = new Vector2(1920, 1080);

    // 코드로 새로 만든 캔버스의 스케일러 설정(동적 오버레이 캔버스용)
    public static void Configure(CanvasScaler cs)
    {
        if (cs == null) return;
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = Reference;
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 0.5f;
    }

    public static void FixAll()
    {
        // 스케일러가 없는 캔버스엔 추가까지(보상/상점/휴식 등 씬 캔버스). 루트 캔버스만(스케일러는 루트에만 유효), 월드 캔버스 제외.
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas) continue;
            if (canvas.renderMode == RenderMode.WorldSpace) continue;
            var cs = canvas.GetComponent<CanvasScaler>();
            if (cs == null) cs = canvas.gameObject.AddComponent<CanvasScaler>();
            Configure(cs);
        }
    }
}
