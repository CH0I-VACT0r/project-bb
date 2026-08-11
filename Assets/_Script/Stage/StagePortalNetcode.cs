using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class StagePortalNetcode : NetworkBehaviour
{
    public NetworkVariable<StageRoomType> roomType = new NetworkVariable<StageRoomType>();
    public SpriteRenderer portalRenderer;

    [Header("Interaction")]
    public float interactRange = 2f;

    [Header("Portal Sprites")]
    public Sprite combatSprite; // 일반 몹 포탈 이미지
    public Sprite eliteSprite;  // 엘리트 포탈 이미지
    public Sprite healSprite;   // 회복 포탈 이미지
    public Sprite shopSprite;   // 상점 포탈 이미지
    public Sprite bossSprite;   // 보스 포탈 이미지

    private Collider2D portalCollider;
    private bool isHovered = false;

    private void Awake()
    {
        portalCollider = GetComponent<Collider2D>();
        if (portalCollider == null)
        {
            Debug.LogError($"[StagePortalNetcode] {gameObject.name}에 Collider2D가 없습니다! 프리팹 루트에 BoxCollider2D 등을 부착해야 합니다.");
        }
    }

    public override void OnNetworkSpawn()
    {
        if (portalRenderer != null)
        {
            switch (roomType.Value)
            {
                case StageRoomType.Combat: portalRenderer.sprite = combatSprite; break;
                case StageRoomType.Elite: portalRenderer.sprite = eliteSprite; break;
                case StageRoomType.Heal: portalRenderer.sprite = healSprite; break;
                case StageRoomType.Shop: portalRenderer.sprite = shopSprite; break;
                case StageRoomType.Boss: portalRenderer.sprite = bossSprite; break;
            }
        }
    }

    private void Update()
    {
        if (Camera.main == null || portalCollider == null || Mouse.current == null) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(screenPos);
        bool isMouseOver = portalCollider.OverlapPoint(mouseWorldPos);

        if (isMouseOver && !isHovered)
        {
            isHovered = true;
            if (CursorManager.Instance != null)
                CursorManager.Instance.SetPortalCursor();
        }
        else if (!isMouseOver && isHovered)
        {
            isHovered = false;
            if (CursorManager.Instance != null)
                CursorManager.Instance.SetDefaultCursor();
        }

        if (isMouseOver && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("[클라이언트] 포탈 클릭 감지됨 -> InteractPortalRpc 호출");
            InteractPortalRpc();
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
    private void InteractPortalRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(senderId, out NetworkClient client))
        {
            float distance = Vector2.Distance(client.PlayerObject.transform.position, transform.position);
            if (distance > interactRange) return;

            if (portalCollider != null) portalCollider.enabled = false;

            if (SceneTransitionCurtain.Instance != null)
            {
                SceneTransitionCurtain.Instance.FadeOutAndCall(() => {
                    ExecuteSceneLoadServer(roomType.Value);
                });
            }
            else
            {
                ExecuteSceneLoadServer(roomType.Value);
            }
        }
    }

    private void ExecuteSceneLoadServer(StageRoomType room)
    {
        if (CombatStageManager.Instance != null)
        {
            CombatStageManager.Instance.TransitionToNextStage(room);
        }
        else
        {
            GameManager.Instance.currentFloor++;
            GameManager.Instance.nextRoomType = room;
            NetworkManager.Singleton.SceneManager.LoadScene("CombatScene", LoadSceneMode.Single);
        }
    }
}