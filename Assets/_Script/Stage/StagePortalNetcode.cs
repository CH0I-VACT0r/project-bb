using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StagePortalNetcode : NetworkBehaviour
{
    // 서버가 설정한 방의 성격이 모든 클라이언트에게 자동 동기화됩니다.
    public NetworkVariable<StageRoomType> roomType = new NetworkVariable<StageRoomType>();

    public SpriteRenderer portalRenderer; // 색상이나 이미지를 바꿀 렌더러

    public override void OnNetworkSpawn()
    {
        // 방 성격에 따라 문 색상 변경 (클라이언트 시각화)
        if (portalRenderer != null)
        {
            switch (roomType.Value)
            {
                case StageRoomType.Combat: portalRenderer.color = Color.white; break;
                case StageRoomType.Elite: portalRenderer.color = Color.red; break;
                case StageRoomType.Heal: portalRenderer.color = Color.green; break;
                case StageRoomType.Shop: portalRenderer.color = Color.yellow; break;
                case StageRoomType.Boss: portalRenderer.color = Color.magenta; break;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 오직 서버(방장)의 연산에서만 씬 이동을 처리합니다.
        if (!IsServer) return;

        if (collision.CompareTag("Player"))
        {
            NetworkObject netObj = collision.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner)
            {
                Debug.Log($"{roomType.Value} 방으로 이동합니다!");

                // 층수 증가 및 다음 방 성격 기록
                GameManager.Instance.currentFloor++;
                GameManager.Instance.nextRoomType = roomType.Value;

                // 전투 씬 다시 로드 (씬이 다시 열리면서 nextRoomType에 맞는 맵/적을 세팅하게 됨)
                NetworkManager.Singleton.SceneManager.LoadScene("CombatScene", LoadSceneMode.Single);
            }
        }
    }
}