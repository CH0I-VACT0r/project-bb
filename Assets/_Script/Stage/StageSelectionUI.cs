using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement; // LoadSceneMode 사용을 위해 필요

public class StageSelectionUI : MonoBehaviour
{
    public void OnClickEnterStage(int stageId)
    {
        // 1. 방장(Host) 권한 검사
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("방장(Host)만 스테이지를 시작할 수 있습니다.");
            return;
        }

        // 2. 스테이지 해금 여부 검사
        if (stageId > GameManager.Instance.highestUnlockedStage)
        {
            Debug.LogWarning($"스테이지 {stageId}는 아직 잠겨 있습니다! (현재 해금: {GameManager.Instance.highestUnlockedStage})");
            return;
        }

        GameManager.Instance.currentStageId = stageId;
        GameManager.Instance.ResetDungeonProgress();
        GameManager.Instance.currentFloor = 1; // 1층부터 시작하도록 명시적 고정
        GameManager.Instance.nextRoomType = StageRoomType.Combat; // 첫 방은 무조건 일반 전투 방

        // 5. 전투 씬으로 이동
        NetworkManager.Singleton.SceneManager.LoadScene("CombatScene", LoadSceneMode.Single);
    }
}