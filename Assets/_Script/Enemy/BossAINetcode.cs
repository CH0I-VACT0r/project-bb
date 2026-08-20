using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(EnemyStatManager))]
public class BossAINetcode : NetworkBehaviour
{
    private EnemyStatManager statManager;
    private StatusEffectManagerNetcode statusManager;
    private EnemyVisualNetcode visualNetcode;
    private Rigidbody2D rb;

    [Header("Boss Target & State")]
    public float searchInterval = 0.5f;
    private float searchTimer = 0f;
    private Transform targetPlayer;
    public Transform TargetPlayer => targetPlayer;

    private bool isDead = false;

    [Header("Boss Pattern Manager")]
    [Tooltip("보스가 사용할 모든 패턴 컴포넌트를 여기에 드래그해서 넣으세요.")]
    public List<BossPatternBase> availablePatterns;

    private bool isExecutingPattern = false;
    private float patternCooldownTimer = 3f; // 스폰 직후 최초 패턴 시전 대기 시간

    // 시각 스크립트에서 좌우 반전을 막기 위한 상태 프로퍼티
    public bool IsDirectionLocked => isDead || isExecutingPattern;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        statManager = GetComponent<EnemyStatManager>();
        statusManager = GetComponent<StatusEffectManagerNetcode>();
        visualNetcode = GetComponent<EnemyVisualNetcode>();
    }

    private void Update()
    {
        if (!IsServer || isDead) return;

        // 타겟 탐색
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            FindNearestPlayer();
            searchTimer = searchInterval;
        }

        // 패턴 실행 중이거나, 타겟이 없거나, 기절 상태면 쿨타임 연산 중단
        if (isExecutingPattern || targetPlayer == null || (statusManager != null && statusManager.isStunned.Value))
        {
            return;
        }

        // 쿨타임 감소 및 다음 패턴 발동 대기
        patternCooldownTimer -= Time.deltaTime;
        if (patternCooldownTimer <= 0f)
        {
            ExecuteRandomPattern();
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer || isDead || isExecutingPattern || (statusManager != null && statusManager.isStunned.Value))
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 보스가 쿨타임(대기 시간) 동안 타겟을 향해 천천히 다가가는 로직
        if (targetPlayer != null && statManager != null)
        {
            float dist = Vector2.Distance(transform.position, targetPlayer.position);
            // 패턴 범위 확보
            if (dist > 2f)
            {
                Vector2 direction = ((Vector2)targetPlayer.position - (Vector2)transform.position).normalized;
                float currentSpeed = statManager.enemyData.moveSpeed * (statusManager != null ? statusManager.moveSpeedMultiplier.Value : 1f);
                rb.linearVelocity = direction * currentSpeed;
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    private void ExecuteRandomPattern()
    {
        if (availablePatterns == null || availablePatterns.Count == 0) return;

        // 패턴 실행 상태 잠금
        isExecutingPattern = true;
        rb.linearVelocity = Vector2.zero;

        // 가중치 기반 무작위 패턴
        BossPatternBase selectedPattern = GetRandomPatternByWeight();

        // 선택된 패턴 실행 및 종료
        selectedPattern.ExecutePattern(targetPlayer, () =>
        {
            // 패턴이 완전히 끝났을 때 상태 잠금 해제 및 개별 쿨타임 적용
            isExecutingPattern = false;
            patternCooldownTimer = selectedPattern.patternCooldown;
        });
    }

    private BossPatternBase GetRandomPatternByWeight()
    {
        float totalWeight = 0f;
        foreach (var pattern in availablePatterns)
        {
            totalWeight += pattern.patternWeight;
        }

        float randomVal = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var pattern in availablePatterns)
        {
            currentWeight += pattern.patternWeight;
            if (randomVal <= currentWeight)
            {
                return pattern;
            }
        }
        return availablePatterns[0];
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

    // 외부에서 사망 시 호출
    public void HandleDeath()
    {
        if (!IsServer || isDead) return;
        isDead = true;
        rb.linearVelocity = Vector2.zero;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        visualNetcode.TriggerDeathAnimation();
        // 이후 풀링 반환 등 데스 루틴 실행 (기존과 동일)
    }
}