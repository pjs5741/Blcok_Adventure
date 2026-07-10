using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    public PlayerStats stats;

    protected SpriteRenderer spriteRenderer;
    public Animator animator;

    //--- 2026-06-26 [회피] 피벗 때 플레이어가 뒤로 포물선 점프. 앵커/정착은 PivotActor가 담당하고, 여기선 점프 변위만 extraOffset로 구동.
    //--- 2026-07-09 연출 값 FxTuning 에셋으로 이동 (씬 직렬화 덮어쓰기 방지 + 실시간 튜닝)
    // [Header("회피(점프)")]
    // public Vector2 dodgeOffset = new Vector2(-2.5f, 0f);   // 뒤로 점프 착지 변위 (Play 보며 조정)
    // public float dodgeHeight = 1.5f;                       // 포물선 정점 높이
    // public float dodgeDuration = Tuning.DodgeDuration;     // 점프 시간(피벗 회전과 맞춤)
    Vector2 dodgeOffset => FxTuning.I.dodgeOffset;
    float dodgeHeight => FxTuning.I.dodgeHeight;
    float dodgeDuration => FxTuning.I.dodgeDuration;

    private PivotActor _pivot;
    private Vector3 _settledDisp;    // 정착 변위 (0 또는 dodgeOffset)
    private Coroutine _dodgeCo;

    void Awake()
    {
        if (!Run.IsInitialized) Run.StartNew();
        stats = Run.stats;
    }

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        _pivot = GetComponent<PivotActor>();
        if (_pivot == null) _pivot = gameObject.AddComponent<PivotActor>();
    }

    // 판이 넘어질 때 — 뒤로 포물선 점프
    public void Dodge()
    {
        if (_dodgeCo != null) StopCoroutine(_dodgeCo);
        //--- 2026-07-10 점프 클립 재생(없으면 무시 — 트랜스폼 포물선은 그대로)
        if (AnimHelper.HasTrigger(animator, "jump")) animator.SetTrigger("jump");
        _dodgeCo = StartCoroutine(HopTo((Vector3)dodgeOffset));
    }

    // 판이 다시 설 때 — 앞으로 점프해 원위치
    public void DodgeReturn()
    {
        if (_dodgeCo != null) StopCoroutine(_dodgeCo);
        if (AnimHelper.HasTrigger(animator, "jump")) animator.SetTrigger("jump");
        _dodgeCo = StartCoroutine(HopTo(Vector3.zero));
    }

    IEnumerator HopTo(Vector3 targetBase)
    {
        Vector3 startBase = _settledDisp;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(dodgeDuration, 0.01f);
            float lin = Mathf.Clamp01(t);
            float e = Mathf.SmoothStep(0f, 1f, lin);            // 좌우 이동은 부드럽게 가감속
            Vector3 d = Vector3.Lerp(startBase, targetBase, e);
            d.y += dodgeHeight * 4f * lin * (1f - lin);         // 포물선: lin=0.5에서 정점
            if (_pivot != null) _pivot.extraOffset = d;
            yield return null;
        }
        _settledDisp = targetBase;
        if (_pivot != null) _pivot.extraOffset = targetBase;
    }

    // 공통 피격 연출 (빨갛게 깜빡)
    protected IEnumerator HitEffect()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = Color.white;
        }
    }
    
    //--- 2026-07-09 공격 2분화: 넉백급 데미지(몬스터 넉백 조건과 동일 기준)면 강공격 모션.
    // 강공격 클립(heavyAttack)이 아직 없으면 자동으로 기본공격 폴백. 반환 = 실제로 강공격을 재생했는지.
    public virtual void Attack() { Attack(false); }

    public bool Attack(bool heavy)
    {
        if (heavy) return AnimHelper.TriggerOrFallback(animator, "heavyAttack", "basicAttack");
        animator.SetTrigger("basicAttack");
        return false;
    }

    public void Hit(float fDamage)
    {
    }

    public void KnockBack()
    {
        animator.SetTrigger("knockBack");
    }
    protected virtual void Die()
    {
        // 기본 사망 연출: 그냥 꺼지기
        gameObject.SetActive(false);
    }
}
