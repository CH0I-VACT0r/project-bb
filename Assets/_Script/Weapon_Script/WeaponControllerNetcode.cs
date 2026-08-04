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
    public AoEIndicatorMesh aoeIndicator;

    [Header("Rendering")]
    public float orbitYMultiplier = 0.5f;
    private SpriteRenderer weaponSprite;
    private SpriteRenderer playerSprite;

    private ContactFilter2D enemyFilter;
    private Collider2D[] hitBuffer = new Collider2D[64];

    public NetworkVariable<Vector2> networkAimDir = new NetworkVariable<Vector2>(
        Vector2.right,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Dynamic Orbit Settings")]
    public float minOrbitSpeed = 90f;
    public float maxOrbitSpeed = 180f;

    private float targetOrbitSpeed;
    private float currentOrbitSpeed;
    private float previousAngle = 0f;
    private float currentAngle = 0f;

    private enum WeaponState { Orbiting, Attacking, Returning }
    private WeaponState currentState = WeaponState.Orbiting;
    private Vector3 attackTargetPos;

    private int currentComboIndex = 0;
    private float lastAttackTime = 0f;
    private bool isAttacking = false;
    private Coroutine aoeCoroutine;

    #region Unity Lifecycle
    void Awake()
    {
        enemyFilter = new ContactFilter2D();
        enemyFilter.useLayerMask = true;
        enemyFilter.layerMask = enemyLayer;
        enemyFilter.useTriggers = true;

        weaponSprite = GetComponent<SpriteRenderer>();

        if (aoeIndicator == null)
        {
            GameObject indicatorObj = new GameObject("Dynamic_AoE_Indicator");
            aoeIndicator = indicatorObj.AddComponent<AoEIndicatorMesh>();

            MeshRenderer mr = indicatorObj.GetComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Sprites/Default"));
            mr.material.color = new Color(1f, 0f, 0f, 0.4f);
            mr.sortingOrder = 100;

            indicatorObj.SetActive(false);
        }

        if (aoeIndicator != null)
        {
            aoeIndicator.transform.SetParent(this.transform);
            aoeIndicator.transform.localPosition = Vector3.zero;
        }
    }

    void Start()
    {
        // 왜곡 방지: 무기를 플레이어의 자식에서 강제 분리
        transform.SetParent(null);

        if (playerTransform != null)
        {
            playerSprite = playerTransform.GetComponent<SpriteRenderer>();
        }

        targetOrbitSpeed = weaponData.orbitSpeed;
        currentOrbitSpeed = weaponData.orbitSpeed;
    }

    void Update()
    {
        // 플레이어 파괴 시 무기도 즉시 파기하여 에러 방지
        if (playerTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        UpdateOrbitAngle();

        if (IsOwner)
        {
            Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            mousePos.z = 0;
            Vector2 aimDir = (mousePos - playerTransform.position).normalized;

            RequestAimUpdateServerRpc(aimDir);
            HandleComboInput();
        }

        switch (currentState)
        {
            case WeaponState.Orbiting: HandleAimingVisuals(); break;
            case WeaponState.Attacking: HandleAttackMove(); break;
            case WeaponState.Returning: HandleReturnMove(); break;
        }

        UpdateSortingOrder();
    }
    #endregion

    #region Weapon Aiming System & Movement
    [ServerRpc]
    private void RequestAimUpdateServerRpc(Vector2 dir)
    {
        networkAimDir.Value = dir;
    }

    private void HandleAimingVisuals()
    {
        transform.position = GetExpectedOrbitPosition();
        Vector2 aimDir = networkAimDir.Value;
        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        if (weaponSprite != null) weaponSprite.flipY = (aimDir.x < 0);
    }

    private Vector3 GetExpectedOrbitPosition()
    {
        float rad = currentAngle * Mathf.Deg2Rad;
        float xOffset = Mathf.Cos(rad) * weaponData.orbitRadius;
        float yOffset = Mathf.Sin(rad) * (weaponData.orbitRadius * orbitYMultiplier);
        return playerTransform.position + new Vector3(xOffset, yOffset, 0);
    }

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

    private void HandleAttackMove()
    {
        transform.position = Vector3.MoveTowards(transform.position, attackTargetPos, weaponData.travelSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, attackTargetPos) < 0.1f)
        {
            currentState = WeaponState.Returning;
        }
    }

    private void HandleReturnMove()
    {
        Vector3 orbitPos = GetExpectedOrbitPosition();
        transform.position = Vector3.MoveTowards(transform.position, orbitPos, weaponData.travelSpeed * Time.deltaTime);
        if (Vector3.Distance(transform.position, orbitPos) < 0.1f)
        {
            currentState = WeaponState.Orbiting;
        }
    }

    private void UpdateSortingOrder()
    {
        if (weaponSprite == null || playerSprite == null) return;
        weaponSprite.sortingOrder = (transform.position.y > playerTransform.position.y)
            ? playerSprite.sortingOrder - 1 : playerSprite.sortingOrder + 1;
    }
    #endregion

    #region Manual Combo System
    private void HandleComboInput()
    {
        if (isAttacking || currentState != WeaponState.Orbiting || weaponData.actionSteps == null || weaponData.actionSteps.Length == 0) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (Time.time - lastAttackTime > weaponData.comboWindow) currentComboIndex = 0;

            Vector3 direction = networkAimDir.Value;
            WeaponActionStep currentStep = weaponData.actionSteps[currentComboIndex];

            bool isMeleeAttack = (currentStep.actionTypes & (WeaponTypeFlags.Melee | WeaponTypeFlags.Slash)) != 0;
            bool isLaser = (currentStep.actionTypes & WeaponTypeFlags.Laser) != 0;

            if (isMeleeAttack)
            {
                Vector3 startPos = playerTransform.position + (Vector3)(direction * weaponData.orbitRadius);
                Vector3 targetPos = playerTransform.position + (Vector3)(direction * weaponData.travelDistance);

                PlayAttackVisualLocal(direction, startPos, targetPos, currentComboIndex);
            }
            else if (isLaser)
            {
                if (aoeCoroutine != null) StopCoroutine(aoeCoroutine);
                aoeCoroutine = StartCoroutine(ShowAoEVisual(direction, currentStep));
            }

            RequestComboAttackServerRpc(currentComboIndex, direction);

            StartCoroutine(AttackCooldownRoutine(currentStep.stepDelay));
            currentComboIndex++;
            if (currentComboIndex >= weaponData.actionSteps.Length) currentComboIndex = 0;
            lastAttackTime = Time.time;
        }
    }

    private void PlayAttackVisualLocal(Vector3 direction, Vector3 startPos, Vector3 targetPos, int comboIdx)
    {
        transform.position = startPos;

        attackTargetPos = targetPos;
        currentState = WeaponState.Attacking;

        if (aoeCoroutine != null) StopCoroutine(aoeCoroutine);
        aoeCoroutine = StartCoroutine(ShowAoEVisual(direction, weaponData.actionSteps[comboIdx]));

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        if (weaponSprite != null) weaponSprite.flipY = (direction.x < 0);
    }

    private IEnumerator AttackCooldownRoutine(float delay)
    {
        isAttacking = true;
        yield return new WaitForSeconds(delay);
        isAttacking = false;
    }

    [ServerRpc]
    private void RequestComboAttackServerRpc(int comboIndex, Vector3 direction)
    {
        if (comboIndex < 0 || comboIndex >= weaponData.actionSteps.Length) return;
        WeaponActionStep step = weaponData.actionSteps[comboIndex];
        ExecuteStepActionServer(step, direction);
    }
    #endregion

    #region Step Execution & Damage Calculation (Server Only)
    private float CalculateFinalDamage()
    {
        float adBonus = playerStats.AttackDamage.Value * weaponData.adScaling;
        float apBonus = playerStats.AbilityPower.Value * weaponData.apScaling;
        return weaponData.baseDamage + adBonus + apBonus;
    }

    private DamageInfo CreateBaseDamageInfo(float baseDmg, Vector3 knockbackDirection, float knockbackForce)
    {
        float accuracy = playerStats.classData != null ? playerStats.classData.accuracy : 100f;
        float penetration = 0f;

        if (playerStats.classData != null)
        {
            penetration = (weaponData.attackAttribute == AttackAttribute.Physical)
                ? playerStats.classData.physicalPenetration : playerStats.classData.magicPenetration;
        }

        return new DamageInfo
        {
            damageAmount = baseDmg,
            attackType = weaponData.attackAttribute,
            attackerAccuracy = accuracy,
            attackerPenetration = penetration,
            knockbackDir = knockbackDirection,
            knockbackForce = knockbackForce
        };
    }

    private void ExecuteStepActionServer(WeaponActionStep step, Vector3 direction)
    {
        float finalDamage = CalculateFinalDamage();
        Vector3 attackOrigin = playerTransform.position;

        if ((step.actionTypes & WeaponTypeFlags.Melee) != 0 || (step.actionTypes & WeaponTypeFlags.Slash) != 0)
        {
            // 플레이어 중심에서 attackRange 만큼 데미지 판정
            int hitCount = Physics2D.OverlapCircle(attackOrigin, step.attackRange, enemyFilter, hitBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                // 플레이어 기준 적 방향 계산
                Vector3 dirToEnemy = (hitBuffer[i].transform.position - attackOrigin).normalized;

                if ((step.actionTypes & WeaponTypeFlags.Slash) != 0)
                {
                    float angleToEnemy = Vector2.Angle(direction, dirToEnemy);
                    if (angleToEnemy > step.slashAngle / 2f) continue;
                }

                DamageInfo info = CreateBaseDamageInfo(finalDamage, dirToEnemy, step.knockbackForce);
                hitBuffer[i].GetComponent<IDamageable>()?.TakeDamage(info);
            }
        }

        if ((step.actionTypes & WeaponTypeFlags.Ranged) != 0 && step.projectilePrefab != null)
        {
            Vector3 projDir = direction;
            if (step.projectileBehavior == ProjectileBehavior.Homing)
            {
                Transform bestTarget = FindBestTarget();
                if (bestTarget != null) projDir = (bestTarget.position - attackOrigin).normalized;
            }

            Vector3 spawnOrigin = playerTransform.position + (Vector3)(direction * weaponData.orbitRadius);
            GameObject projObj = Instantiate(step.projectilePrefab, spawnOrigin, Quaternion.identity);

            ProjectileNetcode proj = projObj.GetComponent<ProjectileNetcode>();
            DamageInfo projInfo = CreateBaseDamageInfo(finalDamage, projDir, step.knockbackForce);
            proj.Initialize(projDir, step.projectileSpeed, projInfo);
            projObj.GetComponent<NetworkObject>().Spawn();
        }
    }

    private Transform FindBestTarget()
    {
        Collider2D[] targets = Physics2D.OverlapCircleAll(playerTransform.position, weaponData.autoTargetRange, enemyLayer);
        Transform nearestBoss = null;
        Transform nearestNormalEnemy = null;
        float minBossDist = Mathf.Infinity;
        float minNormalDist = Mathf.Infinity;

        foreach (var col in targets)
        {
            float dist = Vector2.Distance(playerTransform.position, col.transform.position);
            bool isBoss = col.CompareTag("Boss");

            if (isBoss)
            {
                if (dist < minBossDist) { minBossDist = dist; nearestBoss = col.transform; }
            }
            else
            {
                if (dist < minNormalDist) { minNormalDist = dist; nearestNormalEnemy = col.transform; }
            }
        }
        if (weaponData.isBossPriority && nearestBoss != null) return nearestBoss;
        return nearestNormalEnemy;
    }
    #endregion

    #region Visuals
    private IEnumerator ShowAoEVisual(Vector3 direction, WeaponActionStep step)
    {
        if (aoeIndicator == null) yield break;
        MeshRenderer meshRenderer = aoeIndicator.GetComponent<MeshRenderer>();

        aoeIndicator.ClearMesh();
        aoeIndicator.transform.position = transform.position;

        if ((step.actionTypes & WeaponTypeFlags.Laser) != 0)
        {
            float laserWidth = step.slashAngle > 0f ? step.slashAngle / 30f : 1f;
            aoeIndicator.DrawRectangle(step.attackRange, laserWidth);
        }
        else
        {
            float drawAngle = ((step.actionTypes & WeaponTypeFlags.Slash) != 0) ? step.slashAngle : 360f;
            aoeIndicator.DrawShape(step.attackRange, drawAngle, 24);
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        aoeIndicator.transform.rotation = Quaternion.Euler(0, 0, angle);

        aoeIndicator.gameObject.SetActive(true);
        if (meshRenderer != null) meshRenderer.enabled = true;

        yield return new WaitForSeconds(0.15f);

        if (meshRenderer != null) meshRenderer.enabled = false;
        aoeIndicator.gameObject.SetActive(false);
        aoeIndicator.ClearMesh();
    }
    #endregion
}