using Unity.Netcode;
using UnityEngine;

public class Pattern_EdgeFlyby : BossPatternBase
{
    [Header("Flyby Settings")]
    public float flySpeed = 10f;
    public float damage = 15f;
    public float lifeTime = 5f; // 맵을 뚫고 나가면 자동 삭제할 타이머
    public float spawnRadius = 15f;

    private Vector3 flyDirection;
    private bool isFlying = false;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        PlayerMovementNetcode[] players = FindObjectsByType<PlayerMovementNetcode>(FindObjectsSortMode.None);
        if (players.Length > 0)
        {
            Transform playerTarget = players[0].transform;

            // 플레이어 기준 무작위 외곽(Edge) 좌표로 스스로를 순간이동
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            transform.position = playerTarget.position + (Vector3)(randomDir * spawnRadius);

            // 적 AI 무력화 및 타겟 지정 후 패턴 즉시 시작
            EnemyAINetcode ai = GetComponent<EnemyAINetcode>();
            if (ai != null) ai.canAutoAttack = false;

            ExecutePattern(playerTarget, null);
        }
        else
        {
            EndFlyby();
        }
    }

    protected override void OnPatternStart()
    {
        if (!IsServer) return;
        
        flyDirection = (currentTarget.position - transform.position).normalized; // 타겟(플레이어)을 향한 일직선 방향을 계산 후 영구 
        isFlying = true; // 투사체처럼 일직선 이동 
        Invoke(nameof(EndFlyby), lifeTime); // 일정 시간 뒤 자동 파괴 또는 패턴 종료
    }

    private void Update()
    {
        if (!IsServer || !isFlying) return;

        // 내비게이션을 무시하고 강제 직선 이동
        transform.position += flyDirection * flySpeed * Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (!IsServer) return;
        if (col.CompareTag("Player"))
        {
            DamageInfo info = new DamageInfo { damageAmount = this.damage, attackType = AttackAttribute.Physical };
            col.GetComponent<IDamageable>()?.TakeDamage(info);
        }
    }

    private void EndFlyby()
    {
        isFlying = false;

        if (EnemyPoolManager.Instance != null)
        {
            // 네트워크 동기화 해제 (false = 오브젝트를 파괴하지 않고 씬에 남겨둠)
            NetworkObject netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(false);
            }

            // 풀 매니저를 통해 오브젝트 반환
            EnemyPoolManager.Instance.ReturnEnemy(this.gameObject);
        }
        else
        {
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
}