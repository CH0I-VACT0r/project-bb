using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(EnemyStatManager))]
public class EnemyRangedController : NetworkBehaviour
{
    private EnemyStatManager statManager;
    private EnemyAggroController aggroController;
    private Transform target; // 추적할 플레이어
    private float lastAttackTime;

    void Awake()
    {
        statManager = GetComponent<EnemyStatManager>();
    }

    void Update()
    {
        // 서버에서만 AI 이동 및 공격 로직 수행
        if (!IsServer || statManager.isStunned) return;

        Transform target = aggroController.CurrentTarget;
        if (target == null) return;

        float distanceToTarget = Vector2.Distance(transform.position, target.position);
        var data = statManager.enemyData;

        if (distanceToTarget > data.attackRange)
        {
            // 사거리 밖: 플레이어 쪽으로 이동
            MoveTowardsTarget(data.moveSpeed);
        }
        else
        {
            // 사거리 안: 이동 멈춤
            StopMoving();

            // 쿨타임 체크 후 공격
            if (Time.time - lastAttackTime >= data.attackCooldown)
            {
                FireProjectiles(data);
                lastAttackTime = Time.time;
            }
        }
    }

    private void MoveTowardsTarget(float speed)
    {
        Vector2 direction = (target.position - transform.position).normalized;
        transform.Translate(direction * speed * Time.deltaTime);
    }

    private void StopMoving()
    {
        // Rigidbody를 쓴다면 velocity를 0으로 만들거나, Translate 이동을 하지 않음으로써 정지
    }

    private void FireProjectiles(EnemyDataSO data)
    {
        if (data.projectilePrefab == null) return;

        Vector2 directionToTarget = (target.position - transform.position).normalized;
        float baseAngle = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;

        // 발사 개수에 따라 부채꼴(Spread) 각도 계산
        float startAngle = baseAngle - (data.spreadAngle * (data.projectileCount - 1) / 2f);

        for (int i = 0; i < data.projectileCount; i++)
        {
            float currentAngle = startAngle + (data.spreadAngle * i);
            Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);

            // 투사체 생성 (서버 공간)
            GameObject proj = Instantiate(data.projectilePrefab, transform.position, rotation);

            // 네트워크 스폰 (클라이언트 화면에 보이게 함)
            NetworkObject netObj = proj.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Spawn();

            // 투사체에 데이터 주입
            EnemyProjectile projectileScript = proj.GetComponent<EnemyProjectile>();
            if (projectileScript != null)
            {
                projectileScript.Initialize(data, currentAngle);
            }
        }
    }
}
