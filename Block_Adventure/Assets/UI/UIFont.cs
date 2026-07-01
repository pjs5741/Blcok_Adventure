using UnityEngine;

//--- 2026-06-29 UI 폰트 공용 로더. WebGL 빌드에선 빌트인 폰트(LegacyRuntime.ttf)가 null이라 텍스트가 안 보이는 문제 → Resources의 한글 폰트(Noto Sans KR) 사용.
// 에디터/폰트 누락 시 빌트인으로 폴백.
public static class UIFont
{
    static Font _regular, _bold;

    public static Font Regular
    {
        get
        {
            if (_regular == null) _regular = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
            if (_regular == null) _regular = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _regular;
        }
    }

    public static Font Bold
    {
        get
        {
            if (_bold == null) _bold = Resources.Load<Font>("Fonts/NotoSansKR-Bold");
            if (_bold == null) _bold = Regular;
            return _bold;
        }
    }
}
