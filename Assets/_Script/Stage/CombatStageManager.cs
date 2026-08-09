using Unity.Netcode;
using UnityEngine;

public class CombatStageManager : NetworkBehaviour
{
    public static CombatStageManager Instance { get; private set; }

    [Header("Stage Database")]
    public StageDataSO[] allStages; // 프로젝트에 있는 모든 스테이지 SO를 인스펙터에 등록

    public StageDataSO currentStageData { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        // 맵 생성과 스포너 세팅은 오직 방장(서버)만 수행합니다.
        if (IsServer)
        {
            int targetStageId = GameManager.Instance.currentStageId;
            currentStageData = GetStageDataById(targetStageId);

            if (currentStageData != null)
            {
                SetupStageEnvironment(currentStageData);
            }
            else
            {
                Debug.LogError($"스테이지 {targetStageId}의 데이터를 찾을 수 없습니다!");
            }
        }
    }

    private StageDataSO GetStageDataById(int id)
    {
        foreach (var stage in allStages)
        {
            if (stage.stageId == id) return stage;
        }
        return null;
    }

    private void SetupStageEnvironment(StageDataSO data)
    {
        // 1. 맵 프리팹 생성
        if (data.mapPrefab != null)
        {
            GameObject mapInstance = Instantiate(data.mapPrefab, Vector3.zero, Quaternion.identity);
            NetworkObject netObj = mapInstance.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Spawn();
        }

        // 2. 몬스터 스포너 실행 로직 변경 (직접 찾아서 실행)
        MonsterSpawnerNetcode spawner = FindFirstObjectByType<MonsterSpawnerNetcode>();
        if (spawner != null)
        {
            spawner.InitializeSpawner(data);
        }
        else
        {
            Debug.LogError("전투 씬에 MonsterSpawnerNetcode 컴포넌트가 없습니다! 생성해주세요.");
        }

        Debug.Log($"스테이지 {data.stageId} 맵 생성 및 초기화 완료");
    }
}