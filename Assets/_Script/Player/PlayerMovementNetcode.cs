using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStatManager))]
public class PlayerMovementNetcode : NetworkBehaviour
{
    private Rigidbody2D rb;
    private PlayerStatManager stats;

    private Vector2 movement;
    private bool isDashing = false;

    [Header("Dash Settings")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public int maxDashCount = 2;
    public float comboWindow = 2f;
    public float dashCooldown = 3f;

    public NetworkVariable<int> currentDashCount = new NetworkVariable<int>(2);
    public NetworkVariable<bool> isDashCooldown = new NetworkVariable<bool>(false);

    private float serverComboTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<PlayerStatManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentDashCount.Value = maxDashCount;
        }
    }

    void Update()
    {
        if (IsServer)
        {
            UpdateServerDashTimers();
        }

        if (!IsOwner) return;
        if (isDashing) return;

        InputMovement();
        HandleDashInput();
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;
        if (isDashing) return;

        // Unity 6 API 반영: velocity -> linearVelocity
        rb.linearVelocity = movement.normalized * stats.MoveSpeed.Value;
    }

    private void InputMovement()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");
    }

    private void HandleDashInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && currentDashCount.Value > 0 && !isDashCooldown.Value)
        {
            StartCoroutine(LocalDashRoutine());
            RequestDashServerRpc();
        }
    }

    private IEnumerator LocalDashRoutine()
    {
        isDashing = true;
        Vector2 dashDirection = movement != Vector2.zero ? movement.normalized : new Vector2(transform.localScale.x, 0).normalized;

        // Unity 6 API 반영: velocity -> linearVelocity
        rb.linearVelocity = dashDirection * dashSpeed;
        yield return new WaitForSeconds(dashDuration);

        isDashing = false;
    }

    [ServerRpc]
    private void RequestDashServerRpc()
    {
        if (isDashCooldown.Value || currentDashCount.Value <= 0) return;

        currentDashCount.Value--;
        serverComboTimer = comboWindow;
    }

    private void UpdateServerDashTimers()
    {
        if (isDashCooldown.Value) return;

        if (currentDashCount.Value < maxDashCount)
        {
            serverComboTimer -= Time.deltaTime;

            if (serverComboTimer <= 0f || currentDashCount.Value == 0)
            {
                StartCoroutine(ServerCooldownRoutine());
            }
        }
    }

    private IEnumerator ServerCooldownRoutine()
    {
        isDashCooldown.Value = true;
        currentDashCount.Value = 0;

        yield return new WaitForSeconds(dashCooldown);

        currentDashCount.Value = maxDashCount;
        isDashCooldown.Value = false;
    }
}