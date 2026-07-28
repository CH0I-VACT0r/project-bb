using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAINetcode : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 1.0f;
    public float searchInterval = 0.5f; // 플레이어 탐색 주기

    private Rigidbody2D rb;
    private Transform targetPlayer;
    private float searchTimer = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // 이동 및 타겟 탐색 연산은 오직 서버에서만 수행
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
            // 타겟을 향한 방향 벡터 계산 및 물리적 이동 적용
            Vector2 direction = (targetPlayer.position - transform.position).normalized;
            rb.linearVelocity = direction * moveSpeed;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    // 가장 가까운 플레이어를 탐색하는 로직
    private void FindNearestPlayer()
    {
        // 최적화를 위해 매 프레임이 아닌 searchInterval(0.5초) 주기로 실행
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
}