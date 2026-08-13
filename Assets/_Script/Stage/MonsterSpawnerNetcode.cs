using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MonsterSpawnerNetcode : NetworkBehaviour
{
    public static MonsterSpawnerNetcode Instance { get; private set; }

    [Header("Spawn Bounds Settings")]
    public float spawnCheckRadius = 0.5f;
    public int maxSpawnAttempts = 10;

    private Bounds mapBounds;
    private bool hasBounds = false;

    private int totalSpawnedCount = 0;
    private int totalDeadCount = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    public override void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        base.OnDestroy();
    }

    public void SetSpawnBounds(Bounds bounds)
    {
        mapBounds = bounds;
        hasBounds = true;
    }

    public void SetSpawnBounds(Vector2 min, Vector2 max)
    {
        Vector3 center = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f);
        Vector3 size = new Vector3(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y), 10f);
        mapBounds = new Bounds(center, size);
        hasBounds = true;
    }

    public void InitializeSpawner(StageDataSO stageData)
    {
        bool isServerAuthority = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        Debug.Log($"[MonsterSpawner] InitializeSpawner 호출됨! (서버 권한: {isServerAuthority}, 데이터 존재: {stageData != null})");

        if (!isServerAuthority || stageData == null) return;

        int currentFloor = GameManager.Instance.currentFloor;
        FloorSpawnData floorData = stageData.GetFloorData(currentFloor);

        if (floorData.waves == null || floorData.waves.Length == 0)
        {
            Debug.LogWarning($"[스포너] {currentFloor}층에 설정된 웨이브 데이터가 없습니다! 즉시 클리어 처리합니다.");
            CombatStageManager.Instance.StageCleared();
            return;
        }

        totalDeadCount = 0;
        totalSpawnedCount = 0;

        foreach (var wave in floorData.waves)
        {
            if (wave.spawnGroups != null)
            {
                foreach (var group in wave.spawnGroups)
                {
                    totalSpawnedCount += group.count;
                }
            }
        }

        if (totalSpawnedCount == 0)
        {
            Debug.LogWarning($"[스포너] {currentFloor}층의 웨이브는 존재하지만, 'Spawn Groups'의 Count가 0이거나 프리팹이 할당되지 않았습니다! StageDataSO 인스펙터를 확인하십시오.");
            CombatStageManager.Instance.StageCleared();
            return;
        }

        StartCoroutine(SpawnWaveRoutine(floorData));
    }

    private IEnumerator SpawnWaveRoutine(FloorSpawnData floorData)
    {
        for (int w = 0; w < floorData.waves.Length; w++)
        {
            WaveSpawnData currentWave = floorData.waves[w];

            if (currentWave.spawnGroups != null)
            {
                // 각 그룹에 정의된 프리팹과 개수만큼 순서대로 스폰
                foreach (var group in currentWave.spawnGroups)
                {
                    for (int i = 0; i < group.count; i++)
                    {
                        if (group.monsterPrefab != null)
                        {
                            Vector3 spawnPos = GetValidSpawnPosition();
                            SpawnMonster(group.monsterPrefab, spawnPos);
                        }
                        yield return new WaitForSeconds(0.3f);
                    }
                }
            }

            if (w < floorData.waves.Length - 1)
            {
                yield return new WaitForSeconds(currentWave.timeToNextWave);
            }
        }
    }

    private void SpawnMonster(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return;
        EnemyStatManager statCheck = prefab.GetComponent<EnemyStatManager>();
        if (statCheck == null)
        {
            Debug.LogError($"[MonsterSpawner] 잘못된 프리팹 스폰 시도! '{prefab.name}' 오브젝트에는 EnemyStatManager가 없습니다. StageDataSO 인스펙터에 투사체나 잘못된 프리팹이 연결되었는지 확인하십시오.");
            return;
        }

        GameObject mob = Instantiate(prefab, position, Quaternion.identity);

        // Spawn() must be called BEFORE ApplyScaling,
        // because ApplyScaling checks IsServer which is only true after Spawn().
        mob.GetComponent<NetworkObject>().Spawn();

        EnemyStatManager stat = mob.GetComponent<EnemyStatManager>();
        if (stat != null)
        {
            int floor = GameManager.Instance.currentFloor;
            int playerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
            stat.ApplyScaling(floor, Mathf.Max(1, playerCount));
        }
    }

    private Vector3 GetValidSpawnPosition()
    {
        if (!hasBounds)
            return new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0);

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            float rx = Random.Range(mapBounds.min.x, mapBounds.max.x);
            float ry = Random.Range(mapBounds.min.y, mapBounds.max.y);
            Vector2 targetPos = new Vector2(rx, ry);

            if (Physics2D.OverlapCircle(targetPos, spawnCheckRadius) == null)
            {
                return targetPos;
            }
        }
        return new Vector3(Random.Range(mapBounds.min.x, mapBounds.max.x), Random.Range(mapBounds.min.y, mapBounds.max.y), 0);
    }

    public void OnMonsterDead()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        totalDeadCount++;
        Debug.Log($"[서버] 몬스터 처치: ({totalDeadCount} / {totalSpawnedCount})");

        if (totalDeadCount >= totalSpawnedCount && totalSpawnedCount > 0)
        {
            Debug.Log("[서버] 방 클리어! 다음 층 포탈을 생성합니다.");
            CombatStageManager.Instance.StageCleared();
        }
    }
}