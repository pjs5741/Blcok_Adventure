using UnityEngine;

//--- 2026-07-09 [몬스터 패턴: 시한폭탄] 블록 위 카운트다운 표시. 감소/폭발 판정은 BlockGrid(TickTimeBombs/ExplodeBomb)가 담당.
// colorID 97 — 회색(99)이 아니라서 줄클리어 성립 → 줄클리어/폭탄 AoE로 제거 가능.
public class TimeBombBlock : MonoBehaviour
{
    public int turnsLeft;
    TextMesh _label;

    public void Init(int turns)
    {
        turnsLeft = turns;

        // 카운트다운 라벨 (FloatingDamage와 동일한 TextMesh 구성)
        var go = new GameObject("BombCount");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        _label = go.AddComponent<TextMesh>();
        var font = UIFont.Bold;
        if (font != null) _label.font = font;
        _label.fontSize = 48;
        _label.characterSize = 0.15f;
        _label.anchor = TextAnchor.MiddleCenter;
        _label.alignment = TextAlignment.Center;
        _label.color = Color.white;
        var mr = go.GetComponent<MeshRenderer>();
        if (font != null && font.material != null) mr.sharedMaterial = font.material;
        var mainSr = GetComponent<SpriteRenderer>();
        mr.sortingOrder = mainSr != null ? mainSr.sortingOrder + 2 : 10;

        Refresh();
    }

    // 매 턴 호출. 0 이하가 되면 true → BlockGrid가 폭발 처리
    public bool Tick()
    {
        turnsLeft--;
        Refresh();
        return turnsLeft <= 0;
    }

    void Refresh()
    {
        if (_label != null) _label.text = Mathf.Max(0, turnsLeft).ToString();
        if (turnsLeft <= 1)   // 마지막 턴 경고색
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = FxTuning.I.bombWarnColor;
        }
    }
}
