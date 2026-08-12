using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class CombatStageManager : NetworkBehaviour
{
    public static CombatStageManager Instance { get; private set; }

    [Header("Chapter Database")]
    public StageDataSO[] allChapters;

    [Header("Current Runtime Chapter")]
    public StageDataSO currentChapterData;

    [Header("Portal System")]
    public GameObject portalPrefab;

    private bool isTransitioning = false;
    private Coroutine autoTransitionCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            int selectedStageId = GameManager.Instance.currentStageId;
            currentChapterData = GetChapterDataById(selectedStageId);

            StageRoomType currentRoomType = GameManager.Instance.nextRoomType;
            int currentFloor = GameManager.Instance.currentFloor;

            Debug.Log($"[{currentFloor}층] {currentRoomType} 방 세팅을 시작합니다. (선택된 챕터 ID: {selectedStageId})");
            SetupRoomEnvironment(currentRoomType, currentFloor);
        }
    }

    private StageDataSO GetChapterDataById(int stageId)
    {
        if (allChapters != null)
        {
            foreach (var chapter in allChapters)
            {
                if (chapter.chapterId == stageId)
                {
                    return chapter;
                }
            }
        }

        Debug.LogWarning($"[CombatStageManager] ID {stageId}에 해당하는 챕터 데이터를 찾지 못했습니다! 첫 번째 데이터를 기본값으로 사용합니다.");
        return (allChapters != null && allChapters.Length > 0) ? allChapters[0] : null;
    }

    private void SetupRoomEnvironment(StageRoomType roomType, int floor)
    {
        if (currentChapterData == null)
        {
            Debug.LogError("StageDataSO가 CombatStageManager에 할당되지 않았습니다!");
            return;
        }

        GameObject mapPrefabToUse = null;
        bool requiresMonsterSpawner = false;

        // 1. 방 성격에 따라 DB에서 맵 프리팹 추출
        switch (roomType)
        {
            case StageRoomType.Combat:
                mapPrefabToUse = currentChapterData.defaultCombatMap;
                requiresMonsterSpawner = true;
                break;
            case StageRoomType.Elite:
                mapPrefabToUse = currentChapterData.eliteCombatMap;
                requiresMonsterSpawner = true;
                break;
            case StageRoomType.Boss:
                mapPrefabToUse = currentChapterData.bossCombatMap;
                requiresMonsterSpawner = true;
                break;
            case StageRoomType.Heal:
                mapPrefabToUse = currentChapterData.healRoomMap;
                requiresMonsterSpawner = false;
                break;
            case StageRoomType.Shop:
                mapPrefabToUse = currentChapterData.shopRoomMap;
                requiresMonsterSpawner = false;
                break;
        }

        // 2. 맵 네트워크 스폰
        if (mapPrefabToUse != null)
        {
            GameObject mapInstance = Instantiate(mapPrefabToUse, Vector3.zero, Quaternion.identity);
            NetworkObject netObj = mapInstance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn(true);
            }
        }

        // 3. 전투 방일 경우 스포너 호출
        if (requiresMonsterSpawner)
        {
            MonsterSpawnerNetcode spawner = MonsterSpawnerNetcode.Instance;
            if (spawner == null)
            {
                spawner = FindFirstObjectByType<MonsterSpawnerNetcode>();
            }

            if (spawner != null)
            {
                spawner.InitializeSpawner(currentChapterData);
            }
            else
            {
                Debug.LogError("[CombatStageManager] 씬에서 MonsterSpawnerNetcode를 찾을 수 없습니다! CombatScene Hierarchy에 스포너 오브젝트가 배치되어 있는지 확인하십시오.");
            }
        }
    }

    public void StageCleared()
    {
        if (!IsServer) return;

        if (GameManager.Instance.currentFloor == 20)
        {
            int currentStage = GameManager.Instance.currentStageId;
            GameManager.Instance.UnlockNextStage(currentStage + 1);
            GameManager.Instance.UnlockEnhancedStages(currentStage);
        }

        SpawnNextStagePortals();

        if (autoTransitionCoroutine != null) StopCoroutine(autoTransitionCoroutine);
        autoTransitionCoroutine = StartCoroutine(AutoTransitionRoutine());
    }

    private IEnumerator AutoTransitionRoutine()
    {
        Debug.Log("[서버] 방 클리어! 10초 뒤 다음 층으로 자동 이동합니다.");
        yield return new WaitForSeconds(10f);

        if (isTransitioning) yield break;

        int nextFloor = GameManager.Instance.currentFloor + 1;
        StageRoomType selectedRoomType;

        // 분기점(Heal/Shop)일 경우 50% 확률로 랜덤 방 선택
        if (nextFloor % 5 == 4)
        {
            selectedRoomType = (Random.Range(0, 2) == 0) ? StageRoomType.Heal : StageRoomType.Shop;
            Debug.Log($"[서버] 10초 대기 초과! Heal/Shop 분기에서 무작위 선택됨 -> {selectedRoomType}");
        }
        else if (nextFloor % 5 == 0)
        {
            selectedRoomType = (nextFloor == 20) ? StageRoomType.Boss : StageRoomType.Elite;
        }
        else
        {
            selectedRoomType = StageRoomType.Combat;
        }

        TransitionToNextStage(selectedRoomType);
    }

    // 수동 포탈 클릭과 자동 이동 타이머가 공통으로 사용하는 단일 이동 함수
    public void TransitionToNextStage(StageRoomType roomType)
    {
        if (!IsServer || isTransitioning) return;
        isTransitioning = true;

        if (autoTransitionCoroutine != null)
        {
            StopCoroutine(autoTransitionCoroutine);
            autoTransitionCoroutine = null;
        }

        StartCoroutine(TransitionWithFadeRoutine(roomType));
    }

    private IEnumerator TransitionWithFadeRoutine(StageRoomType roomType)
    {
        
        TriggerFadeOutClientRpc(); // 접속 중인 모든 플레이어에게 화면을 검게 칠하라고 RPC 명령

        yield return new WaitForSeconds(0.3f);
        GameManager.Instance.currentFloor++;
        GameManager.Instance.nextRoomType = roomType;
        NetworkManager.Singleton.SceneManager.LoadScene("CombatScene", LoadSceneMode.Single);
    }

    [ClientRpc]
    private void TriggerFadeOutClientRpc()
    {
        if (SceneTransitionCurtain.Instance != null)
        {
            SceneTransitionCurtain.Instance.FadeOutAndCall(null);
        }
    }

    private void SpawnNextStagePortals()
    {
        int nextFloor = GameManager.Instance.currentFloor + 1;
        if (nextFloor > 20) return;

        if (nextFloor % 5 == 4)
        {
            SpawnPortal(new Vector3(-3f, 0, 0), StageRoomType.Heal);
            SpawnPortal(new Vector3(3f, 0, 0), StageRoomType.Shop);
        }
        else if (nextFloor % 5 == 0)
        {
            if (nextFloor == 20)
                SpawnPortal(Vector3.zero, StageRoomType.Boss);
            else
                SpawnPortal(Vector3.zero, StageRoomType.Elite);
        }
        else
        {
            SpawnPortal(Vector3.zero, StageRoomType.Combat);
        }
    }

    private void SpawnPortal(Vector3 position, StageRoomType type)
    {
        GameObject portalInstance = Instantiate(portalPrefab, position, Quaternion.identity);
        StagePortalNetcode portalScript = portalInstance.GetComponent<StagePortalNetcode>();
        NetworkObject netObj = portalInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
        }
        if (portalScript != null)
        {
            portalScript.roomType.Value = type;
        }
       
    }
}