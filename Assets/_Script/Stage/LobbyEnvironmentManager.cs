using Unity.Netcode;
using UnityEngine;

public class LobbyEnvironmentManager : NetworkBehaviour
{
    [Header("Lobby Theme Prefabs")]
    // 인덱스 0 = 1스테이지 테마 로비, 1 = 2스테이지 테마...
    public GameObject[] lobbyMapPrefabs;

    public override void OnNetworkSpawn()
    {
        // 맵 생성은 오직 방장(서버) 권한
        if (IsServer && lobbyMapPrefabs != null && lobbyMapPrefabs.Length > 0)
        {
            // 방장의 '최고 해금 스테이지'를 기준으로 보여줄 맵 인덱스 결정
            // (1스테이지 진입 상태면 0번 프리팹, 2스테이지 진입 상태면 1번 프리팹 스폰)
            int targetIndex = GameManager.Instance.highestUnlockedStage - 1;

            // 배열 범위를 벗어나지 않도록 안전하게 클램핑
            targetIndex = Mathf.Clamp(targetIndex, 0, lobbyMapPrefabs.Length - 1);

            // 로비 맵 생성 및 네트워크 동기화
            GameObject mapInstance = Instantiate(lobbyMapPrefabs[targetIndex], Vector3.zero, Quaternion.identity);

            NetworkObject netObj = mapInstance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }
        }
    }
}