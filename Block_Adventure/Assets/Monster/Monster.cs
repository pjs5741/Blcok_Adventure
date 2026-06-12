using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public enum MonsterIntent { RowAttack, ConvertBlocks }

public class Monster : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] protected float maxHp = 5000f;
    [SerializeField] protected int goldReward = 25;
    protected float currentHp;
    protected bool isDead = false;
    protected int _poisonStacks = 0;

    [Header("보상 블록")]
    [SerializeField] protected List<GameObject> rewardPool = new List<GameObject>();

    public virtual List<GameObject> GetRewardPool() => rewardPool;

    [Header("UI & Visual")]
    [SerializeField] protected Slider hpSlider;
    protected SpriteRenderer spriteRenderer;
    Animator animator;

    public MonsterIntent CurrentIntent { get; private set; }
    protected Text intentText;
    
    protected virtual void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        SetupIntentText();
        Init();
    }

    public virtual void Init()
    {
        currentHp = maxHp;
        isDead = false;
        _poisonStacks = 0;
        UpdateUI();
        PickIntent();
    }

    protected virtual void OnEnable()
    {
        GameEvents.OnTurnEnd += TickPoison;
    }

    protected virtual void OnDisable()
    {
        GameEvents.OnTurnEnd -= TickPoison;
    }

    public void ApplyPoison(int stacks)
    {
        _poisonStacks += stacks;
        Debug.Log($"☠ {gameObject.name} 독 {_poisonStacks}스택 누적");
    }

    void TickPoison()
    {
        if (isDead || _poisonStacks <= 0) return;
        Debug.Log($"☠ 독 데미지 -{_poisonStacks} ({gameObject.name})");
        TakeDamage(_poisonStacks);
        _poisonStacks = Mathf.Max(0, _poisonStacks - 1);
    }

    void SetupIntentText()
    {
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning($"{gameObject.name}: 인텐트용 Canvas 자식 없음");
            return;
        }

        Transform existing = canvas.transform.Find("IntentText");
        if (existing != null) { intentText = existing.GetComponent<Text>(); return; }

        GameObject textObj = new GameObject("IntentText");
        textObj.transform.SetParent(canvas.transform, false);
        intentText = textObj.AddComponent<Text>();
        intentText.alignment = TextAnchor.MiddleCenter;
        intentText.fontSize = 24;
        intentText.color = Color.yellow;
        intentText.horizontalOverflow = HorizontalWrapMode.Overflow;
        intentText.verticalOverflow = VerticalWrapMode.Overflow;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        intentText.font = font;

        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 30);
        rt.sizeDelta = new Vector2(200, 40);
    }

    public void PickIntent()
    {
        CurrentIntent = (MonsterIntent)Random.Range(0, System.Enum.GetValues(typeof(MonsterIntent)).Length);
        if (intentText == null) return;
        intentText.text = CurrentIntent switch
        {
            MonsterIntent.RowAttack    => "⚔ 줄 추가",
            MonsterIntent.ConvertBlocks => "☠ 블록 변환",
            _ => ""
        };
    }

    // 데미지 받는 함수 (virtual: 자식이 덮어쓰기 가능)
    public virtual void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHp -= damage;
        Debug.Log($"🩸 {gameObject.name} 피격! -{damage}");

        Hit(damage);
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
        int reward = CalculateGoldReward();
        Debug.Log($"💀 {gameObject.name} 사망! (보상: {reward}골드)");

        Run.stats.gold += reward;

        GameEvents.RaiseMonsterDeath();

        // 기본 사망 연출: 그냥 꺼지기
        gameObject.SetActive(false);
    }

    int CalculateGoldReward()
    {
        if (Run.mapState == null) return goldReward;
        var node = Run.mapState.GetNode(Run.mapState.currentNodeId);
        if (node == null) return goldReward;
        switch (node.type)
        {
            case NodeType.Elite: return goldReward * 2;
            case NodeType.Boss: return goldReward * 4;
            default: return goldReward;
        }
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
    
    public virtual IEnumerator AttackCoroutine()
    {
        animator.SetTrigger("basicAttack");

        yield return new WaitUntil(() =>
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            return state.IsName("Basic_Attack") && state.normalizedTime >= 0.5f;
        });

        switch (CurrentIntent)
        {
            case MonsterIntent.RowAttack:
                GameManager.Instance.blockGrid.ShiftAndCreateRow(99, Color.gray);
                break;
            case MonsterIntent.ConvertBlocks:
                GameManager.Instance.blockGrid.ConvertRandomBlocksToGray(3);
                break;
        }

        PickIntent();
    }

    public void Hit(float fDamage)
    {
        if (fDamage < maxHp * 0.12)
            animator.SetTrigger("hit");
        else
            KnockBack();
    }
    public void KnockBack()
    {
        animator.SetTrigger("knockBack");
    }
}