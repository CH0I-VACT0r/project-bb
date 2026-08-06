using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class EnemyProjectile : NetworkBehaviour
{
    private DamageInfo payload;
    private float speed;
    private Vector2 moveDirection;

    private GameObject originalPrefab;
    private Coroutine lifeTimer;

    // 서버가 투사체를 생성하고 초기값을 주입할 때 호출
    public void Initialize(EnemyDataSO data, float angle)
    {
        originalPrefab = data.projectilePrefab;
        speed = data.projectileSpeed;
        moveDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

        payload = new DamageInfo
        {
            damageAmount = data.baseDamage,
            attackType = data.attackAttribute,
            attackerAccuracy = data.accuracy,
            attackerPenetration = (data.attackAttribute == AttackAttribute.Physical) ? data.physicalPenetration : data.magicPenetration,
            elementTypes = data.inflictElement,
            elementBuildUp = data.elementBuildupAmount,
            elementDotDamage = data.elementDotDamage,
            directStatusEffects = data.inflictCC,
            knockbackDir = moveDirection,
            knockbackForce = data.knockbackForce
        };

        if (lifeTimer != null) StopCoroutine(lifeTimer);
        lifeTimer = StartCoroutine(LifeTimeRoutine(5f));
    }

    void Update()
    {
        if (!IsServer) return;
        transform.Translate(moveDirection * speed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            IDamageable target = other.GetComponent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(payload);
            }
            ReturnToPool();
        }
        else if (other.CompareTag("Wall"))
        {
            ReturnToPool();
        }
    }

    private IEnumerator LifeTimeRoutine(float time)
    {
        yield return new WaitForSeconds(time);
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (lifeTimer != null) StopCoroutine(lifeTimer);
        NetworkProjectilePool.Instance.ReturnProjectile(originalPrefab, GetComponent<NetworkObject>());
    }
}