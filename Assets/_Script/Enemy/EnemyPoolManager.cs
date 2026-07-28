// EnemyPoolManager.cs
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

public class EnemyPoolManager : NetworkBehaviour
{
    public static EnemyPoolManager Instance { get; private set; }

    [Header("Pool Settings")]
    public GameObject enemyPrefab; // 적 프리팹 (NetworkObject 포함 필수)
    public int defaultCapacity = 50;
    public int maxCapacity = 200;

    private ObjectPool<GameObject> enemyPool;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        // 풀링 시스템은 서버에서만 구동됩니다.
        if (IsServer)
        {
            enemyPool = new ObjectPool<GameObject>(
                createFunc: CreateEnemy,
                actionOnGet: OnTakeEnemyFromPool,
                actionOnRelease: OnReturnEnemyToPool,
                actionOnDestroy: DestroyEnemy,
                collectionCheck: false,
                defaultCapacity: defaultCapacity,
                maxSize: maxCapacity
            );
        }
    }

    private GameObject CreateEnemy()
    {
        GameObject enemy = Instantiate(enemyPrefab, this.transform);
        return enemy;
    }

    private void OnTakeEnemyFromPool(GameObject enemy)
    {
        enemy.SetActive(true);
    }

    private void OnReturnEnemyToPool(GameObject enemy)
    {
        enemy.SetActive(false);
    }

    private void DestroyEnemy(GameObject enemy)
    {
        Destroy(enemy);
    }

    // 외부(스포너)에서 적을 소환할 때 호출하는 함수
    public void SpawnEnemy(Vector3 spawnPosition)
    {
        if (!IsServer) return;

        GameObject enemy = enemyPool.Get();
        enemy.transform.position = spawnPosition;

        // 클라이언트들에게 스폰 동기화
        enemy.GetComponent<NetworkObject>().Spawn(true);
    }

    // 적이 죽었을 때 풀로 반환하는 함수
    public void ReturnEnemy(GameObject enemy)
    {
        if (!IsServer) return;
        enemyPool.Release(enemy);
    }
}
