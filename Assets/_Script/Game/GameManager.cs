using UnityEngine;

public enum StageRoomType { Combat, Elite, Heal, Shop, Boss }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Stage Progress")]
    public int currentStageId = 1; // 현재 진입한 전투 스테이지 번호
    public int highestUnlockedStage { get; private set; } = 1; // 최고 해금 스테이지

    [Header("Run Progress")]
    public int currentFloor = 1; // 1-1 이면 1, 1-2 면 2...
    public StageRoomType nextRoomType = StageRoomType.Combat;

    private void Awake()
    {
        // 싱글톤(Singleton) 패턴 및 씬 전환 시 파괴 방지
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadUnlockData(); // 시작할 때 세이브 파일 불러오기
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadUnlockData()
    {
        // 로컬 기기에 저장된 "HighestUnlockedStage" 값을 불러옴 (기록이 없으면 기본값 1)
        highestUnlockedStage = PlayerPrefs.GetInt("HighestUnlockedStage", 1);
    }

    // 전투 스테이지 보스를 클리어했을 때 호출할 함수
    public void UnlockNextStage(int stageToUnlock)
    {
        if (stageToUnlock > highestUnlockedStage)
        {
            highestUnlockedStage = stageToUnlock;
            PlayerPrefs.SetInt("HighestUnlockedStage", highestUnlockedStage);
            PlayerPrefs.Save(); // 로컬 기기에 저장
            Debug.Log($"스테이지 {highestUnlockedStage} 해금 완료!");
        }
    }

    // 보스 클리어 시 호출되어 강화 던전을 해금하는 함수
    public void UnlockEnhancedStages(int stageId)
    {
        PlayerPrefs.SetInt($"Unlocked_EnhancedElite_{stageId}", 1);
        PlayerPrefs.SetInt($"Unlocked_EnhancedBoss_{stageId}", 1);
        PlayerPrefs.Save();

        Debug.Log($"스테이지 {stageId}의 강화된 엘리트 및 보스 던전이 해금되었습니다!");
    }

    //  UI 등에서 해당 던전이 열려있는지 확인할 때 쓰는 함수
    public bool IsEnhancedEliteUnlocked(int stageId)
    {
        return PlayerPrefs.GetInt($"Unlocked_EnhancedElite_{stageId}", 0) == 1;
    }

    public bool IsEnhancedBossUnlocked(int stageId)
    {
        return PlayerPrefs.GetInt($"Unlocked_EnhancedBoss_{stageId}", 0) == 1;
    }

    public void ResetDungeonProgress()
    {
        currentFloor = 0; // 포탈 입장 시 currentFloor++ 가 호출되어 1층이 됨
        nextRoomType = StageRoomType.Combat;
        Debug.Log("[GameManager] 던전 진행도가 초기화되었습니다. (다음 입장 시 1층 Combat 방)");
    }
}