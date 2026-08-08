using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(EnemyStatManager))]
public class EnemyContactDamage : MonoBehaviour
{
    private EnemyStatManager statManager;
    private float lastAttackTime;
    public float attackCooldown = 1.0f; // 다단 히트 방지

    private IDamageable currentTarget;
    private Transform currentTargetTransform;

    void Awake()
    {
        statManager = GetComponent<EnemyStatManager>();
    }

    void Update()
    {
        // 넷코드 서버 권한 체크
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;

        // 닿아있는 타겟이 있다면 쿨타임마다 대미지 적용
        if (currentTarget != null)
        {
            if (Time.time - lastAttackTime >= attackCooldown)
            {
                ApplyDamageToTarget(currentTarget, currentTargetTransform.position);
                lastAttackTime = Time.time;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // GetComponent 또는 GetComponentInParent를 사용하여 인터페이스 탐색
            currentTarget = other.GetComponentInParent<IDamageable>();

            // 타겟(스크립트)을 못 찾은 경우 대비 (Null 체크)
            if (currentTarget != null)
            {
                currentTargetTransform = other.transform;
                ApplyDamageToTarget(currentTarget, currentTargetTransform.position);
                lastAttackTime = Time.time;
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            currentTarget = null;
            currentTargetTransform = null;
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