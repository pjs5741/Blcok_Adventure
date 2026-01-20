using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Monster : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] protected float maxHp = 1000f; // 자식에서 접근 가능하게 protected
    protected float currentHp;
    protected bool isDead = false;

    [Header("UI & Visual")]
    [SerializeField] protected Slider hpSlider;
    [SerializeField] protected SpriteRenderer spriteRenderer;

    // 초기화 (Start 대신 Init을 써서 자식이 제어하기 쉽게 함)
    protected virtual void Start()
    {
        Init();
    }

    public virtual void Init()
    {
        currentHp = maxHp;
        isDead = false;
        UpdateUI();
    }

    // 데미지 받는 함수 (virtual: 자식이 덮어쓰기 가능)
    public virtual void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHp -= damage;
        Debug.Log($"🩸 {gameObject.name} 피격! -{damage}");

        UpdateUI();
        StartCoroutine(HitEffect());

        if (currentHp <= 0)
        {
            Die();
        }
    }

    // 사망 처리 (virtual)
    protected virtual void Die()
    {
        isDead = true;
        Debug.Log($"💀 {gameObject.name} 사망!");
        
        // 기본 사망 연출: 그냥 꺼지기
        gameObject.SetActive(false);
    }

    protected void UpdateUI()
    {
        if (hpSlider != null)
        {
            hpSlider.value = currentHp / maxHp;
        }
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
}