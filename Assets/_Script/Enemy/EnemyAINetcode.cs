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

    public Transform TargetPlayer => targetPlayer;

    [HideInInspector]
    public bool canAutoAttack = true;

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

    [Header("Special Pattern (Optional)")]
    public BossPatternBase specialAttackPattern; // 일반 몹도 동일한 모듈 사용
    private bool isExecutingSpecialPattern = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        statManager = GetComponent<EnemyStatManager>();
        statusManager = GetComponent<StatusEffectManagerNetcode>();
        visualNetcode = GetComponent<EnemyVisualNetcode>();

        // Inspector에서 수동 할당이 안 되어 있으면, 같은 오브젝트에서 자동 탐색
        if (specialAttackPattern == null)
        {
            specialAttackPattern = GetComponent<BossPatternBase>();
        }

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
        if (canAutoAttack && targetPlayer != null && !statManager.isStunned && !isExecutingSpecialPattern)
        {
            float dist = Vector2.Distance(transform.position, targetPlayer.position);
            var data = statManager.enemyData;

            if (dist <= data.attackRange)
            {
                // 쿨타임 체크 (특수 패턴과 평타 모두 동일한 쿨타임 사용)
                if (Time.time - lastAttackTime >= data.attackCooldown)
                {
                    if (specialAttackPattern != null)
                    {
                        // 특수 패턴(대쉬, 장판 등) 실행
                        isExecutingSpecialPattern = true;
                        rb.linearVelocity = Vector2.zero;

                        specialAttackPattern.ExecutePattern(targetPlayer, () =>
                        {
                            isExecutingSpecialPattern = false;
                            lastAttackTime = Time.time;
                        });
                    }
                    else
                    {
                        // 특수 패턴이 없는 일반 몹 평타 실행
                        if (data.attackType == EnemyAttackType.Projectile)
                            StartCoroutine(RangedAttackRoutine(data, targetPlayer.position));
                        else if (data.attackType == EnemyAttackType.Melee)
                            StartCoroutine(MeleeAttackRoutine());

                        lastAttackTime = Time.time;
                    }
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

        if (isExecutingSpecialPattern)
        {
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