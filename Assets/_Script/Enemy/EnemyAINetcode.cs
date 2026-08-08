using Unity.Netcode;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D), typeof(EnemyStatManager))]
public class EnemyAINetcode : NetworkBehaviour
{
    private EnemyStatManager statManager;
    private StatusEffectManagerNetcode statusManager;
    private EnemyVisualNetcode visualNetcode;
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

    [Header("Animation Delays")]
    public float attackWindupTime = 0.2f;
    public float deathAnimationDuration = 0.5f;

    // 원거리 공격 쿨타임 추적용
    private float lastAttackTime = 0f;
    private bool isAttacking = false;
    private bool isDead = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        statManager = GetComponent<EnemyStatManager>();
        statusManager = GetComponent<StatusEffectManagerNetcode>();
        visualNetcode = GetComponent<EnemyVisualNetcode>();

        separationFilter.useLayerMask = true;
        separationFilter.layerMask = enemyLayer;
        separationFilter.useTriggers = true;
    }

    private void Update()
    {
        // 서버가 아니거나, 죽었거나, 이미 공격 중이면 Update 연산 중지
        if (!IsServer || isDead || isAttacking) return;

        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            FindNearestPlayer();
            searchTimer = searchInterval;
        }

        // --- 공격 판정 로직 ---
        if (targetPlayer != null && !statManager.isStunned)
        {
            float dist = Vector2.Distance(transform.position, targetPlayer.position);
            var data = statManager.enemyData;

            if (dist <= data.attackRange)
            {
                if (Time.time - lastAttackTime >= data.attackCooldown)
                {
                    // 원거리 적일 경우
                    if (data.attackType == EnemyAttackType.Projectile)
                    {
                        StartCoroutine(RangedAttackRoutine(data, targetPlayer.position));
                    }
                    // 근접 적일 경우 (애니메이션만 재생, 실제 대미지는 OnCollision에서 처리됨)
                    else if (data.attackType == EnemyAttackType.Melee)
                    {
                        StartCoroutine(MeleeAttackRoutine());
                    }

                    lastAttackTime = Time.time;
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer || isDead || isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

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
    // --- 공격 코루틴 로직 ---
    private IEnumerator RangedAttackRoutine(EnemyDataSO data, Vector2 targetPosition)
    {
        isAttacking = true;
        visualNetcode.TriggerAttackAnimation(); // 공격 애니메이션 시작

        // 애니메이션이 무기를 휘두르거나 발사하는 프레임까지 대기
        yield return new WaitForSeconds(attackWindupTime);

        // 지연 시간 동안 적이 죽지 않았을 때만 실제 투사체 생성
        if (!isDead)
        {
            FireProjectiles(data, targetPosition);
        }

        isAttacking = false; // 다시 이동 및 추적 재개
    }

    private IEnumerator MeleeAttackRoutine()
    {
        isAttacking = true;
        visualNetcode.TriggerAttackAnimation();

        yield return new WaitForSeconds(attackWindupTime);

        isAttacking = false;
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

    public void HandleDeath()
    {
        if (!IsServer || isDead) return;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero; // 즉시 정지

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;  // 콜라이더 비활성화

        visualNetcode.TriggerDeathAnimation();
        yield return new WaitForSeconds(deathAnimationDuration);

        // 풀링/파괴 로직 실행
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}