using Unity.Netcode;
using UnityEngine;

public class LobbyPortal : MonoBehaviour
{
    [HideInInspector]
    public GameObject stageSelectionUI; // 활성화할 스테이지 선택 UI 캔버스

    private void Start()
    {
        if (stageSelectionUI == null)
        {
            StageSelectionUI ui = FindFirstObjectByType<StageSelectionUI>(FindObjectsInactive.Include);
            if (ui != null)
            {
                stageSelectionUI = ui.gameObject;
            }
            else
            {
                Debug.LogError("씬에 StageSelectionUI 컴포넌트가 부착된 오브젝트가 없습니다!");
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            NetworkObject netObj = collision.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                stageSelectionUI.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            NetworkObject netObj = collision.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                if (stageSelectionUI != null)
                {
                    stageSelectionUI.SetActive(false);
                }
            }
        }
    }
}