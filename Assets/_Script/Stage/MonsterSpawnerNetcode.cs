using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MonsterSpawnerNetcode : NetworkBehaviour
{
    public static MonsterSpawnerNetcode Instance { get; private set; }

    [Header("Spawn Settings")]
    public float spawnRadius = 15f; // 맵 중앙(또는 플레이어)으로부터 몬스터가 생성될 반경

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void InitializeSpawner(StageDataSO data)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // 데이터가 비정상적으로 들어왔을 경우를 대비한 안전 코드 추가
        if (data == null)
        {
            Debug.LogError("스포너에 전달된 StageDataSO가 비어있습니다!");
            return;
        }

        StartCoroutine(WaveRoutine(data));
    }

    private IEnumerator WaveRoutine(StageDataSO data)
    {
        // 웨이브 시작 전 초기 대기 시간 (로딩 및 플레이어 준비 시간)
        yield return new WaitForSeconds(3f);

        for (int wave = 1; wave <= data.waveCount; wave++)
        {
            Debug.Log($"[서버] Wave {wave} 시작!");

            // 웨이브당 생성할 몬스터 수 계산 (예: 웨이브 단계 * 5마리)
            // 추후 StageDataSO에 배열을 추가해 웨이브마다 마릿수를 디테일하게 조절할 수 있습니다.
            int enemiesToSpawn = wave * 5;

            for (int i = 0; i < enemiesToSpawn; i++)
            {
                SpawnRandomEnemy(data);
            }

            // 다음 웨이브가 시작될 때까지 설정된 시간만큼 대기
            yield return new WaitForSeconds(data.timeBetweenWaves);
        }

        // 모든 일반 웨이브 종료 후 보스 소환
        Debug.Log("[서버] 보스 출현!");
        SpawnBoss(data);
    }

    private void SpawnRandomEnemy(StageDataSO data)
    {
        if (data.normalEnemyPrefabs == null || data.normalEnemyPrefabs.Length == 0) return;

        int randomIndex = Random.Range(0, data.normalEnemyPrefabs.Length);
        GameObject prefabToSpawn = data.normalEnemyPrefabs[randomIndex];

        ExecuteSpawn(prefabToSpawn);
    }

    private void SpawnBoss(StageDataSO data)
    {
        if (data.bossPrefab == null) return;
        ExecuteSpawn(data.bossPrefab);
    }

    private void ExecuteSpawn(GameObject prefab)
    {
        // 생성 위치 계산: 원점(0,0)을 기준으로 지정된 반경(Radius) 바깥의 임의의 위치
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        Vector3 spawnPos = new Vector3(randomDir.x, randomDir.y, 0) * spawnRadius;

        GameObject enemyInstance = Instantiate(prefab, spawnPos, Quaternion.identity);
        NetworkObject netObj = enemyInstance.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            // true를 전달하면 씬이 전환되거나 종료될 때 이 몬스터도 자동으로 파괴(Despawn)됩니다.
            netObj.Spawn(true);
        }
        else
        {
            Debug.LogError($"{prefab.name} 프리팹에 NetworkObject 컴포넌트가 없습니다!");
        }
    }
}