using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(EnemyStatManager))]
public class EnemyAINetcode : NetworkBehaviour
{
    private EnemyStatManager statManager;
    private StatusEffectManagerNetcode statusManager;
    private Rigidbody2D rb;

    [Header("AI Settings")]
    public float searchInterval = 0.5f;
    private float searchTimer = 0f;
    private Transform targetPlayer;

    [Header("Separation Settings (겹침 방지)")]
    public float separationRadius = 0.8f;
    public float separationWeight = 1.0f;
    public LayerMask enemyLayer;
    private ContactFilter2D separationFilter;
    private static Collider2D[] separationBuffer = new Collider2D[15];

    // 원거리 공격 쿨타임 추적용
    private float lastAttackTime = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        statManager = GetComponent<EnemyStatManager>();
        statusManager = GetComponent<StatusEffectManagerNetcode>();

        separationFilter.useLayerMask = true;
        separationFilter.layerMask = enemyLayer;
        separationFilter.useTriggers = true;
    }

    private void Update()
    {
        if (!IsServer) return;

        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            FindNearestPlayer();
            searchTimer = searchInterval;
        }

        // --- 원거리 공격 발사 로직 (Update에서 처리) ---
        if (targetPlayer != null && !statManager.isStunned && statManager.enemyData.attackType == EnemyAttackType.Projectile)
        {
            float dist = Vector2.Distance(transform.position, targetPlayer.position);
            if (dist <= statManager.enemyData.attackRange)
            {
                if (Time.time - lastAttackTime >= statManager.enemyData.attackCooldown)
                {
                    FireProjectiles(statManager.enemyData, targetPlayer.position);
                    lastAttackTime = Time.time;
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        // 기절 시 완전 정지
        if (statusManager != null && statusManager.isStunned.Value)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 targetPos = transform.position;
        bool hasValidTarget = false;
        var data = statManager.enemyData;

        // 도발 / 공포
        if (statusManager != null && (statusManager.isTaunted.Value || statusManager.isFeared.Value) && statusManager.effectSourceId.Value != 0)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(statusManager.effectSourceId.Value, out NetworkObject sourceObj))
            {
                if (statusManager.isTaunted.Value)
                {
                    targetPos = sourceObj.transform.position;
                }
                else if (statusManager.isFeared.Value)
                {
                    Vector2 runDir = ((Vector2)transform.position - (Vector2)sourceObj.transform.position).normalized;
                    targetPos = (Vector2)transform.position + runDir;
                }
                hasValidTarget = true;
            }
        }
        else if (targetPlayer != null)
        {
            targetPos = targetPlayer.position;
            hasValidTarget = true;
        }

        // 타겟이 있을 때의 이동 연산
        if (hasValidTarget)
        {
            float distToTarget = Vector2.Distance(transform.position, targetPos);

            if (data.attackType == EnemyAttackType.Projectile && distToTarget <= data.attackRange && !(statusManager != null && statusManager.isFeared.Value))
            {
                rb.linearVelocity = Vector2.zero;
            }
            else
            {
                Vector2 directionToTarget = ((Vector2)targetPos - (Vector2)transform.position).normalized;
                Vector2 separationVector = CalculateSeparationVector();
                Vector2 finalDirection = (directionToTarget + (separationVector * separationWeight)).normalized;

                float currentSpeed = data.moveSpeed * (statusManager != null ? statusManager.moveSpeedMultiplier.Value : 1f);
                rb.linearVelocity = finalDirection * currentSpeed;
            }
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void FindNearestPlayer()
    {
        PlayerMovementNetcode[] allPlayers = FindObjectsByType<PlayerMovementNetcode>(FindObjectsSortMode.None);
        float shortestDistance = Mathf.Infinity;
        Transform nearestTarget = null;

        foreach (PlayerMovementNetcode player in allPlayers)
        {
            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                nearestTarget = player.transform;
            }
        }
        targetPlayer = nearestTarget;
    }

    private Vector2 CalculateSeparationVector()
    {
        Vector2 repulsionForce = Vector2.zero;
        int hitCount = Physics2D.OverlapCircle(transform.position, separationRadius, separationFilter, separationBuffer);

        for (int i = 0; i < hitCount; i++)
        {
            if (separationBuffer[i].gameObject == this.gameObject) continue;

            Vector2 diff = transform.position - separationBuffer[i].transform.position;
            float distance = diff.magnitude;

            if (distance > 0f)
            {
                repulsionForce += (diff.normalized / distance);
            }
        }
        return repulsionForce.normalized;
    }

    // --- 원거리 투사체 발사 함수 ---
    private void FireProjectiles(EnemyDataSO data, Vector2 targetPosition)
    {
        if (data.projectilePrefab == null) return;

        Vector2 directionToTarget = (targetPosition - (Vector2)transform.position).normalized;
        float baseAngle = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;
        float startAngle = baseAngle - (data.spreadAngle * (data.projectileCount - 1) / 2f);

        for (int i = 0; i < data.projectileCount; i++)
        {
            float currentAngle = startAngle + (data.spreadAngle * i);
            Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);
            NetworkObject netObj = NetworkProjectilePool.Instance.GetProjectile(data.projectilePrefab, transform.position, rotation);

            var projectileScript = netObj.GetComponent<EnemyProjectile>();
            if (projectileScript != null)
            {
                projectileScript.Initialize(data, currentAngle);
            }
        }
    }
}