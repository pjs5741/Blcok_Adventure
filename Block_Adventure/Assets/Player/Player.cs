using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    public PlayerStats stats;

    protected SpriteRenderer spriteRenderer;
    public Animator animator;

    void Awake()
    {
        if (!Run.IsInitialized) Run.StartNew();
        stats = Run.stats;
    }

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
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
    
    public virtual void Attack()
    {
        animator.SetTrigger("basicAttack");
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
