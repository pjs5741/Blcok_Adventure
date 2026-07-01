using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public enum GameState
{
    PlayerTurn,
    Calculate,
    MonsterTurn,
    GameOverCheck,
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
    private bool _coopResolved, _coopMonsterDead, _coopPartnerLeft;
    private CoopClient.ResolveData _coopResolve;

    void Awake()
    {
        Instance = this;
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

        //--- 2026-07-01 첫 전투에 1회만 튜토리얼 자동 표시 (협동에선 생략)
        var canvas = FindFirstObjectByType<Canvas>();
        if (!CoopSession.Active && canvas != null) TutorialOverlay.ShowOnce(canvas.transform);

        if (CoopSession.Active) SetupCoop(canvas);

        StartCoroutine(GameLoop());
    }

    //--- 2026-07-01 협동 초기화: 공유 몬스터 HP, 상대 미니뷰, 캐릭터 2명, 서버 이벤트 구독
    void SetupCoop(Canvas canvas)
    {
        var m = battleManager != null ? battleManager.currentMonster : null;
        if (m != null && CoopSession.MonsterMaxHp > 0) m.SetMaxHp(CoopSession.MonsterMaxHp);

        if (canvas != null)
        {
            var pv = GetComponent<CoopPartnerView>() ?? gameObject.AddComponent<CoopPartnerView>();
            pv.Ensure(canvas.transform);
        }

        SpawnCoopBuddy();

        var c = CoopClient.Instance;
        if (c != null) { c.OnResolve += OnCoopResolve; c.OnPartnerLeft += OnCoopPartnerLeft; }
    }

    // 플레이어 옆에 상대 아바타(시각용). 위치는 라이브에서 미세 조정 필요.
    void SpawnCoopBuddy()
    {
        var player = FindFirstObjectByType<Player>();
        if (player == null) return;
        var psr = player.GetComponentInChildren<SpriteRenderer>();
        if (psr == null) return;

        var buddy = new GameObject("CoopBuddy");
        var sr = buddy.AddComponent<SpriteRenderer>();
        sr.sprite = psr.sprite;
        sr.color = new Color(0.8f, 0.85f, 1f);   // 살짝 다른 색조로 구분
        sr.sortingOrder = psr.sortingOrder - 1;
        buddy.transform.position = psr.transform.position + new Vector3(-3.5f, 0.8f, 0f);
        buddy.transform.localScale = psr.transform.lossyScale;
    }

    IEnumerator GameLoop()
    {
        yield return new WaitForSeconds(0.3f);

        while (CurrentState != GameState.GameOver && CurrentState != GameState.GameClear)
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

            //--- 2026-07-01 협동이면 서버 턴 동기화, 아니면 기존 로컬 몬스터 턴
            if (CoopSession.Active)
                yield return StartCoroutine(CoopSyncPhase(coopHpBefore));
            else
                yield return StartCoroutine(MonsterTurnPhase());
            if (IsTerminated()) break;

            yield return StartCoroutine(GameOverCheckPhase());
        }
    }

    IEnumerator PlayerTurnPhase()
    {
        CurrentState = GameState.PlayerTurn;
        _isPlayerInputDone = false;

        GameEvents.RaiseTurnStart();

        //--- 2026-06-30 인텐트 아래에 "공격까지 남은 턴" 표시
        var monster = battleManager != null ? battleManager.currentMonster : null;
        if (monster != null && monsterAttackInterval > 0)
            monster.SetAttackCountdown(monsterAttackInterval - (_turnCount % monsterAttackInterval));

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

        if (_turnCount % monsterAttackInterval == 0)
        {
            yield return StartCoroutine(battleManager.ExecuteMonsterAttack());
        }
        else
        {
            yield return null;
        }

        GameEvents.RaiseTurnEnd();
    }

    //--- 2026-07-01 협동 턴 동기화: 내 데미지+그리드 전송 → 서버 resolve 대기 → 공유 몬스터 HP 반영 + 인텐트 실행
    IEnumerator CoopSyncPhase(float hpBefore)
    {
        CurrentState = GameState.MonsterTurn;
        var m = battleManager != null ? battleManager.currentMonster : null;
        int dmg = m != null ? Mathf.Max(0, Mathf.RoundToInt(hpBefore - m.CurrentHp)) : 0;
        int[] grid = blockGrid != null ? blockGrid.FlattenColors() : new int[0];

        _coopResolved = false; _coopResolve = null;
        CoopClient.Instance?.SendReady(dmg, grid);

        float guard = 0f;
        yield return new WaitUntil(() => _coopResolved || _coopPartnerLeft || (guard += Time.deltaTime) > 30f);

        if (_coopPartnerLeft) { EndCoop(false); yield break; }

        if (_coopResolve != null && m != null)
        {
            m.SetHp(_coopResolve.monsterHp);
            CoopPartnerView.Instance?.Render(_coopResolve.partnerGrid);
            if (_coopResolve.monsterDead) _coopMonsterDead = true;
            else yield return StartCoroutine(ExecuteCoopIntent(_coopResolve.intent));
        }
        GameEvents.RaiseTurnEnd();
    }

    // 서버가 정한 몬스터 인텐트를 내 그리드에 실행(협동은 피벗/중력 제외)
    IEnumerator ExecuteCoopIntent(string intent)
    {
        if (blockGrid != null)
        {
            switch (intent)
            {
                case "RowAttack":     blockGrid.ShiftAndCreateRow(99, Color.gray); break;
                case "ConvertBlocks": blockGrid.ConvertRandomBlocksToGray(3); break;
                case "Blind":         blockGrid.ApplyBlind(3); break;
            }
        }
        yield return new WaitUntil(() => blockGrid == null || !blockGrid.IsBusy);
    }

    void EndCoop(bool victory)
    {
        StopAllCoroutines();
        CurrentState = victory ? GameState.GameClear : GameState.GameOver;
        Run.lastResult = victory ? RunResult.Victory : RunResult.GameOver;
        CoopClient.Instance?.Leave();
        CoopSession.Reset();
        SceneManager.LoadScene("EndScene");
    }

    void OnCoopResolve(CoopClient.ResolveData d) { _coopResolve = d; _coopResolved = true; }
    void OnCoopPartnerLeft() { _coopPartnerLeft = true; }

    void OnDestroy()
    {
        var c = CoopClient.Instance;
        if (c != null) { c.OnResolve -= OnCoopResolve; c.OnPartnerLeft -= OnCoopPartnerLeft; }
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

        //--- 2026-07-01 협동: 공유 몬스터 처치 = 승리(보상/맵 없음, 베타)
        if (CoopSession.Active)
        {
            if (_coopMonsterDead) EndCoop(true);
            yield break;
        }

        if (battleManager != null && !battleManager.HasLivingMonster())
        {
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
