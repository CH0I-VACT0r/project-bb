using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(StatusEffectManagerNetcode))]
public class EnemyAggroController : NetworkBehaviour
{
    private StatusEffectManagerNetcode statusManager;

    // 외부(근접/원거리 컨트롤러)에서 읽어갈 현재 타겟
    public Transform CurrentTarget { get; private set; }

    [Tooltip("타겟 갱신 주기 (서버 부하 방지용)")]
    public float targetRefreshRate = 0.5f;
    private float lastRefreshTime;

    void Awake()
    {
        statusManager = GetComponent<StatusEffectManagerNetcode>();
    }

    void Update()
    {
        if (!IsServer) return;

        // 0.5초마다 타겟 갱신
        if (Time.time - lastRefreshTime >= targetRefreshRate)
        {
            UpdateTarget();
            lastRefreshTime = Time.time;
        }
    }

    private void UpdateTarget()
    {
        // 1. 도발(Taunt) 상태 최우선 처리 로직
        if (statusManager.isTaunted.Value && statusManager.effectSourceId.Value != 0)
        {
            ulong taunterId = statusManager.effectSourceId.Value;
            // 네트워크 매니저에서 해당 ID를 가진 오브젝트 검색
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(taunterId, out NetworkObject taunterObj))
            {
                CurrentTarget = taunterObj.transform;
                return; // 도발에 걸렸다면 아래의 '가장 가까운 플레이어 탐색'을 무시하고 즉시 종료
            }
        }

        // 2. 도발 상태가 아니라면, 가장 가까운 플레이어 탐색
        CurrentTarget = FindClosestPlayer();
    }

    private Transform FindClosestPlayer()
    {
        Transform closest = null;
        float minDistance = float.MaxValue;

        // 접속 중인 모든 클라이언트(플레이어) 순회
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                float dist = Vector2.Distance(transform.position, client.PlayerObject.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = client.PlayerObject.transform;
                }
            }
        }

        return closest;
    }
}