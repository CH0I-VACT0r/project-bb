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

        // 3. 선택한 스테이지 번호를 GameManager에 저장 (전투 씬으로 가져감)
        GameManager.Instance.currentStageId = stageId;

        // 4. 전투 씬으로 이동
        NetworkManager.Singleton.SceneManager.LoadScene("CombatScene", LoadSceneMode.Single);
    }
}