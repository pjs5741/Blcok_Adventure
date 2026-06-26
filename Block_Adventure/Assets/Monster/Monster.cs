using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public enum MonsterIntent { RowAttack, ConvertBlocks, Blind, GravityShift, GravityShiftRight, Pivot }

public class Monster : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] protected float maxHp = 5000f;
    [SerializeField] protected int goldReward = 25;
    protected float currentHp;
    protected MonsterIntent[] _intentPool;        // 이 몹이 쓰는 패턴 풀 (프로필에서 주입, 가중치는 중복으로)
    protected Color _baseColor = Color.white;     // 외형 기본색 (피격 연출 후 복귀용)
    protected bool _hitting = false;              // 피격 연출 중 (독 펄스보다 우선)
    static readonly Color PoisonColor = new Color(0.6f, 0.2f, 0.85f);   // 독 상태 보라색
    protected bool isDead = false;
    protected int _poisonStacks = 0;
    protected int _burnStacks = 0;
    protected int _attackDelay = 0; // 방패 효과로 누적된 공격 지연 턴 수

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
        ApplyNodeProfile();   // 현재 노드 타입에 맞는 몬스터 프로필 적용 (HP/색/패턴풀)
        Init();
    }

    //--- 2026-06-25 노드별 몬스터 배정: 현재 노드 타입에 맞는 프로필을 골라 적용
    void ApplyNodeProfile()
    {
        var node = Run.mapState?.GetNode(Run.mapState.currentNodeId);
        NodeType type = node != null ? node.type : NodeType.Battle;
        SetProfile(MonsterRegistry.GetForNode(type));
    }

    public void SetProfile(MonsterProfile p)
    {
        if (p == null) return;
        maxHp = p.maxHp;
        goldReward = p.goldReward;
        _intentPool = p.intentPool;
        _baseColor = p.tint;
        if (spriteRenderer != null) spriteRenderer.color = _baseColor;
        if (!string.IsNullOrEmpty(p.name)) gameObject.name = p.name;
    }

    public virtual void Init()
    {
        currentHp = maxHp;
        isDead = false;
        _poisonStacks = 0;
        _burnStacks = 0;
        _attackDelay = 0;
        UpdateUI();
        PickIntent();
    }

    //--- 2026-06-26 독 걸린 동안 보라색으로 반짝(펄스). 피격 중(_hitting)엔 양보.
    protected virtual void Update()
    {
        if (isDead || _hitting || spriteRenderer == null) return;
        if (_poisonStacks > 0)
        {
            float t = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;   // 0~1 펄스
            spriteRenderer.color = Color.Lerp(_baseColor, PoisonColor, t * 0.7f);
        }
        else
        {
            spriteRenderer.color = _baseColor;
        }
    }

    public void AddAttackDelay(int turns)
    {
        _attackDelay += turns;
        Debug.Log($"🛡 {gameObject.name} 공격 {turns}턴 지연 (누적 {_attackDelay})");
    }

    // 공격 시도 전 지연 차감. true 반환 시 공격 진행, false 시 스킵.
    public bool ConsumeAttackDelay()
    {
        if (_attackDelay > 0)
        {
            _attackDelay--;
            Debug.Log($"🛡 {gameObject.name} 공격 지연 (남은 {_attackDelay})");
            return false;
        }
        return true;
    }

    protected virtual void OnEnable()
    {
        GameEvents.OnTurnEnd += TickPoison;
        GameEvents.OnTurnStart += TickBurn;
    }

    protected virtual void OnDisable()
    {
        GameEvents.OnTurnEnd -= TickPoison;
        GameEvents.OnTurnStart -= TickBurn;
    }

    public void ApplyPoison(int stacks)
    {
        _poisonStacks += stacks;
        Debug.Log($"☠ {gameObject.name} 독 {_poisonStacks}스택 누적");
    }

    public void ApplyBurn(int stacks)
    {
        _burnStacks += stacks;
        Debug.Log($"🔥 {gameObject.name} 화상 {_burnStacks}스택 누적");
    }

    void TickPoison()
    {
        if (isDead || _poisonStacks <= 0) return;
        Debug.Log($"☠ 독 데미지 -{_poisonStacks} ({gameObject.name})");
        TakeDamage(_poisonStacks);
        _poisonStacks = Mathf.Max(0, _poisonStacks - 1);
    }

    void TickBurn()
    {
        if (isDead || _burnStacks <= 0) return;
        Debug.Log($"🔥 화상 데미지 -{_burnStacks} ({gameObject.name})");
        TakeDamage(_burnStacks);
        _burnStacks = Mathf.Max(0, _burnStacks - 1);
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
        if (_intentPool != null && _intentPool.Length > 0)
            CurrentIntent = _intentPool[Random.Range(0, _intentPool.Length)];
        else
            CurrentIntent = MonsterIntent.RowAttack;   // 풀 미설정 시 기본
        if (intentText == null) return;
        intentText.text = CurrentIntent switch
        {
            MonsterIntent.RowAttack    => "⚔ 줄 추가",
            MonsterIntent.ConvertBlocks => "☠ 블록 변환",
            MonsterIntent.Blind         => "🌑 시야 가림",
            MonsterIntent.GravityShift  => "⬅ 중력 왼쪽",
            MonsterIntent.GravityShiftRight => "➡ 중력 오른쪽",
            MonsterIntent.Pivot         => "🔄 판 회전",
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
            _hitting = true;
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = _baseColor;
            _hitting = false;
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
            case MonsterIntent.Blind:
                GameManager.Instance.blockGrid.ApplyBlind(3);
                break;
            case MonsterIntent.GravityShift:
                GameManager.Instance.blockGrid.ApplyGravityShift(true);
                break;
            case MonsterIntent.GravityShiftRight:
                GameManager.Instance.blockGrid.ApplyGravityShift(false);
                break;
            case MonsterIntent.Pivot:
                GameManager.Instance.blockGrid.ApplyPivot(3);
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