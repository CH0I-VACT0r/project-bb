using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStatManager))]
public class PlayerMovementNetcode : NetworkBehaviour
{
    private Rigidbody2D rb;
    private PlayerStatManager stats;
    private StatusEffectManagerNetcode statusManager;

    private Vector2 movement;
    private bool isDashing = false;

    [Header("Dash Settings")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public int maxDashCount = 2;
    public float comboWindow = 2f;
    public float dashCooldown = 3f;

    [Header("UI Settings")]
    public Image dashTimerUI;
    public Image dashTimerBG;

    public NetworkVariable<int> currentDashCount = new NetworkVariable<int>(2);
    public NetworkVariable<bool> isDashCooldown = new NetworkVariable<bool>(false);

    private float serverComboTimer = 0f;
    private int localDashCount;
    private float localComboTimer = 0f;
    private float localCooldownTimer = 0f;
    private bool isLocalCooldown = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<PlayerStatManager>();
        statusManager = GetComponent<StatusEffectManagerNetcode>();

        if (dashTimerUI != null) dashTimerUI.enabled = false;
        if (dashTimerBG != null) dashTimerBG.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentDashCount.Value = maxDashCount;
        }

        if (IsOwner)
        {
            localDashCount = maxDashCount;

            CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
            if (camFollow != null)
            {
                camFollow.target = this.transform;
            }

            InputManager.Instance.Controls.Gameplay.Enable();
            InputManager.Instance.Controls.Gameplay.Dash.performed += OnDashPerformed;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            InputManager.Instance.Controls.Gameplay.Dash.performed -= OnDashPerformed;
            InputManager.Instance.Controls.Gameplay.Disable();
        }
    }

    void Update()
    {
        if (IsServer) UpdateServerDashTimers();

        if (!IsOwner) return;
        UpdateLocalDashUI();
        if (isDashing) return;

        if (statusManager != null && (statusManager.isStunned.Value || statusManager.isTaunted.Value || statusManager.isFeared.Value))
        {
            movement = Vector2.zero;
            return;
        }

        movement = InputManager.Instance.Controls.Gameplay.Move.ReadValue<Vector2>().normalized;
    }

    void FixedUpdate()
    {
        if (!IsOwner) return;
        if (isDashing) return;

        if (statusManager != null && statusManager.isStunned.Value)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 finalMoveDir = movement.normalized;

        if (statusManager != null && (statusManager.isTaunted.Value || statusManager.isFeared.Value) && statusManager.effectSourceId.Value != 0)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(statusManager.effectSourceId.Value, out NetworkObject sourceObj))
            {
                if (statusManager.isTaunted.Value)
                {
                    finalMoveDir = ((Vector2)sourceObj.transform.position - (Vector2)transform.position).normalized;
                }
                else if (statusManager.isFeared.Value)
                {
                    finalMoveDir = ((Vector2)transform.position - (Vector2)sourceObj.transform.position).normalized;
                }
            }
        }

        float currentSpeed = stats.MoveSpeed.Value * (statusManager != null ? statusManager.moveSpeedMultiplier.Value : 1f);
        rb.linearVelocity = finalMoveDir * currentSpeed;
    }

    private void OnDashPerformed(InputAction.CallbackContext context)
    {
        if (statusManager != null && (statusManager.isStunned.Value || statusManager.isTaunted.Value || statusManager.isFeared.Value)) return;
        if (isDashing) return;

        if (localDashCount > 0 && !isLocalCooldown)
        {
            StartCoroutine(LocalDashRoutine());
            RequestDashServerRpc();

            localDashCount--;

            if (localDashCount > 0)
            {
                localComboTimer = comboWindow;
            }
            else
            {
                isLocalCooldown = true;
                localCooldownTimer = dashCooldown;
                localComboTimer = 0f;
            }
        }
    }

    private void UpdateLocalDashUI()
    {
        if (dashTimerUI == null) return;

        // 대시 쿨타임 (빨간색)
        if (isLocalCooldown)
        {
            localCooldownTimer -= Time.deltaTime;

            dashTimerUI.enabled = true;
            if (dashTimerBG != null) dashTimerBG.enabled = true;

            dashTimerUI.color = Color.red;
            dashTimerUI.fillAmount = 1f - (localCooldownTimer / dashCooldown);

            if (localCooldownTimer <= 0f)
            {
                isLocalCooldown = false;
                localDashCount = maxDashCount;

                dashTimerUI.enabled = false;
                if (dashTimerBG != null) dashTimerBG.enabled = false;
            }
        }
        // 대쉬 대기 (노란색)
        else if (localComboTimer > 0f)
        {
            localComboTimer -= Time.deltaTime;

            dashTimerUI.enabled = true;
            if (dashTimerBG != null) dashTimerBG.enabled = true;

            dashTimerUI.color = Color.yellow;
            dashTimerUI.fillAmount = localComboTimer / comboWindow;

            if (localComboTimer <= 0f)
            {
                isLocalCooldown = true;
                localCooldownTimer = dashCooldown;
                localComboTimer = 0f;
            }
        }
        else
        {
            dashTimerUI.enabled = false;
            if (dashTimerBG != null) dashTimerBG.enabled = false; // 배경 끄기
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

        stats.GrantInvincibility(dashDuration);
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