using Unity.Netcode;
using UnityEngine;

public class CombatStageManager : NetworkBehaviour
{
    public static CombatStageManager Instance { get; private set; }

    [Header("Stage Database")]
    public StageDataSO[] allStages; // 스테이지 데이터들

    [Header("Room Environment Prefabs")]
    public GameObject defaultCombatMap; // 일반/엘리트/보스전 맵 프리팹
    public GameObject healRoomMap;      // 회복 방 맵 프리팹 (추후 할당)
    public GameObject shopRoomMap;      // 상점 방 맵 프리팹 (추후 할당)

    [Header("Portal System")]
    public GameObject portalPrefab;

    public StageDataSO currentStageData { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 방장이 선택해서 들어온 방의 성격과 현재 층수를 읽어옵니다.
            StageRoomType currentRoomType = GameManager.Instance.nextRoomType;
            int currentFloor = GameManager.Instance.currentFloor;

            Debug.Log($"[{currentFloor}층] {currentRoomType} 방 세팅을 시작합니다.");

            SetupRoomEnvironment(currentRoomType, currentFloor);
        }
    }

    private void SetupRoomEnvironment(StageRoomType roomType, int floor)
    {
        GameObject mapPrefabToUse = null;
        bool requiresMonsterSpawner = false;

        // 1. 방 성격에 따라 맵 프리팹과 스포너 작동 여부를 결정합니다.
        switch (roomType)
        {
            case StageRoomType.Combat:
            case StageRoomType.Elite:
            case StageRoomType.Boss:
                mapPrefabToUse = defaultCombatMap;
                requiresMonsterSpawner = true;
                break;
            case StageRoomType.Heal:
                mapPrefabToUse = healRoomMap;
                requiresMonsterSpawner = false; // 평화로운 방이므로 스폰 금지
                break;
            case StageRoomType.Shop:
                mapPrefabToUse = shopRoomMap;
                requiresMonsterSpawner = false;
                break;
        }

        // 2. 맵 생성 및 네트워크 동기화
        if (mapPrefabToUse != null)
        {
            GameObject mapInstance = Instantiate(mapPrefabToUse, Vector3.zero, Quaternion.identity);
            NetworkObject netObj = mapInstance.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Spawn();
        }

        // 3. 전투 방일 경우에만 몬스터 스포너 가동
        if (requiresMonsterSpawner)
        {
            // 임시로 floor(층수)를 ID로 사용하여 스테이지 데이터를 찾습니다. 
            // 추후 '엘리트 전용 데이터', '보스 전용 데이터'를 따로 맵핑하도록 기획을 확장할 수 있습니다.
            currentStageData = GetStageDataById(floor);

            if (currentStageData != null)
            {
                MonsterSpawnerNetcode spawner = FindFirstObjectByType<MonsterSpawnerNetcode>();
                if (spawner != null)
                {
                    spawner.InitializeSpawner(currentStageData);
                }
            }
            else
            {
                Debug.LogWarning($"{floor}층에 해당하는 StageDataSO가 존재하지 않습니다!");
                // 데이터가 없을 경우 테스트를 위해 스폰을 넘기고 바로 클리어 처리를 띄울 수도 있습니다.
                SpawnNextStagePortals();
            }
        }
        else
        {
            Debug.Log("비전투 방입니다. 몬스터가 등장하지 않습니다.");
            // TODO: 상점 NPC나 회복 샘물 등을 스폰하는 로직이 여기에 추가됩니다.
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

    public void StageCleared()
    {
        if (!IsServer) return;

        if (GameManager.Instance.currentFloor == 20)
        {
            int currentStage = GameManager.Instance.currentStageId;
            GameManager.Instance.UnlockNextStage(currentStage + 1); // 다음 챕터 해금
            GameManager.Instance.UnlockEnhancedStages(currentStage); // 현재 챕터의 강화 던전 해금
        }

        SpawnNextStagePortals();
    }

    private void SpawnNextStagePortals()
    {
        int nextFloor = GameManager.Instance.currentFloor + 1;

        if (nextFloor == 20)
        {
            SpawnPortal(Vector3.zero, StageRoomType.Boss);
        }
        else if (nextFloor % 5 == 0)
        {
            SpawnPortal(Vector3.zero, StageRoomType.Elite);
        }
        else
        {
            StageRoomType typeA = (StageRoomType)Random.Range(0, 4);
            StageRoomType typeB = (StageRoomType)Random.Range(0, 4);

            while (typeA == typeB) { typeB = (StageRoomType)Random.Range(0, 4); }

            SpawnPortal(new Vector3(-3f, 0, 0), typeA);
            SpawnPortal(new Vector3(3f, 0, 0), typeB);
        }
    }

    private void SpawnPortal(Vector3 position, StageRoomType type)
    {
        GameObject portalInstance = Instantiate(portalPrefab, position, Quaternion.identity);

        StagePortalNetcode portalScript = portalInstance.GetComponent<StagePortalNetcode>();
        if (portalScript != null)
        {
            portalScript.roomType.Value = type;
        }

        portalInstance.GetComponent<NetworkObject>().Spawn();
    }
}