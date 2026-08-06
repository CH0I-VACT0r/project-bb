using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(EnemyStatManager), typeof(EnemyAggroController))]
public class EnemyMeleeController : NetworkBehaviour
{
    private EnemyStatManager statManager;
    private EnemyAggroController aggroController;

    void Awake()
    {
        statManager = GetComponent<EnemyStatManager>();
        aggroController = GetComponent<EnemyAggroController>();
    }

    void Update()
    {
        // 기절, 공포 등 CC에 걸려있으면 이동 로직 정지
        if (!IsServer || statManager.isStunned || GetComponent<StatusEffectManagerNetcode>().isFeared.Value)
            return;

        Transform target = aggroController.CurrentTarget;
        if (target == null) return;

        // 타겟 방향으로 계속 이동 (충돌 처리는 EnemyContactDamage가 알아서 함)
        float currentMoveSpeed = statManager.enemyData.moveSpeed * GetComponent<StatusEffectManagerNetcode>().moveSpeedMultiplier.Value;
        Vector2 direction = (target.position - transform.position).normalized;
        transform.Translate(direction * currentMoveSpeed * Time.deltaTime);
    }
}
