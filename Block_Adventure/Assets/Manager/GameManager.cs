/*using UnityEngine;
using System.Collections;

// 게임의 상태를 명확하게 정의
public enum GameState
{
    None,
    Spawn,      // 블록 생성
    Input,      // 플레이어 조작 (대기)
    Logic,      // 매칭 계산 & 중력 (조작 불가)
    Attack,     // 몬스터 공격
    GameOver
}

public class GameManager : MonoBehaviour
{
    // 어디서든 부를 수 있게 싱글톤
    public static GameManager Instance;

    [Header("Components")]
    public BlockSpawner spawner;
    public BlockGrid blockGrid;       // GameGrid 대신 BlockGrid
    public BattleManager battleManager;

    [Header("Status")]
    public GameState CurrentState;
    private bool isInputFinished = false; // 조작 끝났는지 체크하는 깃발

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        StartCoroutine(GameLoop());
    }

    // ★ 게임의 심장 (무한 반복)
    IEnumerator GameLoop()
    {
        // 1. 게임 시작 전 초기화 대기
        yield return new WaitForSeconds(0.5f);

        while (true)
        {
            // =================================================
            // PHASE 1. 생성 (Spawn)
            // =================================================
            CurrentState = GameState.Spawn;
            spawner.SpawnBlock();
            
            // 블록이 생성되고 자리 잡을 때까지 아주 잠깐 대기
            yield return new WaitForSeconds(0.1f);


            // =================================================
            // PHASE 2. 입력 (Input)
            // =================================================
            CurrentState = GameState.Input;
            isInputFinished = false;

            // 플레이어가 블록을 놓거나, 시간이 다 될 때까지 무한 대기
            // (BlockMovement에서 FinishInput()을 부르면 통과됨)
            yield return new WaitUntil(() => isInputFinished);


            // =================================================
            // PHASE 3. 로직 & 연출 (Logic)
            // =================================================
            CurrentState = GameState.Logic;

            bool hasMatch = false;
            do
            {
                // 1. 터질 게 있는지 계산만 함 (파괴 X)
                var matches = blockGrid.CalculateMatches();

                if (matches.Count > 0)
                {
                    hasMatch = true;

                    // 2. 파괴 연출 보여줌 (여기서 시간 끔)
                    yield return StartCoroutine(blockGrid.AnimateAndDestroy(matches));

                    // 3. 빈칸 채우기 (중력)
                    blockGrid.ApplyGravity();
                    
                    // 4. 떨어지는 애니메이션 시간만큼 대기
                    yield return new WaitForSeconds(0.4f); 
                }
                else
                {
                    hasMatch = false;
                }
            } 
            while (hasMatch); // 연쇄 폭발이 일어난다면 계속 반복


            // =================================================
            // PHASE 4. 공격 (Attack)
            // =================================================
            CurrentState = GameState.Attack;

            // 이번 턴에 쌓인 콤보만큼 데미지 배율 계산해서 공격
            if (blockGrid.CurrentComboCount > 0)
            {
                // 배틀매니저가 몬스터 때림 (이벤트 방식이면 생략 가능하지만 직접 호출이 명확함)
                // battleManager.AttackMonster(blockGrid.currentDamageMultiplier);
            }

            // 턴 데이터 초기화
            blockGrid.ResetTurnData();

            // 잠깐 쉬고 다시 생성 단계로!
            yield return new WaitForSeconds(0.2f);
        }
    }

    // 외부(BlockMovement)에서 호출하는 버튼
    public void FinishInput()
    {
        isInputFinished = true;
    }

    // 게임 오버 처리
    public void SetGameOver()
    {
        StopAllCoroutines();
        CurrentState = GameState.GameOver;
        Debug.Log("❌ GAME OVER");
        // UI 띄우기...
    }
}*/

