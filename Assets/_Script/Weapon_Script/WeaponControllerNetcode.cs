using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponControllerNetcode : NetworkBehaviour
{
    public PlayerStatManager playerStats;
    public Transform playerTransform;
    public WeaponDataSO weaponData;
    public LayerMask enemyLayer;

    [Header("Visuals")]
    public SpriteRenderer aoeIndicator;

    [Header("Rendering")]
    public float orbitYMultiplier = 0.5f;
    private SpriteRenderer weaponSprite;
    private SpriteRenderer playerSprite;

    private ContactFilter2D enemyFilter;
    private Collider2D[] hitBuffer = new Collider2D[64];

    [Header("Orbit Damage Settings")]
    public float orbitDamageTickRate = 0.5f;
    public float orbitDamageRadius = 0.5f;
    public float orbitDamageMultiplier = 0.5f;

    private float orbitDamageTimer = 0f;

    [Header("Dynamic Orbit Settings")]
    public float minOrbitSpeed = 90f;
    public float maxOrbitSpeed = 180f;

    private float targetOrbitSpeed;
    private float currentOrbitSpeed;
    private float previousAngle = 0f;

    private enum WeaponState { Orbiting, Attacking, Returning }
    private WeaponState currentState = WeaponState.Orbiting;

    private float currentAngle = 0f;
    private float currentCooldown = 0f;
    private Vector3 targetDirection;
    private Vector3 targetPosition;

    #region Unity Lifecycle
    void Awake()
    {
        enemyFilter = new ContactFilter2D();
        enemyFilter.useLayerMask = true;
        enemyFilter.layerMask = enemyLayer;
        enemyFilter.useTriggers = true;

        weaponSprite = GetComponent<SpriteRenderer>();

        if (aoeIndicator != null) aoeIndicator.enabled = false;
    }

    void Start()
    {
        if (playerTransform != null)
        {
            playerSprite = playerTransform.GetComponent<SpriteRenderer>();
        }

        targetOrbitSpeed = weaponData.orbitSpeed;
        currentOrbitSpeed = weaponData.orbitSpeed;
    }

    void Update()
    {
        UpdateOrbitAngle();

        switch (currentState)
        {
            case WeaponState.Orbiting:
                HandleOrbit();
                HandleCooldown();
                break;
            case WeaponState.Attacking:
                if (weaponData.weaponType != WeaponType.Ranged)
                    HandleAttackMove();
                break;
            case WeaponState.Returning:
                HandleReturnMove();
                break;
        }

        UpdateSortingOrder();
    }
    #endregion

    #region State & Cooldown Management
    private void HandleCooldown()
    {
        if (!IsOwner) return;

        currentCooldown -= Time.deltaTime;
        if (currentCooldown <= 0f)
        {
            Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            mousePos.z = 0;

            Vector3 direction = (mousePos - transform.position).normalized;

            RequestAttackServerRpc(direction);

            float cdr = Mathf.Clamp(playerStats.CooldownReduction.Value, 0f, 80f);
            float finalCooldown = weaponData.baseCooldown * (1f - (cdr / 100f));

            currentCooldown = finalCooldown;
        }
    }

    private void UpdateSortingOrder()
    {
        if (weaponSprite == null || playerSprite == null) return;

        if (transform.position.y > playerTransform.position.y)
        {
            weaponSprite.sortingOrder = playerSprite.sortingOrder - 1;
        }
        else
        {
            weaponSprite.sortingOrder = playerSprite.sortingOrder + 1;
        }
    }
    #endregion

    #region Orbiting System
    private void UpdateOrbitAngle()
    {
        currentOrbitSpeed = Mathf.Lerp(currentOrbitSpeed, targetOrbitSpeed, Time.deltaTime * 3f);

        previousAngle = currentAngle;
        currentAngle += currentOrbitSpeed * Time.deltaTime;

        if (previousAngle < 90f && currentAngle >= 90f)
        {
            int minMultiple = Mathf.RoundToInt(minOrbitSpeed / 5f);
            int maxMultiple = Mathf.RoundToInt(maxOrbitSpeed / 5f);

            targetOrbitSpeed = Random.Range(minMultiple, maxMultiple + 1) * 5;
        }

        currentAngle %= 360f;
    }

    private Vector3 GetExpectedOrbitPosition()
    {
        float rad = currentAngle * Mathf.Deg2Rad;

        float xOffset = Mathf.Cos(rad) * weaponData.orbitRadius;
        float yOffset = Mathf.Sin(rad) * (weaponData.orbitRadius * orbitYMultiplier);

        return playerTransform.position + new Vector3(xOffset, yOffset, 0);
    }

    private void HandleOrbit()
    {
        transform.position = GetExpectedOrbitPosition();

        if (IsServer && weaponData.weaponType != WeaponType.Ranged)
        {
            orbitDamageTimer -= Time.deltaTime;
            if (orbitDamageTimer <= 0f)
            {
                PerformOrbitDamageServer();
                orbitDamageTimer = orbitDamageTickRate;
            }
        }
    }
    #endregion

    #region Attack Movement System
    [ServerRpc]
    private void RequestAttackServerRpc(Vector3 direction)
    {
        targetDirection = direction;

        if (weaponData.weaponType == WeaponType.Ranged)
        {
            SpawnProjectileServer(direction);
            ExecuteAttackClientRpc(direction, transform.position);
        }
        else
        {
            Vector3 tPos = transform.position + (direction * weaponData.travelDistance);
            ExecuteAttackClientRpc(direction, tPos);
        }
    }

    [ClientRpc]
    private void ExecuteAttackClientRpc(Vector3 direction, Vector3 targetPos)
    {
        targetDirection = direction;
        targetPosition = targetPos;

        if (weaponData.weaponType != WeaponType.Ranged)
        {
            currentState = WeaponState.Attacking;
        }
    }

    private void HandleAttackMove()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, weaponData.travelSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            if (IsServer) PerformMeleeDamageServer();

            StartCoroutine(ShowAoEVisual());

            currentState = WeaponState.Returning;
        }
    }

    private void HandleReturnMove()
    {
        Vector3 targetOrbitPos = GetExpectedOrbitPosition();

        transform.position = Vector3.MoveTowards(transform.position, targetOrbitPos, weaponData.travelSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetOrbitPos) < 0.1f)
        {
            currentState = WeaponState.Orbiting;
        }
    }
    #endregion

    #region Combat & Damage Calculation (Server Only)
    private float CalculateFinalDamage()
    {
        float adBonus = playerStats.AttackDamage.Value * weaponData.adScaling;
        float apBonus = playerStats.AbilityPower.Value * weaponData.apScaling;

        return weaponData.baseDamage + adBonus + apBonus;
    }

    private void PerformOrbitDamageServer()
    {
        int hitCount = Physics2D.OverlapCircle(transform.position, orbitDamageRadius, enemyFilter, hitBuffer);
        float finalDamage = CalculateFinalDamage() * orbitDamageMultiplier;

        for (int i = 0; i < hitCount; i++)
        {
            IDamageable damageableTarget = hitBuffer[i].GetComponent<IDamageable>();
            damageableTarget?.TakeDamage(finalDamage);
        }
    }

    private void PerformMeleeDamageServer()
    {
        int hitCount = Physics2D.OverlapCircle(transform.position, weaponData.attackRange, enemyFilter, hitBuffer);
        float finalDamage = CalculateFinalDamage();

        for (int i = 0; i < hitCount; i++)
        {
            if (weaponData.weaponType == WeaponType.Slash)
            {
                Vector3 dirToEnemy = (hitBuffer[i].transform.position - transform.position).normalized;
                float angleToEnemy = Vector2.Angle(targetDirection, dirToEnemy);

                if (angleToEnemy > weaponData.slashAngle / 2f) continue;
            }

            IDamageable damageableTarget = hitBuffer[i].GetComponent<IDamageable>();
            damageableTarget?.TakeDamage(finalDamage);
        }
    }

    private void SpawnProjectileServer(Vector3 direction)
    {
        GameObject projObj = Instantiate(weaponData.projectilePrefab, transform.position, Quaternion.identity);

        ProjectileNetcode proj = projObj.GetComponent<ProjectileNetcode>();
        float finalDamage = CalculateFinalDamage();
        proj.Initialize(direction, weaponData.projectileSpeed, finalDamage);

        projObj.GetComponent<NetworkObject>().Spawn();
    }
    #endregion

    #region Visuals
    private IEnumerator ShowAoEVisual()
    {
        if (aoeIndicator == null) yield break;

        float diameter = weaponData.attackRange * 2f;
        aoeIndicator.transform.localScale = new Vector3(diameter, diameter, 1f);

        aoeIndicator.enabled = true;
        yield return new WaitForSeconds(0.15f);
        aoeIndicator.enabled = false;
    }
    #endregion
}