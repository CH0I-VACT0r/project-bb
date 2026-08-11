using Unity.Netcode;
using UnityEngine;

public class HealStatueNetcode : NetworkBehaviour
{
    public NetworkVariable<bool> isUsed = new NetworkVariable<bool>(false);

    [Header("Interaction")]
    public float interactRange = 3f;

    private void OnMouseEnter()
    {
        if (!isUsed.Value && CursorManager.Instance != null)
        {
            CursorManager.Instance.SetInteractCursor();
        }
    }

    private void OnMouseExit()
    {
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetDefaultCursor();
        }
    }

    private void OnMouseDown()
    {
        if (!isUsed.Value)
        {
            InteractStatueRpc();
        }
    }

    // ★ 수정: 최신 Rpc 어트리뷰트 적용
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void InteractStatueRpc(RpcParams rpcParams = default)
    {
        if (isUsed.Value) return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out NetworkClient client))
        {
            float distance = Vector2.Distance(client.PlayerObject.transform.position, transform.position);

            if (distance > interactRange)
            {
                Debug.LogWarning($"[서버] 플레이어가 조각상에서 너무 멉니다.");
                return;
            }

            ApplyStatueEffect(senderId, client.PlayerObject.gameObject);
        }
    }

    private void ApplyStatueEffect(ulong clientId, GameObject playerObj)
    {
        isUsed.Value = true;
        bool isAnyPartyMemberDead = false;

        if (isAnyPartyMemberDead)
        {
            Debug.Log($"[서버] 플레이어 {clientId}가 파티원을 부활시켰습니다!");
        }
        else
        {
            PlayerStatManager stats = playerObj.GetComponent<PlayerStatManager>();
            if (stats != null)
            {
                stats.HealPercentage(25f);
            }
        }
        UpdateStatueVisualClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)] // (선택) 최신 NGO에서는 ClientRpc 대신 사용 가능하나, 기존 ClientRpc를 써도 무방합니다.
    private void UpdateStatueVisualClientRpc()
    {
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetDefaultCursor();
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.gray;
    }
}