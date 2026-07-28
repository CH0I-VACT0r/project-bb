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

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

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

        if (targetPlayer != null)
        {
            // 1. 플레이어를 향한 목표 방향 벡터
            Vector2 directionToTarget = (targetPlayer.position - transform.position).normalized;

            // 2. 주변 적들을 밀어내는 척력 벡터 계산
            Vector2 separationVector = CalculateSeparationVector();

            // 3. 두 벡터를 합성하고 정규화하여 최종 이동 벡터 산출
            Vector2 finalDirection = (directionToTarget + (separationVector * separationWeight)).normalized;

            rb.linearVelocity = finalDirection * moveSpeed;
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