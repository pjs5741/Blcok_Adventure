using UnityEngine;

//--- 2026-06-30 임시 placeholder 스프라이트(무지개). 실제 아이콘 PNG 넣기 전까지 모든 임시 이미지로 사용.
// 코드로 생성(에셋/임포트 불필요). PlaceholderSprite.Rainbow 로 접근.
public static class PlaceholderSprite
{
    static Sprite _rainbow;

    public static Sprite Rainbow
    {
        get
        {
            if (_rainbow == null)
            {
                const int s = 64;
                var tex = new Texture2D(s, s) { filterMode = FilterMode.Bilinear };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float hue = (x + y) / (2f * s);   // 대각선 무지개
                        tex.SetPixel(x, y, Color.HSVToRGB(Mathf.Repeat(hue, 1f), 0.85f, 1f));
                    }
                tex.Apply();
                _rainbow = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            }
            return _rainbow;
        }
    }
}
