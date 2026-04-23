using UnityEngine;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    [Header("연결 대상")]
    public BlockGrid blockGrid;   // 공격 신호를 보낼 그리드
    public Monster currentMonster; // 맞을 몬스터
    public Player player;
    
    [Header("데미지 설정")]
    public float baseDamage = 100f; // 블록 1번 터질 때 기본 데미지

    void Awake()
    {
        player = FindFirstObjectByType<Player>();
    }
    void Start()
    {
        // 그리드가 없으면 에러 나니까 체크
        if (blockGrid != null)
        {
            // "그리드에서 공격 신호(OnAttackTriggered)가 오면 -> OnGridAttack을 실행해라"
            blockGrid.OnAttackTriggered += OnGridAttack;
        }
    }

    // 실제 공격 처리 함수
    void OnGridAttack(float damageMultiplier)
    {
        if (currentMonster is not null && currentMonster.gameObject.activeSelf)
        {
            // 최종 데미지 = 기본공격력 * 배율
            float finalDamage = baseDamage * damageMultiplier;
            
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
            yield return currentMonster.StartCoroutine(currentMonster.AttackCoroutine());
    }

    // 게임 꺼질 때 연결 해제 (메모리 관리)
    void OnDestroy()
    {
        if (blockGrid != null)
            blockGrid.OnAttackTriggered -= OnGridAttack;
    }
}