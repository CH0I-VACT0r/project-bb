using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer), typeof(NetworkAnimator))]
public class PlayerVisualNetcode : NetworkBehaviour
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    // 좌우 반전 상태를 서버에서 모든 클라이언트로 동기화하는 변수
    private NetworkVariable<bool> isFlipped = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public override void OnNetworkSpawn()
    {
        // 스폰 시점 및 값이 변할 때마다 콜백 함수 연결
        isFlipped.OnValueChanged += OnFlipStateChanged;

        // 늦게 접속한 클라이언트(Join late)를 위한 초기 상태 즉시 적용
        spriteRenderer.flipX = isFlipped.Value;
    }

    public override void OnNetworkDespawn()
    {
        // 객체 파괴 시 메모리 누수 방지를 위해 이벤트 연결 해제
        isFlipped.OnValueChanged -= OnFlipStateChanged;
    }

    // 서버 변수가 변경될 때마다 모든 클라이언트에서 자동 실행됨
    private void OnFlipStateChanged(bool previousValue, bool newValue)
    {
        spriteRenderer.flipX = newValue;
    }

    private void Update()
    {
        if (!IsOwner) return;

        float moveInput = 0f;
        float verticalInput = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveInput = 1f;
            else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveInput = -1f;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) verticalInput = 1f;
            else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) verticalInput = -1f;
        }

        // 애니메이션 동기화
        bool isWalking = (moveInput != 0 || verticalInput != 0);
        animator.SetBool("isWalking", isWalking);

        // 스프라이트 반전 동기화
        if (moveInput > 0 && isFlipped.Value)
        {
            UpdateFlipServerRpc(false);
        }
        else if (moveInput < 0 && !isFlipped.Value)
        {
            UpdateFlipServerRpc(true);
        }
    }

    [ServerRpc]
    private void UpdateFlipServerRpc(bool flipState)
    {
        isFlipped.Value = flipState;
    }
}