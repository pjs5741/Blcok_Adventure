using UnityEngine;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    [Header("연결 대상")]
    public Monster currentMonster;
    public Player player;

    [Header("연출")]
    public float monsterTelegraph = Tuning.MonsterTelegraph;   //--- 몬스터 공격 전 예고 멈춤(초)
    public string playerAttackState = "Player_BasicAttack";    //--- 플레이어 공격 애니 상태명(애니 변경 시 인스펙터에서 수정)

    void Awake()
    {
        player = FindFirstObjectByType<Player>();
    }

    //--- 2026-06-30 이벤트(OnMatchCompleted) 구독 제거 → BlockGrid가 AttackRoutine을 직접 호출하고 모션 완료까지 대기.

    // 블록 매칭 공격 한 번: 효과 적용 → 플레이어 공격 → (공격 모션 끝까지) → 몬스터 피격 → (피격 연출 끝까지).
    // 시간 예측(고정 대기) 없이 실제 애니메이션/연출 완료를 기다린다.
    public IEnumerator AttackRoutine(AttackContext ctx)
    {
        if (!HasLivingMonster()) yield break;

        IconEffects.Apply(ctx, currentMonster);

        player.Attack();
        yield return WaitAnimDone(player.animator, playerAttackState);   // 공격 모션 끝까지

        float finalDamage = player.stats.EffectiveDamage * ctx.damageMultiplier;
        if (finalDamage > 0 && HasLivingMonster())
            currentMonster.TakeDamage(finalDamage);

        // 몬스터 피격 연출 끝까지 (죽으면 즉시 통과)
        yield return new WaitWhile(() => HasLivingMonster() && currentMonster.IsHitReacting);
    }

    // 트리거 후 해당 상태에 진입했다가 끝(normalizedTime>=1)날 때까지 대기. 진입 안 되면 안전상 빠져나옴(무한대기 방지).
    static IEnumerator WaitAnimDone(Animator anim, string stateName)
    {
        if (anim == null) yield break;
        float guard = 0f;
        while (!anim.GetCurrentAnimatorStateInfo(0).IsName(stateName) && guard < Tuning.AnimWaitGuard)
        {
            guard += Time.deltaTime;
            yield return null;
        }
        while (anim.GetCurrentAnimatorStateInfo(0).IsName(stateName) &&
               anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;
    }

    public bool HasLivingMonster()
    {
        return currentMonster != null && currentMonster.gameObject.activeSelf;
    }

    public IEnumerator ExecuteMonsterAttack()
    {
        if (HasLivingMonster())
        {
            //--- 2026-07-01 누적 방패 소모 → 공격 1턴 지연. 방패가 충분하면 이번 공격 스킵.
            if (Run.stats.shieldStacks >= Tuning.ShieldPerDelay)
            {
                Run.stats.shieldStacks -= Tuning.ShieldPerDelay;
                Debug.Log($"🛡 방패 {Tuning.ShieldPerDelay} 소모 → 몬스터 공격 1턴 지연 (남은 방패 {Run.stats.shieldStacks})");
                yield break;
            }

            // 몬스터 자체 지연(있으면) — 한 턴 스킵
            if (!currentMonster.ConsumeAttackDelay())
                yield break;

            //--- 2026-07-01 무한 대기 방지: 플레이어 공격 상태가 안 끝나도 가드 시간 지나면 통과
            float exitGuard = 0f;
            yield return new WaitUntil(() =>
                (exitGuard += Time.deltaTime) > Tuning.AnimWaitGuard ||
                !player.animator.GetCurrentAnimatorStateInfo(0).IsName("Player_BasicAttack"));

            //--- 2026-06-30 공격 예고(텔레그래프) — 인텐트 보고 대비할 틈. 너무 빨리 때리던 문제
            yield return new WaitForSeconds(monsterTelegraph);

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

}