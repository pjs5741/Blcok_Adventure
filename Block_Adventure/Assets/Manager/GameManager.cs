using UnityEngine;
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

    void Awake()
    {
        Instance = this;
        spawner = FindFirstObjectByType<BlockSpawner>();
        blockGrid = FindFirstObjectByType<BlockGrid>();
        battleManager = FindFirstObjectByType<BattleManager>();
        rewardManager = FindFirstObjectByType<RewardManager>();
    }

    void Start()
    {
        StartCoroutine(GameLoop());
    }

    IEnumerator GameLoop()
    {
        yield return new WaitForSeconds(0.3f);

        while (CurrentState != GameState.GameOver && CurrentState != GameState.GameClear)
        {
            yield return StartCoroutine(PlayerTurnPhase());
            if (IsTerminated()) break; 

            yield return StartCoroutine(CalculatePhase());
            if (IsTerminated()) break;

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
        spawner.SpawnBlock();

        yield return new WaitUntil(() => _isPlayerInputDone);
    }

    IEnumerator CalculatePhase()
    {
        CurrentState = GameState.Calculate;
        yield return StartCoroutine(blockGrid.ProcessTurn());
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

    IEnumerator GameOverCheckPhase()
    {
        CurrentState = GameState.GameOverCheck;

        if (blockGrid.IsGameOver())
        {
            SetGameOver();
            yield break;
        }

        if (battleManager != null && !battleManager.HasLivingMonster())
        {
            yield return StartCoroutine(RewardPhase());
            // [TEST] 실제로는 맵 씬으로 전환 후 다음 노드 선택해야 함
            battleManager.RespawnMonster();
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
