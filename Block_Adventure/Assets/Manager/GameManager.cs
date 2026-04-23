using UnityEngine;
using System.Collections;

public enum GameState
{
    PlayerTurn,
    Calculate,
    MonsterTurn,
    GameOverCheck,
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

    [Header("Monster Settings")]
    public int monsterAttackInterval = 3;

    public GameState CurrentState { get; private set; }

    private bool _isPlayerInputDone;
    private int _turnCount;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        spawner = FindObjectOfType<BlockSpawner>();
        blockGrid = FindObjectOfType<BlockGrid>();
        battleManager = FindObjectOfType<BattleManager>();
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

            GameOverCheckPhase();
        }
    }

    IEnumerator PlayerTurnPhase()
    {
        CurrentState = GameState.PlayerTurn;
        _isPlayerInputDone = false;

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
    }

    void GameOverCheckPhase()
    {
        CurrentState = GameState.GameOverCheck;

        if (blockGrid.IsGameOver())
        {
            SetGameOver();
            return;
        }

        if (battleManager != null && !battleManager.HasLivingMonster())
        {
            SetGameClear();
        }
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
