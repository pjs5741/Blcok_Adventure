using UnityEngine;

//--- 2026-07-03 피격 데미지 숫자 팝업. 월드 TextMesh로 몬스터 위에 뜨고, sin 아치(위로 올라갔다 내려옴) + 페이드아웃.
public class FloatingDamage : MonoBehaviour
{
    private float _t;
    private Vector3 _base;
    private TextMesh _tm;
    private Color _color;

    public static void Spawn(Vector3 worldPos, float amount, Color color)
    {
        var go = new GameObject("DmgPopup");
        go.transform.position = worldPos;

        var tm = go.AddComponent<TextMesh>();
        var font = UIFont.Regular;
        if (font != null) tm.font = font;
        tm.text = Mathf.RoundToInt(amount).ToString();
        tm.fontSize = 48;
        tm.characterSize = Tuning.DamagePopupSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;

        var mr = go.GetComponent<MeshRenderer>();
        if (font != null && font.material != null) mr.sharedMaterial = font.material;
        mr.sortingOrder = 100;   // 스프라이트 위

        var fd = go.AddComponent<FloatingDamage>();
        fd._base = worldPos;
        fd._tm = tm;
        fd._color = color;
    }

    void Update()
    {
        _t += Time.deltaTime / Mathf.Max(Tuning.DamagePopupLife, 0.01f);
        if (_t >= 1f) { Destroy(gameObject); return; }

        float y = Tuning.DamagePopupRise * Mathf.Sin(_t * Mathf.PI);   // 0→최고→0 (아치)
        transform.position = _base + Vector3.up * y;

        var c = _color;
        c.a = Mathf.Clamp01(1.6f * (1f - _t));   // 후반부 페이드아웃
        _tm.color = c;
    }
}
