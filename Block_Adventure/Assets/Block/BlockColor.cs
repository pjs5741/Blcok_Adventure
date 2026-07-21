using UnityEngine;
using System.Collections;

public class BlockColor : MonoBehaviour
{
    public int colorID = 0; // 0:비어있음, 1~5: 아이콘 ID, 97:시한폭탄, 98:금, 99:회색 garbage

    //--- 2026-07-09 [몬스터 패턴: 얼림] 빙결 상태. 색은 보이되 색깔 매칭 불가(줄클리어로만 제거), N턴 후 해동.
    public int frozenTurns;
    public bool IsFrozen => frozenTurns > 0;
    Color _unfrozenColor;    // 해동 시 복귀할 원래 배경색
    Coroutine _tintCo;
    //--- 2026-07-15 얼림이 색 틴트만으론 티가 안 나서(#6) 얼음 테두리 오버레이 추가 — 블록/아이콘은 비치되 얼었음이 명확
    Transform _frost;
    static Sprite _frostSprite;

    public void Freeze(int turns)
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        if (!IsFrozen) _unfrozenColor = sr.color;   // 처음 얼 때만 원래 색 기억
        frozenTurns = turns;
        var fx = FxTuning.I;
        StartTint(Color.Lerp(_unfrozenColor, fx.freezeTint, fx.freezeTintStrength), fx.freezeFadeDuration);
        AddFrost();
    }

    // 매 턴 감소. 해동되는 턴이면 true (호출부 로그용)
    public bool TickFrozen()
    {
        if (frozenTurns <= 0) return false;
        frozenTurns--;
        if (frozenTurns > 0) return false;
        StartTint(_unfrozenColor, FxTuning.I.freezeFadeDuration);   // 원래 색으로 해동 페이드
        RemoveFrost();
        return true;
    }

    //--- 2026-07-15 얼음 테두리 오버레이 (둥근 사각 링). 블록/아이콘 위에 얹되 가운데는 비어 색·아이콘이 보임.
    void AddFrost()
    {
        if (_frost != null) return;
        var go = new GameObject("FrostOverlay", typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.02f);   // 아이콘(-0.01)보다 살짝 앞
        go.transform.localScale = Vector3.one;
        var fsr = go.GetComponent<SpriteRenderer>();
        fsr.sprite = FrostSprite();
        fsr.color = new Color(0.75f, 0.95f, 1f, 0.95f);   // 밝은 얼음빛
        var mainSr = GetComponent<SpriteRenderer>();
        fsr.sortingOrder = (mainSr != null ? mainSr.sortingOrder : 0) + 2;   // 아이콘(+1) 위
        _frost = go.transform;
    }

    void RemoveFrost()
    {
        if (_frost != null) { Destroy(_frost.gameObject); _frost = null; }
    }

    // 둥근 사각형 "링"(테두리)만 불투명, 안쪽은 투명 — 얼음 프레임. 1회 절차 생성.
    static Sprite FrostSprite()
    {
        if (_frostSprite != null) return _frostSprite;
        const int S = 64;
        const float corner = 12f, edge = 6f;   // corner=모서리 둥글기, edge=테두리 두께
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        var px = new Color[S * S];
        float half = S / 2f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                // 둥근 사각형 경계까지의 부호거리(음수=안쪽)
                float dx = Mathf.Abs(x + 0.5f - half) - (half - corner);
                float dy = Mathf.Abs(y + 0.5f - half) - (half - corner);
                float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f))
                                + Mathf.Min(Mathf.Max(dx, dy), 0f) - corner;
                // outside ≈ 0 이 경계. |outside| < edge 인 띠만 불투명 → 링
                float a = 1f - Mathf.SmoothStep(edge - 1.5f, edge, Mathf.Abs(outside));
                px[y * S + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        _frostSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);   // PPU=64 → 1칸
        return _frostSprite;
    }

    void StartTint(Color target, float duration)
    {
        if (_tintCo != null) StopCoroutine(_tintCo);
        _tintCo = StartCoroutine(TintRoutine(target, duration));
    }

    // rgb만 보간 (알파는 은폐 페이드가 소유 — 서로 침범 X)
    IEnumerator TintRoutine(Color target, float duration)
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;
        Color start = sr.color;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(duration, 0.01f);
            float lin = Mathf.Clamp01(t);
            var c = Color.Lerp(start, target, lin);
            c.a = sr.color.a;
            sr.color = c;
            yield return null;
        }
        _tintCo = null;
    }

    // 옛 호환
    public void SetColorInfo(int id, Color color)
    {
        SetIconInfo(id, color, null);
    }

    // 배경색 + 아이콘 오버레이 (P&D 스타일)
    public void SetIconInfo(int id, Color bgColor, Sprite iconSprite)
    {
        this.colorID = id;
        //--- 2026-07-09 색 재설정(회색 변환 등) 시 빙결 틴트/상태 해제 — 틴트 코루틴이 새 색을 덮지 않게
        if (_tintCo != null) { StopCoroutine(_tintCo); _tintCo = null; }
        frozenTurns = 0;
        RemoveFrost();   //--- 2026-07-15 재색칠 시 얼음 테두리도 제거

        SpriteRenderer mainSr = GetComponent<SpriteRenderer>();
        if (mainSr != null) mainSr.color = bgColor;

        Transform iconChild = transform.Find("IconOverlay");

        if (iconSprite == null)
        {
            if (iconChild != null) Destroy(iconChild.gameObject);
            return;
        }

        if (iconChild == null)
        {
            GameObject iconGo = new GameObject("IconOverlay", typeof(SpriteRenderer));
            iconGo.transform.SetParent(transform, false);
            iconGo.transform.localScale = Vector3.one * 0.75f;
            iconGo.transform.localPosition = new Vector3(0, 0, -0.01f); // 살짝 앞으로
            iconChild = iconGo.transform;
        }

        SpriteRenderer iconSr = iconChild.GetComponent<SpriteRenderer>();
        iconSr.sprite = iconSprite;
        iconSr.color = Color.white;
        if (mainSr != null) iconSr.sortingOrder = mainSr.sortingOrder + 1;
    }
}