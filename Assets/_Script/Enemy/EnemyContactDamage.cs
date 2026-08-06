using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(EnemyStatManager))]
public class EnemyContactDamage : MonoBehaviour
{
    private EnemyStatManager statManager;
    private float lastAttackTime;
    public float attackCooldown = 1.0f; // 다단 히트 방지

    void Awake()
    {
        statManager = GetComponent<EnemyStatManager>();
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (Time.time - lastAttackTime < attackCooldown) return;

        IDamageable target = collision.gameObject.GetComponent<IDamageable>();
        if (target != null && collision.gameObject.CompareTag("Player"))
        {
            ApplyDamageToTarget(target, collision.transform.position);
            lastAttackTime = Time.time;
        }
    }

    private void ApplyDamageToTarget(IDamageable target, Vector3 targetPos)
    {
        var data = statManager.enemyData;
        Vector3 knockbackDir = (targetPos - transform.position).normalized;

        DamageInfo info = new DamageInfo
        {
            damageAmount = data.baseDamage,
            attackType = data.attackAttribute,
            attackerAccuracy = data.accuracy,
            attackerPenetration = (data.attackAttribute == AttackAttribute.Physical) ? data.physicalPenetration : data.magicPenetration,
            knockbackDir = knockbackDir,
            knockbackForce = data.knockbackForce,

            // SO에 정의된 속성/CC를 묻힘
            elementTypes = data.inflictElement,
            elementBuildUp = data.elementBuildupAmount,
            elementDotDamage = data.elementDotDamage,
            directStatusEffects = data.inflictCC
        };

        target.TakeDamage(info);
    }
}