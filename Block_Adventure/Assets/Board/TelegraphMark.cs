using UnityEngine;

//--- 2026-07-13 [인텐트 예고 마커] 다음 공격에 영향받을 칸 위에 반투명 펄스 오버레이 (삼키기=점액 연두, 오염=보라).
// 카운트다운 동안 미리 보여줘 카운터플레이(해당 줄 클리어로 회피) 유도. 스프라이트는 절차 생성 — 리소스 막판 교체.
public class TelegraphMark : MonoBehaviour
{
    static Sprite _sprite;
    SpriteRenderer _sr;
    Color _base;

    public static GameObject Create(Transform parent, Vector2Int cell, Color color)
    {
        var go = new GameObject("TelegraphMark");
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(cell.x, cell.y, -0.05f);   // 블록 살짝 앞
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSprite();
        sr.color = color;
        sr.sortingOrder = 5;   // 블록(0)·아이콘 오버레이(+1) 위
        var tm = go.AddComponent<TelegraphMark>();
        tm._sr = sr;
        tm._base = color;
        return go;
    }

    void Update()
    {
        if (_sr == null) return;
        float t = (Mathf.Sin(Time.time * FxTuning.I.telegraphPulseRate) + 1f) * 0.5f;   // 0~1 펄스
        var c = _base;
        c.a = _base.a * Mathf.Lerp(0.55f, 1f, t);
        _sr.color = c;
    }

    // 1칸 크기 둥근 사각형 스프라이트 (흰색 — 색은 SpriteRenderer.color로)
    static Sprite GetSprite()
    {
        if (_sprite != null) return _sprite;
        const int S = 64;
        const float corner = 14f, soft = 2f;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                // 둥근 사각형 SDF: 모서리 밖 거리
                float dx = Mathf.Max(Mathf.Abs(x - (S - 1) / 2f) - (S / 2f - corner), 0f);
                float dy = Mathf.Max(Mathf.Abs(y - (S - 1) / 2f) - (S / 2f - corner), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 1f - Mathf.SmoothStep(corner - soft, corner, d);
                px[y * S + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        _sprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);   // PPU=64 → 1칸
        return _sprite;
    }
}
