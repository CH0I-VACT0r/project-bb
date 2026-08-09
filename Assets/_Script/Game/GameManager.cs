using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Stage Progress")]
    public int currentStageId = 1; // 현재 진입한 전투 스테이지 번호
    public int highestUnlockedStage { get; private set; } = 1; // 최고 해금 스테이지

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
}