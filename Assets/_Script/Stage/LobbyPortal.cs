using Unity.Netcode;
using UnityEngine;

public class LobbyPortal : MonoBehaviour
{
    public GameObject stageSelectionUI; // 활성화할 스테이지 선택 UI 캔버스

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            NetworkObject netObj = collision.GetComponent<NetworkObject>();
            // 충돌한 플레이어가 내(로컬) 캐릭터일 때만 화면에 UI를 띄움
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
                // ★ 씬 이동 시 UI가 이미 파괴되었는지 확인하는 방어 코드 추가
                if (stageSelectionUI != null)
                {
                    stageSelectionUI.SetActive(false);
                }
            }
        }
    }
}