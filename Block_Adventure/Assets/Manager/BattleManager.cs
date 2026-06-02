using UnityEngine;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    [Header("연결 대상")]
    public Monster currentMonster;
    public Player player;

    void Awake()
    {
        player = FindFirstObjectByType<Player>();
    }

    void Start()
    {
        GameManager.Instance.blockGrid.OnAttackTriggered += OnGridAttack;
    }

    // 실제 공격 처리 함수
    void OnGridAttack(float damageMultiplier)
    {
        if (currentMonster is not null && currentMonster.gameObject.activeSelf)
        {
            // 최종 데미지 = 기본공격력 * 배율
            float finalDamage = player.stats.baseDamage * damageMultiplier;
            
            // 몬스터 때리기
            player.Attack();
            currentMonster.TakeDamage(finalDamage);
        }
    }
    
    public bool HasLivingMonster()
    {
        return currentMonster != null && currentMonster.gameObject.activeSelf;
    }

    public IEnumerator ExecuteMonsterAttack()
    {
        if (HasLivingMonster())
        {
            yield return new WaitUntil(() =>
                !player.animator.GetCurrentAnimatorStateInfo(0).IsName("Player_BasicAttack"));

            player.animator.SetTrigger("hit");
            yield return currentMonster.StartCoroutine(currentMonster.AttackCoroutine());
        }
    }

    // [TEST] 실제로는 맵에서 다음 노드 선택 후 새 몬스터 로드해야 함
    public void RespawnMonster()
    {
        if (currentMonster == null) return;
        currentMonster.gameObject.SetActive(true);
        currentMonster.Init();
    }

    // 게임 꺼질 때 연결 해제 (메모리 관리)
    void OnDestroy()
    {
        if (GameManager.Instance.blockGrid != null)
            GameManager.Instance.blockGrid.OnAttackTriggered -= OnGridAttack;
    }
}