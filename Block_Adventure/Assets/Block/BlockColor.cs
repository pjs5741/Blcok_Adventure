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

    public void Freeze(int turns)
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        if (!IsFrozen) _unfrozenColor = sr.color;   // 처음 얼 때만 원래 색 기억
        frozenTurns = turns;
        var fx = FxTuning.I;
        StartTint(Color.Lerp(_unfrozenColor, fx.freezeTint, fx.freezeTintStrength), fx.freezeFadeDuration);
    }

    // 매 턴 감소. 해동되는 턴이면 true (호출부 로그용)
    public bool TickFrozen()
    {
        if (frozenTurns <= 0) return false;
        frozenTurns--;
        if (frozenTurns > 0) return false;
        StartTint(_unfrozenColor, FxTuning.I.freezeFadeDuration);   // 원래 색으로 해동 페이드
        return true;
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