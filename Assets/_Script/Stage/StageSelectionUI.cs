using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement; // LoadSceneMode 사용을 위해 필요

public class StageSelectionUI : MonoBehaviour
{
    public void OnClickEnterStage(int stageId)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (stageId > GameManager.Instance.highestUnlockedStage) return;

        GameManager.Instance.currentStageId = stageId;
        GameManager.Instance.ResetDungeonProgress();
        GameManager.Instance.currentFloor = 1;
        GameManager.Instance.nextRoomType = StageRoomType.Combat;

        if (SceneTransitionCurtain.Instance != null)
        {
            SceneTransitionCurtain.Instance.FadeOutAndCall(() => {
                NetworkManager.Singleton.SceneManager.LoadScene("CombatScene", LoadSceneMode.Single);
            });
        }
        else
        {
            NetworkManager.Singleton.SceneManager.LoadScene("CombatScene", LoadSceneMode.Single);
        }
    }
}