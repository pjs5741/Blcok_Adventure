using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public enum GameState
{
    PlayerTurn,
    Calculate,
    MonsterTurn,
    GameOverCheck,
    Dead,        //--- 2026-07-15 데드 페이즈: 몬스터 사망 → 데스모션 완료 대기 → 보상
    Reward,
    GameOver,
    GameClear
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Components")]
    public BlockSpawner spawner;
    public BlockGrid blockGrid;
    public BattleManager battleManager;
    public RewardManager rewardManager;

    [Header("Monster Settings")]
    public int monsterAttackInterval = 3;

    public GameState CurrentState { get; private set; }

    private bool _isPlayerInputDone;
    private int _turnCount;

    //--- 2026-07-01 협동(멀티) 상태
    private bool _coopResolved, _coopPartnerLeft;
    private CoopClient.ResolveData _coopResolve;
    //--- 2026-07-20 협동 예고(텔레그래프): 마지막으로 예고한 인텐트. nextIntent가 바뀔 때만 다시 그림(매 턴 재계산하면 예고 칸이 움직여 회피 불가).
    private string _coopTelegraphedIntent;

    void Awake()
    {
        Instance = this;
        UIScale.FixAll();   //--- 2026-07-03 캔버스 해상도 스케일 통일(창 크기 달라도 UI 안 깨지게)
        spawner = FindFirstObjectByType<BlockSpawner>();
        blockGrid = FindFirstObjectByType<BlockGrid>();
        battleManager = FindFirstObjectByType<BattleManager>();
        rewardManager = FindFirstObjectByType<RewardManager>();

        //--- 2026-06-30 덱 뷰어(D키) 부착
        if (GetComponent<DeckViewer>() == null) gameObject.AddComponent<DeckViewer>();
        //--- 2026-07-01 플레이어 버프(방패/공격) 표시 부착
        if (GetComponent<PlayerBuffUI>() == null) gameObject.AddComponent<PlayerBuffUI>();
    }

    void Start()
    {
        //--- 2026-07-01 전투 시작 시 플레이어 버프(방패/공격버프) 초기화 (전투 단위)
        if (Run.IsInitialized) Run.stats.ResetBattleBuffs();

        //--- 2026-07-03 튜토리얼: 싱글=최초 1회 / 협동=협동 런당 1회 무조건 표시
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            if (CoopSession.Active)
            {
                if (!CoopSession.TutorialShown) { TutorialOverlay.Create(canvas.transform); CoopSession.TutorialShown = true; }
            }
            else TutorialOverlay.ShowOnce(canvas.transform);
        }

        if (CoopSession.Active) SetupCoop(canvas);

        StartCoroutine(GameLoop());
    }

    //--- 2026-07-01 협동 초기화: 공유 몬스터 HP, 상대 미니뷰, 캐릭터 2명, 서버 이벤트 구독
    void SetupCoop(Canvas canvas)
    {
        var m = battleManager != null ? battleManager.currentMonster : null;
        if (m != null)
        {
            //--- 2026-07-03 양쪽이 같은 몬스터(시드로 프로필 동기화) 스폰
            var node = Run.mapState != null ? Run.mapState.GetNode(Run.mapState.currentNodeId) : null;
            if (node != null) m.SetProfile(MonsterRegistry.GetForNode(node.type, CoopSession.MonsterSeed));
            if (CoopSession.MonsterMaxHp > 0) m.SetMaxHp(CoopSession.MonsterMaxHp);
            m.SetAttackCountdown(CoopSession.AttackCountdown);   //--- 2026-07-03 서버 동기 카운트다운 초기값
        }

        if (canvas != null)
        {
            var pv = GetComponent<CoopPartnerView>() ?? gameObject.AddComponent<CoopPartnerView>();
            pv.Ensure(canvas.transform);
        }

        SpawnCoopBuddy();

        var c = CoopClient.Instance;
        if (c != null)
        {
            c.OnResolve += OnCoopResolve;
            c.OnPartnerLeft += OnCoopPartnerLeft;
            c.OnPartnerGrid += OnCoopPartnerGrid;   // 실시간 상대 그리드
            c.OnBattleEnd += OnCoopBattleEnd;       // 전투 승리 → 맵 복귀/클리어
            c.OnWaitPartner += OnCoopWaitPartner;   // 내가 락 완료, 상대 대기
        }
    }

    //--- 2026-07-03 상대 대기 표시(내가 먼저 락 → 상대 기다리는 중)
    private GameObject _waitOverlay;
    void OnCoopWaitPartner() => ShowWaitOverlay(true);

    void ShowWaitOverlay(bool show)
    {
        if (show && _waitOverlay == null)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            _waitOverlay = UIBuilder.NewUI("CoopWait", canvas.transform, typeof(RectTransform), typeof(Image));
            UIBuilder.Stretch((RectTransform)_waitOverlay.transform);
            _waitOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            var t = UIBuilder.Text(_waitOverlay.transform, "T", "상대 플레이어를 기다리는 중...", 44, Color.white, TextAnchor.MiddleCenter);
            UIBuilder.SetAnchors(t.rectTransform, new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.58f));
        }
        if (_waitOverlay != null) _waitOverlay.SetActive(show);
    }

    //--- 2026-07-06 협동 전투 종료: 보상(개인별) → 그리드 저장(유지) → 보스면 클리어, 아니면 맵 복귀
    private bool _coopEnded;   // 협동 전투 종료 → 게임 루프 정지(보상 중 블록 재스폰 방지)
    void OnCoopBattleEnd(int nodeId) { _coopEnded = true; StartCoroutine(CoopBattleEndRoutine(nodeId)); }

    IEnumerator CoopBattleEndRoutine(int nodeId)
    {
        var node = Run.mapState != null ? Run.mapState.GetNode(nodeId) : null;

        //--- 2026-07-15 데드 페이즈(협동): 몬스터가 죽었으면 데스모션을 끝까지 재생한 뒤 보상 표시
        var deadMon = battleManager != null ? battleManager.currentMonster : null;
        if (deadMon != null && deadMon.IsDead)
            yield return new WaitWhile(() => deadMon.IsDeathPlaying);

        // 보상: 각자 자기 덱에 카드 획득(개인별)
        if (rewardManager != null && battleManager != null)
            yield return StartCoroutine(rewardManager.ShowReward(battleManager.currentMonster));

        if (blockGrid != null) blockGrid.SaveSnapshot();   // 협동도 그리드 유지(다음 전투로 이어짐)

        if (node != null && node.type == NodeType.Boss) { EndCoop(true); yield break; }
        SceneManager.LoadScene("MapScene");   // 완료 처리는 MapManager.Start에서
    }

    //--- 2026-07-01 협동: 내 그리드(조작 중 블록 포함)를 상대에게 실시간 전송.
    // 낭비 방지: 최대 5회/초로 체크하되 "직전 전송과 달라졌을 때만" 실제 전송(가만있으면 0회).
    private float _coopGridTimer;
    private int[] _coopLastSent;
    void Update()
    {
        if (!CoopSession.Active) return;
        _coopGridTimer += Time.deltaTime;
        if (_coopGridTimer < 0.2f) return;   // 체크 주기(전송 상한 5회/초)
        _coopGridTimer = 0f;

        var c = CoopClient.Instance;
        if (c == null || !c.Connected || blockGrid == null) return;

        int[] flat = blockGrid.FlattenColors(spawner != null ? spawner.ActiveBlock : null);
        if (GridEquals(flat, _coopLastSent)) return;   // 변화 없으면 전송 생략
        _coopLastSent = flat;
        c.SendGrid(flat);
    }

    static bool GridEquals(int[] a, int[] b)
    {
        if (b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    void OnCoopPartnerGrid(int[] grid) => CoopPartnerView.Instance?.Render(grid);

    // 플레이어 옆에 상대 아바타. 플레이어를 복제하되 로직/입력 스크립트만 제거 → Animator는 남아 애니메이션 재생(움직임).
    void SpawnCoopBuddy()
    {
        var player = FindFirstObjectByType<Player>();
        if (player == null) return;

        var buddy = Instantiate(player.gameObject);
        buddy.name = "CoopBuddy";

        // MonoBehaviour(스크립트)만 전부 제거 — Animator/SpriteRenderer는 Component라 유지되어 애니 계속 재생
        foreach (var mb in buddy.GetComponentsInChildren<MonoBehaviour>(true))
            Destroy(mb);

        buddy.transform.position = player.transform.position + new Vector3(-3.5f, 0.8f, 0f);
        buddy.transform.rotation = player.transform.rotation;
        buddy.transform.localScale = player.transform.localScale;

        foreach (var sr in buddy.GetComponentsInChildren<SpriteRenderer>())
        {
            sr.color = new Color(0.8f, 0.85f, 1f);       // 색조로 구분
            sr.sortingOrder = sr.sortingOrder - 1;
        }

        _coopBuddyAnim = buddy.GetComponentInChildren<Animator>();   // 버디 공격 모션용
    }

    private Animator _coopBuddyAnim;

    IEnumerator GameLoop()
    {
        yield return new WaitForSeconds(0.3f);

        while (CurrentState != GameState.GameOver && CurrentState != GameState.GameClear && !_coopEnded)
        {
            yield return StartCoroutine(PlayerTurnPhase());
            if (IsTerminated()) break;

            //--- 2026-07-01 협동: 이번 턴 내가 준 데미지 계산용, 계산 전 몬스터 HP 캡처
            float coopHpBefore = 0f;
            if (CoopSession.Active)
            {
                var cm = battleManager != null ? battleManager.currentMonster : null;
                coopHpBefore = cm != null ? cm.CurrentHp : 0f;
            }

            yield return StartCoroutine(CalculatePhase());
            if (IsTerminated()) break;

            yield return StartCoroutine(DotPhase());   //--- 2026-06-30 도트(독/화상) 전용 페이즈 (보이게)
            if (IsTerminated()) break;

            //--- 2026-07-15 데드 페이즈: 데미지(계산/도트) 뒤 몬스터가 죽었으면 데스모션을 끝까지 재생하고 보상 → 씬 전환.
            //    죽어가는 몬스터가 다시 공격하거나 다음 플레이어 턴 블록이 스폰되는 것을 방지.
            //    (협동은 승리/보상을 서버 battleEnd(OnCoopBattleEnd)가 처리하므로 여기선 스킵)
            if (!CoopSession.Active && MonsterIsDead())
            {
                yield return StartCoroutine(DeadPhase());
                break;
            }

            //--- 2026-07-01 협동이면 서버 턴 동기화, 아니면 기존 로컬 몬스터 턴
            if (CoopSession.Active)
                yield return StartCoroutine(CoopSyncPhase(coopHpBefore));
            else
                yield return StartCoroutine(MonsterTurnPhase());
            if (IsTerminated()) break;

            //--- 2026-07-20 협동 데드 페이즈: 로컬에서 몬스터가 죽었으면 더 이상 진행(다음 블록 스폰) 말고
            // 서버 battleEnd(OnCoopBattleEnd→CoopBattleEndRoutine)가 데스모션 완료까지 기다린 뒤 보상/전환하도록 대기.
            // (솔로의 DeadPhase와 동일 취지 — 죽어가는 중 블록이 또 스폰되던 문제 방지)
            if (CoopSession.Active && MonsterIsDead())
            {
                float deadGuard = 0f;
                yield return new WaitUntil(() => _coopEnded || _coopPartnerLeft || (deadGuard += Time.deltaTime) > 15f);
                if (_coopPartnerLeft) { EndCoop(false); yield break; }
                break;   // battleEnd가 왔으면 CoopBattleEndRoutine이 보상/전환 처리
            }

            yield return StartCoroutine(GameOverCheckPhase());
        }
    }

    IEnumerator PlayerTurnPhase()
    {
        CurrentState = GameState.PlayerTurn;
        _isPlayerInputDone = false;

        GameEvents.RaiseTurnStart();

        //--- 2026-06-30 인텐트 아래에 "공격까지 남은 턴" 표시
        //--- 2026-07-03 카운트다운: 싱글=로컬 몬스터 소유값 / 협동=서버 동기값(resolve/goNode에서 SetAttackCountdown)
        var monster = battleManager != null ? battleManager.currentMonster : null;
        if (monster != null && !CoopSession.Active)
            monster.SetAttackCountdown(monster.AttackCountdown);

        // 피벗/중력전환 등 그리드 변형 연출이 진행 중이면 끝날 때까지 스폰 대기 (연출 중 스폰하면 위치/크기 불일치 버그)
        yield return new WaitUntil(() => blockGrid == null || !blockGrid.IsBusy);

        spawner.SpawnBlock();

        yield return new WaitUntil(() => _isPlayerInputDone);
    }

    IEnumerator CalculatePhase()
    {
        CurrentState = GameState.Calculate;
        yield return StartCoroutine(blockGrid.ProcessTurn());
    }

    //--- 2026-06-30 도트 페이즈: 몬스터의 독/화상을 보이게 적용 (화상→독, 각 피격 연출 + 잠깐)
    IEnumerator DotPhase()
    {
        var m = battleManager != null ? battleManager.currentMonster : null;
        if (m != null && battleManager.HasLivingMonster() && m.HasDoT)
            yield return StartCoroutine(m.TickDoTRoutine());
    }

    IEnumerator MonsterTurnPhase()
    {
        CurrentState = GameState.MonsterTurn;
        _turnCount++;

        //--- 2026-07-03 공격 타이밍은 몬스터가 소유(주기+방패지연). 일반3/엘리트2/보스1턴.
        var monster = battleManager != null ? battleManager.currentMonster : null;
        if (monster != null && monster.AdvanceTurnAndCheckAttack())
            yield return StartCoroutine(battleManager.ExecuteMonsterAttack());
        else
            yield return null;

        GameEvents.RaiseTurnEnd();
    }

    //--- 2026-07-01 협동 턴 동기화: 내 데미지+그리드 전송 → 서버 resolve 대기 → 공유 몬스터 HP 반영 + 인텐트 실행
    IEnumerator CoopSyncPhase(float hpBefore)
    {
        CurrentState = GameState.MonsterTurn;
        var m = battleManager != null ? battleManager.currentMonster : null;

        //--- 2026-07-06 이번 턴 내 데미지 = 공격(계산됨, 아직 미적용) + 도트(DotPhase에서 HP 감소분). 공격 연출은 resolve에서.
        int atkDmg = blockGrid != null ? Mathf.Max(0, Mathf.RoundToInt(blockGrid.CoopLastAttackDmg)) : 0;
        int dotDmg = m != null ? Mathf.Max(0, Mathf.RoundToInt(hpBefore - m.CurrentHp)) : 0;
        int[] grid = blockGrid != null ? blockGrid.FlattenColors() : new int[0];

        _coopResolved = false; _coopResolve = null;
        CoopClient.Instance?.SendTurnReady(atkDmg + dotDmg, grid);

        float guard = 0f;
        yield return new WaitUntil(() => _coopResolved || _coopPartnerLeft || (guard += Time.deltaTime) > 30f);

        if (_coopPartnerLeft) { EndCoop(false); yield break; }

        if (_coopResolve != null && m != null)
        {
            var player = battleManager != null ? battleManager.player : null;
            int partnerDmg = _coopResolve.partnerDamage;

            //--- 2026-07-06 둘 다 조작 끝난 뒤(지금) 동시에 재생: 내 공격 → 몹 피격, 이어서 상대(버디) 공격 → 몹 피격
            //--- 2026-07-09 넉백급 데미지면 강공격 모션 (솔로와 동일 기준)
            if (atkDmg > 0 && battleManager.HasLivingMonster())
            {
                if (player != null) player.Attack(atkDmg >= m.MaxHp * Tuning.HitKnockbackRatio);
                yield return new WaitForSeconds(Tuning.MonsterTelegraph);
                if (battleManager.HasLivingMonster()) m.TakeDamage(atkDmg);   // 이때 HP 깎임 + 팝업 + 피격
                yield return new WaitWhile(() => battleManager.HasLivingMonster() && m.IsHitReacting);
            }

            if (partnerDmg > 0 && battleManager.HasLivingMonster())
            {
                if (_coopBuddyAnim != null)
                {
                    bool pHeavy = partnerDmg >= m.MaxHp * Tuning.HitKnockbackRatio;
                    if (pHeavy) AnimHelper.TriggerOrFallback(_coopBuddyAnim, "heavyAttack", "basicAttack");
                    else _coopBuddyAnim.SetTrigger("basicAttack");
                }
                yield return new WaitForSeconds(Tuning.MonsterTelegraph);
                if (battleManager.HasLivingMonster()) m.TakeDamage(partnerDmg);
                yield return new WaitWhile(() => battleManager.HasLivingMonster() && m.IsHitReacting);
            }

            m.SetHp(_coopResolve.monsterHp);   // 서버 권위값으로 정합
            //--- 2026-07-20 서버가 사망 확정이면 로컬도 확실히 사망 처리(데스모션 재생 보장) — 솔로와 동일
            if (_coopResolve.monsterDead) m.EnsureDead();
            m.SetAttackCountdown(_coopResolve.attackCountdown);   //--- 서버 동기 카운트다운
            CoopPartnerView.Instance?.Render(_coopResolve.partnerGrid);

            //--- 2026-07-20 이번 턴 발동 인텐트. 지난 사이클에 nextIntent로 이미 예고돼 CurrentIntent+예고칸이 세팅된 상태 →
            // 셀 재계산 없이 그대로 발동(재계산하면 예고 칸이 바뀌어 회피가 무의미해짐). 만약 어긋나 있으면 서버값으로 보정.
            bool fire = !_coopResolve.monsterDead && !string.IsNullOrEmpty(_coopResolve.intent) && _coopResolve.intent != "None";
            if (fire)
            {
                if (m.CurrentIntent.ToString() != _coopResolve.intent) m.SetIntentByName(_coopResolve.intent);
                _coopTelegraphedIntent = null;   // 발동으로 예고 소진 → 다음 nextIntent를 새로 예고
                yield return StartCoroutine(battleManager.ExecuteMonsterAttack());
            }

            //--- 2026-07-20 다음 공격 인텐트를 미리 예고(텔레그래프+아이콘) — 솔로처럼 카운트다운 동안 대비 가능.
            // nextIntent가 바뀔 때만 1회 세팅(매 턴 SetIntentByName하면 예고 칸이 매턴 재추첨되어 회피 불가).
            string next = _coopResolve.nextIntent;
            if (!_coopResolve.monsterDead && !string.IsNullOrEmpty(next) && next != "None" && next != _coopTelegraphedIntent)
            {
                m.SetIntentByName(next);
                _coopTelegraphedIntent = next;
            }
        }
        GameEvents.RaiseTurnEnd();
    }

    //--- 2026-07-09 미사용 죽은 코드 — 협동 인텐트도 SetIntentByName → ExecuteMonsterAttack → AttackCoroutine 경유로 통합됨
    // // 서버가 정한 몬스터 인텐트를 내 그리드에 실행(협동은 피벗/중력 제외)
    // IEnumerator ExecuteCoopIntent(string intent)
    // {
    //     if (string.IsNullOrEmpty(intent) || intent == "None") yield break;   //--- 공격 없는 턴(주기 외)
    //     if (blockGrid != null)
    //     {
    //         switch (intent)
    //         {
    //             case "RowAttack":     blockGrid.ShiftAndCreateRow(99, Color.gray); break;
    //             case "ConvertBlocks": blockGrid.ConvertRandomBlocksToGray(3); break;
    //             case "Blind":         blockGrid.ApplyBlind(3); break;
    //         }
    //     }
    //     yield return new WaitUntil(() => blockGrid == null || !blockGrid.IsBusy);
    // }

    void EndCoop(bool victory)
    {
        StopAllCoroutines();
        CurrentState = victory ? GameState.GameClear : GameState.GameOver;
        Run.lastResult = victory ? RunResult.Victory : RunResult.GameOver;
        CoopClient.Instance?.LeaveRoom();
        CoopSession.Reset();
        SceneManager.LoadScene("EndScene");
    }

    void OnCoopResolve(CoopClient.ResolveData d) { _coopResolve = d; _coopResolved = true; ShowWaitOverlay(false); }
    void OnCoopPartnerLeft() { _coopPartnerLeft = true; }

    void OnDestroy()
    {
        var c = CoopClient.Instance;
        if (c != null)
        {
            c.OnResolve -= OnCoopResolve;
            c.OnPartnerLeft -= OnCoopPartnerLeft;
            c.OnPartnerGrid -= OnCoopPartnerGrid;
            c.OnBattleEnd -= OnCoopBattleEnd;
            c.OnWaitPartner -= OnCoopWaitPartner;
        }
    }

    IEnumerator GameOverCheckPhase()
    {
        CurrentState = GameState.GameOverCheck;

        if (blockGrid.IsGameOver())
        {
            if (CoopSession.Active) { EndCoop(false); yield break; }
            SetGameOver();
            yield break;
        }

        //--- 2026-07-03 협동: 승리(몹 처치)는 서버 battleEnd(OnCoopBattleEnd)가 맵복귀/클리어 처리. 여기선 진행만.
        if (CoopSession.Active) yield break;

        //--- 2026-07-15 몬스터 처치 보상은 데드 페이즈(DeadPhase)로 이관 → 여기서는 플레이어 패배(천장 도달)만 판정.
    }

    //--- 2026-07-15 단일플레이 몬스터 사망 판정 (데스모션 재생 시작 즉시 true)
    bool MonsterIsDead()
    {
        var m = battleManager != null ? battleManager.currentMonster : null;
        return m != null && m.IsDead;
    }

    //--- 2026-07-15 데드 페이즈: 몬스터 데스모션을 끝까지 재생한 뒤 보상 → (보스면 클리어 / 아니면 맵 복귀).
    //    기존 GameOverCheckPhase의 몬스터 처치 보상 로직을 이관.
    IEnumerator DeadPhase()
    {
        CurrentState = GameState.Dead;

        // 데스모션(사망 애니메이션)이 다 끝날 때까지 대기. death 클립 없는 몹은 즉시 통과.
        var m = battleManager != null ? battleManager.currentMonster : null;
        if (m != null)
            yield return new WaitWhile(() => m.IsDeathPlaying);

        yield return StartCoroutine(RewardPhase());

        // 보스 처치면 라운드 클리어
        var currentNode = Run.mapState?.GetNode(Run.mapState.currentNodeId);
        if (currentNode != null && currentNode.type == NodeType.Boss)
        {
            Run.lastResult = RunResult.Victory;
            SceneManager.LoadScene("EndScene");
            yield break;
        }

        blockGrid.SaveSnapshot();
        SceneManager.LoadScene("MapScene");
    }

    IEnumerator RewardPhase()
    {
        CurrentState = GameState.Reward;
        if (rewardManager != null)
            yield return StartCoroutine(rewardManager.ShowReward(battleManager.currentMonster));
    }

    public void OnPlayerInputDone()
    {
        if (CurrentState == GameState.PlayerTurn)
            _isPlayerInputDone = true;
    }

    public void SetGameOver()
    {
        StopAllCoroutines();
        CurrentState = GameState.GameOver;
        Debug.Log("GAME OVER");
        Run.lastResult = RunResult.GameOver;
        SceneManager.LoadScene("EndScene");
    }

    public void SetGameClear()
    {
        StopAllCoroutines();
        CurrentState = GameState.GameClear;
        Debug.Log("GAME CLEAR");
    }

    bool IsTerminated()
    {
        return CurrentState == GameState.GameOver || CurrentState == GameState.GameClear;
    }
}
