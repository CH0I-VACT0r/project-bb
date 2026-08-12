using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class HealStatueNetcode : NetworkBehaviour
{
    public NetworkVariable<bool> isUsed = new NetworkVariable<bool>(false);
    private Collider2D statCollider;
    private bool isHovered = false;

    [Header("Interaction")]
    public float interactRange = 3f;

    private void Awake()
    {
        statCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (!IsSpawned) return;
        if (Camera.main == null || statCollider == null || Mouse.current == null) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(screenPos);

        bool isMouseOver = statCollider.OverlapPoint(mouseWorldPos);

        if (isMouseOver && !isUsed.Value && !isHovered)
        {
            isHovered = true;
            if (CursorManager.Instance != null)
                CursorManager.Instance.SetInteractCursor(); // 포탈과 다른 일반 상호작용 커서
        }
        else if ((!isMouseOver || isUsed.Value) && isHovered)
        {
            isHovered = false;
            if (CursorManager.Instance != null)
                CursorManager.Instance.SetDefaultCursor();
        }

        if (isMouseOver && !isUsed.Value && Mouse.current.leftButton.wasPressedThisFrame)
        {
            InteractStatueRpc();
        }
    }

    private void OnDisable()
    {
        if (isHovered && CursorManager.Instance != null)
        {
            CursorManager.Instance.SetDefaultCursor();
            isHovered = false;
        }
    }

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

        if (CombatStageManager.Instance != null)
        {
            CombatStageManager.Instance.StageCleared();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateStatueVisualClientRpc()
    {
        // 상호작용이 끝나면 켜져있던 커서를 즉시 기본 상태로 되돌림
        if (isHovered && CursorManager.Instance != null)
        {
            CursorManager.Instance.SetDefaultCursor();
            isHovered = false;
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.gray;
    }
}