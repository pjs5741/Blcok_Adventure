using UnityEngine;
using System.Collections;

public enum GameState { None, Spawn, Input, Logic, Attack, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    public BlockSpawner spawner;
    public GameGrid gameGrid;
    public BattleManager battleManager;

    public GameState CurrentState { get; private set; }

    void Awake() => Instance = this;

    void Start()
    {
        StartCoroutine(GameLoop()); // 게임 시작!
    }

    // ★ 이 루프가 게임의 모든 흐름을 통제합니다. (이거 하나만 보면 게임 흐름 파악 가능)
    IEnumerator GameLoop()
    {
        while (true) // 무한 반복 (턴제)
        {
            // 1. [생성 페이즈] 블록이 없으면 생성
            CurrentState = GameState.Spawn;
            spawner.SpawnBlock(); 
            // 블록이 자리를 잡을 때까지(초기화) 잠깐 대기
            yield return new WaitForSeconds(0.1f); 

            // 2. [입력 페이즈] 플레이어가 조작할 때까지 대기
            CurrentState = GameState.Input;
            // 사용자가 스왑하거나 블록을 놓을 때까지 무한 대기
            // (PangMovement가 조작을 마치면 isInputFinished를 true로 바꿈)
            yield return new WaitUntil(() => isInputFinished); 
            isInputFinished = false; // 리셋

            // 3. [로직 페이즈] 매칭/폭발/중력 (여기가 제일 복잡했던 부분)
            CurrentState = GameState.Logic;
            
            bool hasMatch = false;
            do 
            {
                // Grid야, 터질 거 계산해서 명단 내놔 (파괴 안 함, 계산만)
                var comboList = gameGrid.CalculateMatches(); 

                if (comboList.Count > 0)
                {
                    hasMatch = true;
                    // Grid야, 명단에 있는 애들 연출 보여주고 지워
                    yield return StartCoroutine(gameGrid.AnimateAndDestroy(comboList));
                    
                    // Grid야, 빈칸 채워 (중력)
                    gameGrid.ApplyGravity();
                    yield return new WaitForSeconds(0.3f); // 떨어지는 시간 대기
                }
                else
                {
                    hasMatch = false;
                }
            } while (hasMatch); // 연쇄 폭발이 없을 때까지 반복

            // 4. [공격 페이즈] 다 터졌으니 정산해서 때리기
            CurrentState = GameState.Attack;
            // 이번 턴 콤보 수만큼 공격
            battleManager.ExecuteAttack(gameGrid.CurrentComboCount);
            
            // 콤보 초기화
            gameGrid.ResetTurnData(); 

            // 다시 1번(Spawn)으로 돌아감
        }
    }

    // PangMovement 등에서 호출할 변수
    public bool isInputFinished = false;
    public void FinishInput() => isInputFinished = true;
}