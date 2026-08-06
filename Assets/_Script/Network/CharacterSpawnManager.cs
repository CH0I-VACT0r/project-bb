using Unity.Netcode;
using UnityEngine;

public class CharacterSpawnManager : NetworkBehaviour
{
    public static CharacterSpawnManager Instance;

    [Tooltip("에디터에서 만든 PlayerClassDataSO 에셋들을 등록하세요.")]
    public PlayerClassDataSO[] availableClasses;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 클라이언트 UI 버튼 클릭 시 호출
    public void SelectClassAndSpawn(int classId)
    {
        if (!NetworkManager.Singleton.IsClient) return;
        RequestSpawnRpc(classId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestSpawnRpc(int classId, RpcParams rpcParams = default)
    {
        // ServerRpcParams 대신 RpcParams 사용
        ulong clientId = rpcParams.Receive.SenderClientId;

        // 이미 스폰된 캐릭터가 있는지 중복 검사
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
        {
            return;
        }

        PlayerClassDataSO selectedClass = GetClassDataById(classId);
        if (selectedClass == null || selectedClass.playerPrefab == null)
        {
            Debug.LogError($"[SpawnManager] ID {classId}에 해당하는 직업 데이터 또는 프리팹이 없습니다.");
            return;
        }

        // 서버 인스턴스화
        GameObject playerInstance = Instantiate(selectedClass.playerPrefab);

        // 스폰 직전 PlayerStatManager에 선택한 ClassData 주입
        PlayerStatManager statManager = playerInstance.GetComponent<PlayerStatManager>();
        if (statManager != null)
        {
            statManager.classData = selectedClass;
        }

        // 네트워크 스폰 및 클라이언트 소유권(IsOwner) 부여 (이 때 PlayerStatManager의 OnNetworkSpawn이 실행되면서 NetworkVariable들이 갱신)
        playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }

    private PlayerClassDataSO GetClassDataById(int id)
    {
        foreach (var data in availableClasses)
        {
            if (data.classId == id) return data;
        }
        return null;
    }
}
