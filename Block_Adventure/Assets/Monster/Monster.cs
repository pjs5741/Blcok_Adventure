using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

//--- 2026-07-09 Freeze(얼림)/TimeBomb(시한폭탄) 추가
//--- 2026-07-13 Devour(삼키기 — 슬라임) 추가
public enum MonsterIntent { RowAttack, ConvertBlocks, Blind, GravityShift, GravityShiftRight, Pivot, SelfCleanse, Dispel, Freeze, TimeBomb, Devour }

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
    //--- 2026-07-15 데드 페이즈용 상태 노출: 사망 즉시(데스모션 재생 중 포함) 판정 + 데스모션 재생 여부
    public bool IsDead => isDead;              // 데스모션이 다 끝나기 전에도 true (죽어가는 몹이 공격/피격되는 것 방지)
    public bool IsDeathPlaying => _deathPlaying;   // GameManager 데드 페이즈가 이게 false 될 때까지 대기
    protected bool _deathPlaying = false;
    protected int _poisonStacks = 0;
    protected int _burnStacks = 0;
    //--- 2026-07-01 디버프 걸린 순서(작을수록 먼저=왼쪽). 소진되면 -1.
    protected int _poisonOrder = -1, _burnOrder = -1, _debuffSeq = 0;
    //--- 2026-07-03 공격 주기(프로필별) + 남은 카운트다운 + 특수 인텐트 쿨다운
    protected int attackInterval = 3;   // 일반 3 / 엘리트 2 / 보스 1
    protected int _attackCountdown = 3; // 다음 공격까지 남은 턴
    int[] _intentCooldown;
    //--- 2026-07-09 광폭화(보스 페이즈): 체력 EnrageHpRatio 이하 최초 1회 발동 → 이후 줄추가가 EnrageRows개
    protected bool _canEnrage;          // 프로필에서 주입 (보스만)
    protected bool _enraged;
    public bool IsEnraged => _enraged;
    //--- 2026-07-15 삼키기(Devour) 성장: 실제로 삼킨 횟수만큼 몸이 커지고(영구), 다음 삼키기 반경도 커짐
    protected int _devourTimes = 0;
    // 현재 삼키기 반경(삼킬수록 +0.15씩, 상한 +1.2). 예고(텔레그래프)와 실행이 같은 값을 쓰도록 단일 소스.
    public float CurrentDevourRadius => Tuning.DevourRadius + Mathf.Min(_devourTimes * 0.15f, 1.2f);

    [Header("보상 블록")]
    [SerializeField] protected List<GameObject> rewardPool = new List<GameObject>();

    public virtual List<GameObject> GetRewardPool() => rewardPool;

    [Header("UI & Visual")]
    [SerializeField] protected Slider hpSlider;
    protected SpriteRenderer spriteRenderer;
    Animator animator;
    PivotActor _pivotActor;   //--- 2026-07-09 점프 가격 연출용 (extraOffset 구동)
    [SerializeField] protected string attackStateName = "Basic_Attack";   //--- 공격 애니 상태명(애니 변경 시 인스펙터 수정)
    [SerializeField] protected string castStateName = "Cast_Attack";      //--- 2026-07-09 특수패턴(판조작) 시전 애니 상태명(클립은 리소스 작업 때)

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
        _pivotActor = GetComponent<PivotActor>();
        if (_pivotActor == null) _pivotActor = gameObject.AddComponent<PivotActor>();
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
        attackInterval = Mathf.Max(1, p.attackInterval);   //--- 2026-07-03 공격 주기
        _canEnrage = p.enrage;   //--- 2026-07-09 광폭화 가능 여부(보스)
        if (spriteRenderer != null) spriteRenderer.color = _baseColor;
        if (!string.IsNullOrEmpty(p.name)) gameObject.name = p.name;

        //--- 2026-07-14 전용 아트가 있는 프로필: 컨트롤러 교체 + 틴트 해제 + 크기 보정 (중복 적용 방지 플래그)
        if (!string.IsNullOrEmpty(p.animPath) && !_artApplied)
        {
            var ctrl = Resources.Load<RuntimeAnimatorController>(p.animPath);
            var an = GetComponent<Animator>();
            if (ctrl != null && an != null)
            {
                an.runtimeAnimatorController = ctrl;
                _baseColor = Color.white;   // 픽셀아트는 원색 그대로 (틴트 구분용 색 제거)
                if (spriteRenderer != null) spriteRenderer.color = _baseColor;
                transform.localScale *= p.artScale;
                _artApplied = true;
            }
        }
    }
    bool _artApplied;   //--- 2026-07-14 협동(SetProfile 선호출)+Start 중복 적용 방지

    public virtual void Init()
    {
        currentHp = maxHp;
        isDead = false;
        _poisonStacks = 0;
        _burnStacks = 0;
        _poisonOrder = -1; _burnOrder = -1; _debuffSeq = 0;
        _devourTimes = 0;   //--- 2026-07-15 전투 단위 삼키기 성장 초기화(재사용 대비)
        _attackCountdown = Mathf.Max(1, attackInterval);
        if (_intentCooldown == null) _intentCooldown = new int[System.Enum.GetValues(typeof(MonsterIntent)).Length];
        else System.Array.Clear(_intentCooldown, 0, _intentCooldown.Length);
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

    public int AttackCountdown => Mathf.Max(0, _attackCountdown);

    //--- 2026-07-03 매 몬스터 턴 호출. 방패(ShieldPerDelay당 1턴 지연) 반영 후 이번 턴 공격 여부 반환.
    public bool AdvanceTurnAndCheckAttack()
    {
        _attackCountdown--;
        if (_attackCountdown > 0) return false;

        // 공격 예정 — 플레이어 방패가 있으면 ShieldPerDelay 소모해 1턴 미룸(스킵 아님, 계속 있으면 계속 미뤄짐)
        if (Run.IsInitialized && Run.stats.shieldStacks >= Tuning.ShieldPerDelay)
        {
            Run.stats.shieldStacks -= Tuning.ShieldPerDelay;
            _attackCountdown = 1;
            Debug.Log($"🛡 방패 {Tuning.ShieldPerDelay} 소모 → 공격 1턴 지연");
            return false;
        }
        _attackCountdown = attackInterval;   // 공격 후 주기 리셋
        return true;
    }

    // 유물(수호의 손) 등 외부에서 공격을 turns턴 미룸
    public void AddAttackDelay(int turns)
    {
        _attackCountdown += turns;
        Debug.Log($"🛡 {gameObject.name} 공격 {turns}턴 지연 (남은 카운트 {_attackCountdown})");
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
        if (_intentCooldown == null) _intentCooldown = new int[System.Enum.GetValues(typeof(MonsterIntent)).Length];
        for (int i = 0; i < _intentCooldown.Length; i++) if (_intentCooldown[i] > 0) _intentCooldown[i]--;

        MonsterIntent pick = MonsterIntent.RowAttack;
        if (_intentPool != null && _intentPool.Length > 0)
        {
            //--- 2026-07-03 쿨다운 중인 특수 인텐트 제외하고 선택(다 막히면 줄추가로 폴백)
            var cand = new System.Collections.Generic.List<MonsterIntent>();
            foreach (var it in _intentPool) if (_intentCooldown[(int)it] <= 0) cand.Add(it);
            pick = cand.Count > 0 ? cand[Random.Range(0, cand.Count)] : MonsterIntent.RowAttack;
        }
        CurrentIntent = pick;
        if (pick != MonsterIntent.RowAttack) _intentCooldown[(int)pick] = Tuning.IntentCooldown;   // 특수 인텐트 재등장 쿨다운
        RefreshIntent();
        RefreshTelegraph();
    }

    //--- 2026-06-29 [TEST] 다음 인텐트 강제 지정 — V키 등으로 정상 몬스터 턴 흐름에 태워 발동(즉시 발동 X)
    public void ForceNextIntent(MonsterIntent intent)
    {
        CurrentIntent = intent;
        RefreshIntent();
        RefreshTelegraph();
    }

    //--- 2026-07-03 협동: 서버가 정한 인텐트 이름으로 아이콘 반영(로컬 랜덤 인텐트 대신)
    public void SetIntentByName(string name)
    {
        if (System.Enum.TryParse(name, out MonsterIntent it)) { CurrentIntent = it; RefreshIntent(); RefreshTelegraph(); }
    }

    //--- 2026-07-13 인텐트 예고: 삼키기/오염은 대상 칸을 미리 확정해 판에 마커 표시 (그 외 인텐트는 예고 제거)
    void RefreshTelegraph()
    {
        var grid = GameManager.Instance != null ? GameManager.Instance.blockGrid : null;
        if (grid == null) return;
        switch (CurrentIntent)
        {
            case MonsterIntent.Devour:        grid.TelegraphDevour(CurrentDevourRadius); break;
            case MonsterIntent.ConvertBlocks: grid.TelegraphConvert(Tuning.ConvertSpotCount, Tuning.ConvertSpotRadius); break;
            default:                          grid.ClearTelegraph(); break;
        }
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
        //--- 2026-07-09 광폭화 중 줄추가는 개수 강화 안내
        if (_enraged && CurrentIntent == MonsterIntent.RowAttack)
            intentTip.body = $"광폭화! 바닥에 회색 줄을 {Tuning.EnrageRows}개 추가한다.";
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
        MonsterIntent.Freeze           => "얼림",
        MonsterIntent.TimeBomb         => "시한폭탄",
        MonsterIntent.Devour           => "삼키기",
        _ => "?"
    };

    static string IntentDesc(MonsterIntent intent) => intent switch
    {
        MonsterIntent.RowAttack        => "바닥에 회색 줄을 추가한다.",
        MonsterIntent.ConvertBlocks    => $"판 {Tuning.ConvertSpotCount}군데를 오염시켜 주변 블록을 회색으로 만든다.",
        MonsterIntent.Blind            => "몇 턴간 쌓인 블록이 보이지 않는다.",
        MonsterIntent.GravityShift     => "몇 턴간 중력이 왼쪽으로 작용한다.",
        MonsterIntent.GravityShiftRight => "몇 턴간 중력이 오른쪽으로 작용한다.",
        MonsterIntent.Pivot            => "판을 90도 회전시킨다.",
        MonsterIntent.SelfCleanse      => "자신의 독·화상을 모두 제거한다.",
        MonsterIntent.Dispel           => "플레이어의 버프(방패·공격 버프)를 모두 제거한다.",
        MonsterIntent.Freeze           => $"블록 {Tuning.FreezeCount}개를 얼린다. 언 블록은 색깔 매칭이 안 되고, 줄 클리어로만 지울 수 있다. {Tuning.FreezeTurns}턴 뒤 녹는다.",
        MonsterIntent.TimeBomb         => $"판에 시한폭탄을 설치한다. {Tuning.TimeBombTurns}턴 안에 줄 클리어 등으로 없애지 못하면 터져서 주변이 회색이 된다.",
        MonsterIntent.Devour           => "쌓인 블록을 원형으로 삼켜 소화해버린다. 삼켜진 자리는 구멍이 되어 줄 완성이 어려워진다.",
        _ => ""
    };

    // 데미지 받는 함수 (virtual: 자식이 덮어쓰기 가능)
    public virtual void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHp -= damage;
        Debug.Log($"🩸 {gameObject.name} 피격! -{damage}");

        //--- 2026-07-03 데미지 숫자 팝업(sin 아치)
        FloatingDamage.Spawn(transform.position + Vector3.up * Tuning.DamagePopupYOffset, damage, Color.white);

        Hit(damage);
        // UpdateUI 즉시 갱신 제거 → Update()에서 체력바 보간 (2026-07-01)
        StartCoroutine(HitEffect());

        if (currentHp <= 0)
        {
            Die();
        }
        //--- 2026-07-09 광폭화: 체력 비율 최초 통과 시 1회 발동
        else if (_canEnrage && !_enraged && currentHp <= maxHp * Tuning.EnrageHpRatio)
        {
            StartCoroutine(EnrageRoutine());
        }
    }

    //--- 2026-07-09 광폭화 연출: 셰이크 + 붉은 틴트 + 크기 업. 이후 줄추가가 EnrageRows개(AttackCoroutine).
    IEnumerator EnrageRoutine()
    {
        _enraged = true;
        var fx = FxTuning.I;
        Debug.Log($"😡 {gameObject.name} 광폭화! (체력 {Tuning.EnrageHpRatio:P0} 이하)");
        if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(fx.enrageShakeDuration, fx.enrageShakeStrength);

        // 피격 연출(빨간 깜빡)이 끝난 뒤 기본색을 광폭화 색으로 전환 (HitEffect가 _baseColor로 복귀시키므로 기본색 자체를 바꿈)
        yield return new WaitWhile(() => _hitting);
        Color from = _baseColor;
        Color to = Color.Lerp(_baseColor, fx.enrageTint, fx.enrageTintStrength);
        Vector3 s0 = transform.localScale, s1 = s0 * fx.enrageScale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(fx.enrageDuration, 0.01f);
            float lin = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            _baseColor = Color.Lerp(from, to, lin);   // 독 펄스/피격 복귀도 이 색 기준
            if (!_hitting && spriteRenderer != null && _poisonStacks <= 0) spriteRenderer.color = _baseColor;
            transform.localScale = Vector3.Lerp(s0, s1, lin);
            yield return null;
        }
        _baseColor = to;
        transform.localScale = s1;

        // 인텐트 툴팁에 광폭화 반영
        RefreshIntent();
    }

    //--- 2026-07-15 삼키기 성공(블록을 실제로 삼킴) 시 호출: 몸집 커짐(영구) + 다음 삼키기 반경 강화.
    // BlockGrid.DevourRoutine이 "꿀꺽" 연출 뒤 삼킨 개수와 함께 호출. 헛삼킴(0개, 플레이어 회피)이면 성장 없음.
    public void OnDevoured(int blocksEaten)
    {
        if (blocksEaten <= 0) return;
        _devourTimes++;
        StartCoroutine(DevourGrowRoutine(blocksEaten));
        Debug.Log($"🐛 슬라임 성장! (삼킨 횟수 {_devourTimes}, 이번 {blocksEaten}개, 다음 반경 {CurrentDevourRadius:F2})");
    }

    IEnumerator DevourGrowRoutine(int blocksEaten)
    {
        Vector3 from = transform.localScale;
        float grow = 1f + Mathf.Min(blocksEaten * 0.02f, 0.1f);   // 이번에 삼킨 만큼 커짐(한 번에 최대 +10%)
        Vector3 to = from * grow;
        if (CameraShake.Instance != null) CameraShake.Instance.TriggerShake(0.2f, 0.15f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.35f;
            transform.localScale = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }
        transform.localScale = to;
    }

    //--- 2026-07-20 협동: 서버가 사망 확정(_coopResolve.monsterDead)했는데 로컬 데미지 반올림으로 아직 안 죽었으면
    // 강제로 사망 처리 → 솔로와 동일하게 데스모션이 반드시 재생되도록 보장.
    public void EnsureDead()
    {
        if (isDead) return;
        currentHp = 0f;
        Die();
    }

    // 사망 처리 (virtual)
    protected virtual void Die()
    {
        isDead = true;
        //--- 2026-07-06 골드는 보상창(RewardManager)에서 눈에 보이게 지급 → 여기선 조용히 안 올림
        Debug.Log($"💀 {gameObject.name} 사망!");

        GameEvents.RaiseMonsterDeath();

        //--- 2026-07-14 사망 연출: death 클립 있으면 재생 후 꺼짐 (없으면 기존대로 즉시 꺼짐)
        if (AnimHelper.HasTrigger(animator, "death")) StartCoroutine(DeathRoutine());
        else gameObject.SetActive(false);
    }

    IEnumerator DeathRoutine()
    {
        _deathPlaying = true;   //--- 2026-07-15 데스모션 시작 → GameManager 데드 페이즈가 완료까지 대기
        animator.SetTrigger("death");
        float guard = 0f;
        yield return new WaitUntil(() =>
        {
            guard += Time.deltaTime;
            var st = animator.GetCurrentAnimatorStateInfo(0);
            return (st.IsName("Death") && st.normalizedTime >= 1f) || guard > 3f;   // 무한대기 방지
        });
        //--- 2026-07-15 SetActive(false) 전에 내려야 함 (비활성 순간 이 코루틴이 중단되어 이후 줄이 실행되지 않음)
        _deathPlaying = false;
        gameObject.SetActive(false);
    }

    //--- 2026-07-06 이 몹 처치 골드 보상(노드 타입 배율 포함). 보상창에서 지급/표시.
    public int GoldReward => CalculateGoldReward();

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
    public float MaxHp => maxHp;   //--- 2026-07-09 강공격(넉백급 데미지) 판정용
    public void SetHp(float hp)
    {
        currentHp = Mathf.Clamp(hp, 0f, maxHp);   // HP바는 Update()에서 보간
        //--- 2026-07-09 협동: 서버 권위값 반영으로 광폭화 임계를 지나친 경우도 발동 (TakeDamage 크로싱 놓침 방지)
        if (_canEnrage && !_enraged && !isDead && currentHp > 0f && currentHp <= maxHp * Tuning.EnrageHpRatio)
            StartCoroutine(EnrageRoutine());
    }
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
        //--- 2026-07-09 판 회전은 전용 연출: 점프해서 그리드 상단을 가격하고 착지 → 그다음 판이 삐걱→넘어짐(ApplyPivot 내부).
        // 그 외 인텐트는 기존 제자리 공격 모션.
        if (CurrentIntent == MonsterIntent.Pivot)
        {
            yield return JumpStrikeRoutine();
            GameManager.Instance.blockGrid.ApplyPivot(3);
            if (!CoopSession.Active) PickIntent();   //--- 2026-07-20 협동은 서버가 인텐트 소유(피벗은 협동 미사용이지만 일관성)
            yield break;
        }

        //--- 2026-07-09 모션 2분화: 물리 공격(줄추가/회색화)=기본공격, 판 조작(중력/은폐/얼림/폭탄/디스펠/정화)=시전(Cast) 모션.
        // 시전 클립(castAttack)이 아직 없으면 기본공격 폴백.
        bool cast = IsCastIntent(CurrentIntent) && AnimHelper.TriggerOrFallback(animator, "castAttack", "basicAttack");
        if (!IsCastIntent(CurrentIntent)) animator.SetTrigger("basicAttack");
        string waitState = cast ? castStateName : attackStateName;

        float guard = 0f;
        yield return new WaitUntil(() =>
        {
            guard += Time.deltaTime;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            return (state.IsName(waitState) && state.normalizedTime >= 0.5f) || guard > Tuning.AttackMaxWait;
        });

        switch (CurrentIntent)
        {
            case MonsterIntent.RowAttack:
                //--- 2026-07-09 광폭화 상태면 줄을 여러 개 추가
                int rows = _enraged ? Tuning.EnrageRows : 1;
                for (int i = 0; i < rows; i++)
                    GameManager.Instance.blockGrid.ShiftAndCreateRow(99, Color.gray);
                break;
            case MonsterIntent.ConvertBlocks:
                GameManager.Instance.blockGrid.ApplyCorruption(Tuning.ConvertSpotCount, Tuning.ConvertSpotRadius);
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
            //--- 2026-07-09 Pivot은 위의 전용 연출 분기로 이동
            // case MonsterIntent.Pivot:
            //     GameManager.Instance.blockGrid.ApplyPivot(3);
            //     break;
            case MonsterIntent.SelfCleanse:
                CleanseSelf();
                break;
            case MonsterIntent.Dispel:
                if (Run.IsInitialized) { Run.stats.shieldStacks = 0; Run.stats.buffAttack = 0f; }
                Debug.Log($"✖ {gameObject.name} 디스펠 → 플레이어 버프 제거");
                break;
            //--- 2026-07-09 신규 패턴
            case MonsterIntent.Freeze:
                GameManager.Instance.blockGrid.ApplyFreeze(Tuning.FreezeCount, Tuning.FreezeTurns);
                break;
            case MonsterIntent.TimeBomb:
                GameManager.Instance.blockGrid.ApplyTimeBomb(Tuning.TimeBombTurns);
                break;
            case MonsterIntent.Devour:
                GameManager.Instance.blockGrid.ApplyDevour(CurrentDevourRadius);   //--- 2026-07-15 삼킬수록 반경↑
                break;
        }

        //--- 2026-07-20 협동은 인텐트/예고를 서버가 소유(CoopSyncPhase가 nextIntent로 세팅) → 로컬 재추첨 금지. 솔로만 다음 인텐트 추첨.
        if (!CoopSession.Active) PickIntent();
    }

    //--- 2026-07-09 모션 분류: 몸으로 때리는 패턴=기본공격, 판을 조작하는 패턴=시전(Cast)
    static bool IsCastIntent(MonsterIntent intent) => intent switch
    {
        MonsterIntent.RowAttack => false,
        MonsterIntent.ConvertBlocks => false,
        MonsterIntent.Pivot => false,   // 피벗은 점프 가격(별도 연출)
        _ => true   // Blind, GravityShift×2, SelfCleanse, Dispel, Freeze, TimeBomb, Devour(입 벌리기 클립)
    };

    //--- 2026-07-09 [피벗 전용 연출] 포물선 점프로 그리드 우상단 타격 지점까지 → 공중 가격(기존 공격 모션 재생) → 착지 복귀.
    // 위치는 PivotActor.extraOffset으로 구동(직접 transform 이동은 PivotActor.LateUpdate가 덮어씀).
    // 전용 점프킥 애니 클립은 막판 리소스 작업 때 교체.
    IEnumerator JumpStrikeRoutine()
    {
        var fx = FxTuning.I;
        var grid = GameManager.Instance != null ? GameManager.Instance.blockGrid : null;

        // 타격 지점: 그리드 우상단 모서리 + 오프셋 (그리드 없으면 제자리 공격 폴백)
        Vector3 disp = Vector3.zero;
        if (grid != null)
        {
            Vector3 strikePos = new Vector3(
                grid.data.width - 1 + fx.lungeTargetOffset.x,
                grid.data.height - 1 + fx.lungeTargetOffset.y, transform.position.z);
            disp = strikePos - transform.position;
        }

        yield return HopOffset(Vector3.zero, disp, fx.lungeDuration, fx.lungeArc);   // 점프해 들어감

        animator.SetTrigger("basicAttack");   // 공중 가격 (임시: 기본 공격 모션)
        float guard = 0f;
        yield return new WaitUntil(() =>
        {
            guard += Time.deltaTime;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            return (state.IsName(attackStateName) && state.normalizedTime >= 0.5f) || guard > Tuning.AttackMaxWait;
        });

        yield return HopOffset(disp, Vector3.zero, fx.lungeDuration, fx.lungeArc);   // 착지 복귀
    }

    // extraOffset을 a→b 포물선으로 보간 (arc = 추가 높이)
    IEnumerator HopOffset(Vector3 a, Vector3 b, float duration, float arc)
    {
        if (_pivotActor == null) yield break;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(duration, 0.01f);
            float lin = Mathf.Clamp01(t);
            Vector3 d = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, lin));
            d.y += arc * 4f * lin * (1f - lin);   // 포물선: 중간 지점 정점
            _pivotActor.extraOffset = d;
            yield return null;
        }
        _pivotActor.extraOffset = b;
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