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
        GameManager.Instance.blockGrid.OnMatchCompleted += OnGridAttack;
    }

    void OnGridAttack(AttackContext ctx)
    {
        if (currentMonster is null || !currentMonster.gameObject.activeSelf) return;

        IconEffects.Apply(ctx, currentMonster);

        player.Attack();

        float finalDamage = player.stats.baseDamage * ctx.damageMultiplier;
        if (finalDamage > 0)
            currentMonster.TakeDamage(finalDamage);
    }

    public bool HasLivingMonster()
    {
        return currentMonster != null && currentMonster.gameObject.activeSelf;
    }

    public IEnumerator ExecuteMonsterAttack()
    {
        if (HasLivingMonster())
        {
            // 방패로 인한 지연 — 한 턴 스킵
            if (!currentMonster.ConsumeAttackDelay())
                yield break;

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
        if (GameManager.Instance != null && GameManager.Instance.blockGrid != null)
            GameManager.Instance.blockGrid.OnMatchCompleted -= OnGridAttack;
    }
}