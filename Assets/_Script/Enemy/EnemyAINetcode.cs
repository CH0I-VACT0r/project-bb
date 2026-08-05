using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAINetcode : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 1.0f;
    public float searchInterval = 0.5f; // 플레이어 탐색 주기

    [Header("Separation Settings")]
    public float separationRadius = 0.8f; // 밀어내는 반경 탐색 크기
    public float separationWeight = 1.0f; // 밀어내는 힘의 가중치
    public LayerMask enemyLayer;

    private Rigidbody2D rb;
    private Transform targetPlayer;
    private float searchTimer = 0f;

    private ContactFilter2D separationFilter;
    private static Collider2D[] separationBuffer = new Collider2D[15];

    private StatusEffectManagerNetcode statusManager;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        if (statusManager != null && statusManager.isStunned.Value)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 targetPos = transform.position;
        bool hasValidTarget = false;

        if (statusManager != null && (statusManager.isTaunted.Value || statusManager.isFeared.Value) && statusManager.effectSourceId.Value != 0)
        {
            // NetworkManager를 통해 시전자의 실제 Transform을 찾아냄
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(statusManager.effectSourceId.Value, out NetworkObject sourceObj))
            {
                if (statusManager.isTaunted.Value)
                {
                    targetPos = sourceObj.transform.position; // 시전자 쪽으로 향함
                }
                else if (statusManager.isFeared.Value)
                {
                    // 시전자 반대편으로 도망치는 좌표 계산
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

        if (hasValidTarget)
        {
            Vector2 directionToTarget = ((Vector2)targetPos - (Vector2)transform.position).normalized;
            Vector2 separationVector = CalculateSeparationVector();
            Vector2 finalDirection = (directionToTarget + (separationVector * separationWeight)).normalized;

            float currentSpeed = moveSpeed * (statusManager != null ? statusManager.moveSpeedMultiplier.Value : 1f);
            rb.linearVelocity = finalDirection * currentSpeed;
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

    // 서버 전용: 분리 벡터 연산
    private Vector2 CalculateSeparationVector()
    {
        Vector2 repulsionForce = Vector2.zero;

        // NonAlloc을 사용하여 메모리 할당 없이 주변 적 탐색
        int hitCount = Physics2D.OverlapCircle(transform.position, separationRadius, separationFilter, separationBuffer);

        for (int i = 0; i < hitCount; i++)
        {
            if (separationBuffer[i].gameObject == this.gameObject) continue; // 자기 자신 제외

            Vector2 diff = transform.position - separationBuffer[i].transform.position;
            float distance = diff.magnitude;

            if (distance > 0f)
            {
                // 거리가 가까울수록 밀어내는 힘이 강해지도록 반비례 연산
                repulsionForce += (diff.normalized / distance);
            }
        }

        return repulsionForce.normalized;
    }
}