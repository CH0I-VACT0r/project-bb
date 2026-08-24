using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BossPattern_SummonAdds : BossPatternBase
{
    [Header("Summon Settings")]
    [Tooltip("보스가 소환할 전용 잡몹 프리팹 (NetworkObject 필수)")]
    public GameObject minionPrefab;

    public int summonCount = 10;
    public float spawnRadius = 3f;
    [Tooltip("애니메이션 시작 후 실제 잡몹이 튀어나올 때까지의 대기 시간")]
    public float spawnDelay = 0.2f;

    protected override void OnPatternStart()
    {
        if (!IsServer) return;
        StartCoroutine(SummonRoutine());
    }

    private IEnumerator SummonRoutine()
    {
        // 1. 소환 애니메이션 실행
        if (!string.IsNullOrEmpty(animatorTriggerName))
        {
            PlayPatternAnimationClientRpc(animatorTriggerName);
        }

        // 2. 애니메이션 박자에 맞게 대기
        yield return new WaitForSeconds(spawnDelay);

        // 3. 지정된 프리팹으로 몬스터 스폰
        if (minionPrefab != null)
        {
            for (int i = 0; i < summonCount; i++)
            {
                // 보스 주변의 무작위 원형 좌표 산출
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                Vector2 spawnPos = (Vector2)transform.position + (randomDir * spawnRadius);

                // 풀 매니저를 거치지 않고 보스 전용 하수인을 직접 생성
                GameObject minion = Instantiate(minionPrefab, spawnPos, Quaternion.identity);

                NetworkObject netObj = minion.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                }

                // 소환된 하수인에게도 현재 층수에 맞는 스탯 스케일링 적용
                EnemyStatManager stat = minion.GetComponent<EnemyStatManager>();
                if (stat != null && GameManager.Instance != null)
                {
                    stat.isSummonedMinion = true;
                    int floor = GameManager.Instance.currentFloor;
                    int playerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
                    stat.ApplyScaling(floor, Mathf.Max(1, playerCount));
                }
            }
        }
        else
        {
            Debug.LogWarning("[BossPattern_SummonAdds] 인스펙터에 minionPrefab이 할당되지 않았습니다!");
        }

        FinishPattern();
    }
}