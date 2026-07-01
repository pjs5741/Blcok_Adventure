using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public enum MonsterIntent { RowAttack, ConvertBlocks, Blind, GravityShift, GravityShiftRight, Pivot, SelfCleanse, Dispel }

public class Monster : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] protected float maxHp = 5000f;
    [SerializeField] protected int goldReward = 25;
    protected float currentHp;
    protected MonsterIntent[] _intentPool;        // 이 몹이 쓰는 패턴 풀 (프로필에서 주입, 가중치는 중복으로)
    protected ShapeDrop[] _rewardShapes;          //--- 2026-06-30 보상 카드 모양 드랍 가중치
    public ShapeDrop[] RewardShapes => _rewardShapes;
    protected Color _baseColor = Color.white;     // 외형 기본색 (피격 연출 후 복귀용)
    protected bool _hitting = false;              // 피격 연출 중 (독 펄스보다 우선)
    public bool IsHitReacting => _hitting;        //--- 2026-06-30 피격 모션 진행 중 여부 (공격 후 중력 대기용)
    static readonly Color PoisonColor = new Color(0.6f, 0.2f, 0.85f);   // 독 상태 보라색
    protected bool isDead = false;
    protected int _poisonStacks = 0;
    protected int _burnStacks = 0;
    //--- 2026-07-01 디버프 걸린 순서(작을수록 먼저=왼쪽). 소진되면 -1.
    protected int _poisonOrder = -1, _burnOrder = -1, _debuffSeq = 0;
    protected int _attackDelay = 0; // 방패 효과로 누적된 공격 지연 턴 수

    [Header("보상 블록")]
    [SerializeField] protected List<GameObject> rewardPool = new List<GameObject>();

    public virtual List<GameObject> GetRewardPool() => rewardPool;

    [Header("UI & Visual")]
    [SerializeField] protected Slider hpSlider;
    protected SpriteRenderer spriteRenderer;
    Animator animator;
    [SerializeField] protected string attackStateName = "Basic_Attack";   //--- 공격 애니 상태명(애니 변경 시 인스펙터 수정)

    public MonsterIntent CurrentIntent { get; private set; }
    //--- 2026-06-30 인텐트를 텍스트 → 이미지(임시 무지개 placeholder) + 호버 툴팁(효과 설명)으로 변경
    protected Image intentIcon;
    protected TooltipTarget intentTip;
    protected Text countdownText;   //--- 2026-06-30 공격까지 남은 턴 수 (인텐트 아래)
    //--- 2026-06-30 독/화상 디버프 배지 (아이콘 + 우하단 숫자)
    protected GameObject _poisonBadge, _burnBadge;
    protected Text _poisonCount, _burnCount;

    protected virtual void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        //--- 2026-06-26 피벗 연출 중 카메라 휩쓸림 방지 + 뒤집은 후 간격 유지용 헬퍼 부착
        if (GetComponent<PivotActor>() == null) gameObject.AddComponent<PivotActor>();
        SetupIntentIcon();
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
        _rewardShapes = p.rewardShapes;
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
        _poisonOrder = -1; _burnOrder = -1; _debuffSeq = 0;
        _attackDelay = 0;
        UpdateUI();
        RefreshStatus();
        PickIntent();
    }

    //--- 2026-06-26 독 걸린 동안 보라색으로 반짝(펄스). 피격 중(_hitting)엔 양보.
    protected virtual void Update()
    {
        //--- 2026-07-01 체력바 부드럽게 보간(피격/사망과 무관하게 매 프레임 목표치로 수렴)
        if (hpSlider != null)
        {
            float target = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
            hpSlider.value = Mathf.Abs(hpSlider.value - target) > 0.0005f
                ? Mathf.Lerp(hpSlider.value, target, Time.deltaTime * Tuning.HpBarLerpSpeed)
                : target;
        }

        if (isDead || _hitting || spriteRenderer == null) return;
        if (_poisonStacks > 0)
        {
            float t = (Mathf.Sin(Time.time * Tuning.PoisonPulseRate) + 1f) * 0.5f;   // 0~1 펄스
            spriteRenderer.color = Color.Lerp(_baseColor, PoisonColor, t * Tuning.PoisonColorIntensity);
        }
        else
        {
            spriteRenderer.color = _baseColor;
        }
    }

    //--- 2026-07-01 정화 패턴: 자신의 독·화상 전부 제거
    public void CleanseSelf()
    {
        _poisonStacks = 0; _burnStacks = 0;
        _poisonOrder = -1; _burnOrder = -1;
        RefreshStatus();
        Debug.Log($"✨ {gameObject.name} 정화 → 독·화상 제거");
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

    //--- 2026-06-30 도트(독/화상) 인라인 이벤트 틱 폐기 → GameManager의 전용 DoT 페이즈(TickDoTRoutine)에서 처리(보이게).
    // OnEnable/OnDisable의 도트 구독 제거 (블라인드/피벗 등 다른 OnTurnStart 구독은 BlockGrid 쪽에서 유지)

    public void ApplyPoison(int stacks)
    {
        if (_poisonStacks == 0) _poisonOrder = _debuffSeq++;   // 처음 걸리는 순간 순서 기록
        _poisonStacks += stacks;
        RefreshStatus();
        Debug.Log($"☠ {gameObject.name} 독 {_poisonStacks}스택 누적");
    }

    public void ApplyBurn(int stacks)
    {
        if (_burnStacks == 0) _burnOrder = _debuffSeq++;
        _burnStacks += stacks;
        RefreshStatus();
        Debug.Log($"🔥 {gameObject.name} 화상 {_burnStacks}스택 누적");
    }

    public bool HasDoT => !isDead && (_poisonStacks > 0 || _burnStacks > 0);

    //--- 2026-06-30 도트 페이즈: 화상 → 독 순서로 피격 연출 + 잠깐 멈춤(보이게). GameManager.DotPhase에서 호출.
    public System.Collections.IEnumerator TickDoTRoutine()
    {
        // 먼저 걸린(왼쪽) 것부터 처리
        bool poisonFirst = _poisonOrder >= 0 && (_burnOrder < 0 || _poisonOrder <= _burnOrder);
        if (poisonFirst) { yield return TickOne(true); yield return TickOne(false); }
        else { yield return TickOne(false); yield return TickOne(true); }
    }

    System.Collections.IEnumerator TickOne(bool poison)
    {
        int stacks = poison ? _poisonStacks : _burnStacks;
        if (stacks <= 0 || isDead) yield break;

        Debug.Log($"{(poison ? "☠ 독" : "🔥 화상")} 데미지 -{stacks} ({gameObject.name})");
        TakeDamage(stacks);
        if (poison) { _poisonStacks = Mathf.Max(0, _poisonStacks - 1); if (_poisonStacks == 0) _poisonOrder = -1; }
        else        { _burnStacks = Mathf.Max(0, _burnStacks - 1);     if (_burnStacks == 0) _burnOrder = -1; }
        RefreshStatus();
        yield return new WaitForSeconds(Tuning.DotTickPause);
    }

    void SetupIntentIcon()
    {
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning($"{gameObject.name}: 인텐트용 Canvas 자식 없음");
            return;
        }

        // 월드 스페이스 캔버스에서 호버(툴팁) 동작하도록 레이캐스터 + 카메라 보장
        if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
        if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null) canvas.worldCamera = Camera.main;

        Transform existing = canvas.transform.Find("IntentIcon");
        if (existing != null)
        {
            intentIcon = existing.GetComponent<Image>();
            intentTip = existing.GetComponent<TooltipTarget>();
            return;
        }

        GameObject iconObj = new GameObject("IntentIcon", typeof(RectTransform), typeof(Image), typeof(TooltipTarget));
        iconObj.transform.SetParent(canvas.transform, false);

        intentIcon = iconObj.GetComponent<Image>();
        intentIcon.sprite = PlaceholderSprite.Rainbow;   // 임시: 실제 인텐트 아이콘 PNG 넣기 전까지 무지개 placeholder
        intentIcon.preserveAspect = true;
        intentTip = iconObj.GetComponent<TooltipTarget>();

        RectTransform rt = iconObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 40);
        rt.sizeDelta = new Vector2(48, 48);

        // 공격까지 남은 턴 숫자 — 인텐트 아이콘 우하단(독/화상 배지와 동일한 위치 규칙)
        GameObject cdObj = new GameObject("Countdown", typeof(RectTransform), typeof(Text));
        cdObj.transform.SetParent(iconObj.transform, false);
        countdownText = cdObj.GetComponent<Text>();
        countdownText.font = UIFont.Bold;
        countdownText.fontSize = 22;
        countdownText.alignment = TextAnchor.LowerRight;
        countdownText.color = Color.white;
        countdownText.raycastTarget = false;
        countdownText.horizontalOverflow = HorizontalWrapMode.Overflow;
        countdownText.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform cdrt = cdObj.GetComponent<RectTransform>();
        cdrt.anchorMin = cdrt.anchorMax = new Vector2(1f, 0f);   // 우하단
        cdrt.pivot = new Vector2(1f, 0f);
        cdrt.anchoredPosition = new Vector2(6, -4);
        cdrt.sizeDelta = new Vector2(40, 26);

        // 상태(독/화상) 배지 — 아이콘 이미지 + 우하단 숫자 + 호버 툴팁. 인텐트 옆에 나란히.
        _poisonBadge = CreateDebuffBadge(canvas.transform, 3, new Vector2(50, 40), out _poisonCount);  // 독=독약
        _burnBadge   = CreateDebuffBadge(canvas.transform, 2, new Vector2(98, 40), out _burnCount);    // 화상=화염
        RefreshStatus();
    }

    // 디버프 배지 생성: 아이콘 + 우하단 숫자 텍스트 + 호버 툴팁
    GameObject CreateDebuffBadge(Transform parent, int colorID, Vector2 anchoredPos, out Text countText)
    {
        GameObject b = new GameObject("DebuffBadge", typeof(RectTransform), typeof(Image), typeof(TooltipTarget));
        b.transform.SetParent(parent, false);
        var img = b.GetComponent<Image>();
        var icon = BlockColors.Icon(colorID);
        img.sprite = icon != null ? icon : PlaceholderSprite.Rainbow;
        img.preserveAspect = true;
        img.raycastTarget = true;   // 호버용

        var tip = b.GetComponent<TooltipTarget>();
        tip.title = BlockColors.Name(colorID);
        tip.body = BlockColors.Desc(colorID);
        var rt = b.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(Tuning.BadgeSize, Tuning.BadgeSize);   //--- 2026-07-01 버프/디버프 공용 크기

        GameObject c = new GameObject("Count", typeof(RectTransform), typeof(Text));
        c.transform.SetParent(b.transform, false);
        countText = c.GetComponent<Text>();
        countText.font = UIFont.Bold;
        countText.fontSize = 22;
        countText.alignment = TextAnchor.LowerRight;
        countText.color = Color.white;
        countText.raycastTarget = false;
        countText.horizontalOverflow = HorizontalWrapMode.Overflow;
        countText.verticalOverflow = VerticalWrapMode.Overflow;
        var crt = c.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 0f);   // 아이콘 우하단
        crt.pivot = new Vector2(1f, 0f);
        crt.anchoredPosition = new Vector2(6, -4);             // 살짝 바깥쪽
        crt.sizeDelta = new Vector2(40, 26);
        return b;
    }

    void RefreshStatus()
    {
        // 활성 디버프를 "걸린 순서(왼쪽부터)"로 빈틈없이 배치
        const float baseX = 50f, y = 40f;
        float spacing = Tuning.BadgeSpacing;   //--- 2026-07-01 버프/디버프 공용 간격

        var active = new System.Collections.Generic.List<(int order, GameObject badge, Text count, int stacks)>();
        if (_poisonStacks > 0 && _poisonBadge != null) active.Add((_poisonOrder, _poisonBadge, _poisonCount, _poisonStacks));
        if (_burnStacks > 0 && _burnBadge != null) active.Add((_burnOrder, _burnBadge, _burnCount, _burnStacks));
        active.Sort((a, b) => a.order.CompareTo(b.order));

        if (_poisonBadge != null) _poisonBadge.SetActive(false);
        if (_burnBadge != null) _burnBadge.SetActive(false);

        for (int i = 0; i < active.Count; i++)
        {
            active[i].badge.SetActive(true);
            ((RectTransform)active[i].badge.transform).anchoredPosition = new Vector2(baseX + i * spacing, y);
            if (active[i].count != null) active[i].count.text = active[i].stacks.ToString();
        }
    }

    public void PickIntent()
    {
        if (_intentPool != null && _intentPool.Length > 0)
            CurrentIntent = _intentPool[Random.Range(0, _intentPool.Length)];
        else
            CurrentIntent = MonsterIntent.RowAttack;   // 풀 미설정 시 기본
        RefreshIntent();
    }

    //--- 2026-06-29 [TEST] 다음 인텐트 강제 지정 — V키 등으로 정상 몬스터 턴 흐름에 태워 발동(즉시 발동 X)
    public void ForceNextIntent(MonsterIntent intent)
    {
        CurrentIntent = intent;
        RefreshIntent();
    }

    //--- 2026-06-30 공격까지 남은 턴 수 표시 (인텐트 아래). GameManager가 매 턴 갱신.
    public void SetAttackCountdown(int turns)
    {
        if (countdownText != null) countdownText.text = turns.ToString();
    }

    // 인텐트 아이콘 호버 툴팁 갱신 (지금은 아이콘이 임시 무지개 공통 → 종류는 툴팁으로 구분)
    void RefreshIntent()
    {
        if (intentTip == null) return;
        intentTip.title = IntentTitle(CurrentIntent);
        intentTip.body = IntentDesc(CurrentIntent);
    }

    static string IntentTitle(MonsterIntent intent) => intent switch
    {
        MonsterIntent.RowAttack        => "줄 추가",
        MonsterIntent.ConvertBlocks    => "블록 변환",
        MonsterIntent.Blind            => "시야 가림",
        MonsterIntent.GravityShift     => "중력 왼쪽",
        MonsterIntent.GravityShiftRight => "중력 오른쪽",
        MonsterIntent.Pivot            => "판 회전",
        MonsterIntent.SelfCleanse      => "정화",
        MonsterIntent.Dispel           => "디스펠",
        _ => "?"
    };

    static string IntentDesc(MonsterIntent intent) => intent switch
    {
        MonsterIntent.RowAttack        => "바닥에 회색 줄을 추가한다.",
        MonsterIntent.ConvertBlocks    => "무작위 블록 몇 개를 회색으로 만든다.",
        MonsterIntent.Blind            => "몇 턴간 쌓인 블록이 보이지 않는다.",
        MonsterIntent.GravityShift     => "몇 턴간 중력이 왼쪽으로 작용한다.",
        MonsterIntent.GravityShiftRight => "몇 턴간 중력이 오른쪽으로 작용한다.",
        MonsterIntent.Pivot            => "판을 90도 회전시킨다.",
        MonsterIntent.SelfCleanse      => "자신의 독·화상을 모두 제거한다.",
        MonsterIntent.Dispel           => "플레이어의 버프(방패·공격 버프)를 모두 제거한다.",
        _ => ""
    };

    // 데미지 받는 함수 (virtual: 자식이 덮어쓰기 가능)
    public virtual void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHp -= damage;
        Debug.Log($"🩸 {gameObject.name} 피격! -{damage}");

        Hit(damage);
        // UpdateUI 즉시 갱신 제거 → Update()에서 체력바 보간 (2026-07-01)
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

    //--- 2026-07-01 협동: 공유 몬스터 HP를 서버 권위값으로 동기화
    public float CurrentHp => currentHp;
    public void SetHp(float hp) { currentHp = Mathf.Clamp(hp, 0f, maxHp); }      // HP바는 Update()에서 보간
    public void SetMaxHp(float hp) { maxHp = Mathf.Max(1f, hp); currentHp = maxHp; UpdateUI(); }

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

        float guard = 0f;
        yield return new WaitUntil(() =>
        {
            guard += Time.deltaTime;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            return (state.IsName(attackStateName) && state.normalizedTime >= 0.5f) || guard > Tuning.AttackMaxWait;
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
            case MonsterIntent.SelfCleanse:
                CleanseSelf();
                break;
            case MonsterIntent.Dispel:
                if (Run.IsInitialized) { Run.stats.shieldStacks = 0; Run.stats.buffAttack = 0f; }
                Debug.Log($"✖ {gameObject.name} 디스펠 → 플레이어 버프 제거");
                break;
        }

        PickIntent();
    }

    public void Hit(float fDamage)
    {
        if (fDamage < maxHp * Tuning.HitKnockbackRatio)
            animator.SetTrigger("hit");
        else
            KnockBack();
    }
    public void KnockBack()
    {
        animator.SetTrigger("knockBack");
    }
}