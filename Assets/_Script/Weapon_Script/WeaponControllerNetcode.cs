using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

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
    private float currentAngle = 0f;

    // 콤보 시스템 상태 변수
    private int currentComboIndex = 0;
    private float lastAttackTime = 0f;
    private bool isAttacking = false;

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
        HandleOrbit(); // 공전 무기는 패시브로 상시 작동
        UpdateSortingOrder();

        if (IsOwner)
        {
            HandleComboInput();
        }
    }
    #endregion

    #region Manual Combo System
    private void HandleComboInput()
    {
        if (isAttacking || weaponData.actionSteps == null || weaponData.actionSteps.Length == 0) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // 콤보 유효 시간 초과 시 1타로 초기화
            if (Time.time - lastAttackTime > weaponData.comboWindow)
            {
                currentComboIndex = 0;
            }

            Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            mousePos.z = 0;

            // 플레이어에서 마우스를 향하는 방향 벡터 산출
            Vector3 direction = (mousePos - transform.position).normalized;

            RequestComboAttackServerRpc(currentComboIndex, direction);

            // 해당 타격 스텝의 딜레이만큼 입력 잠금(난사 방지)
            StartCoroutine(AttackCooldownRoutine(weaponData.actionSteps[currentComboIndex].stepDelay));

            currentComboIndex++;
            if (currentComboIndex >= weaponData.actionSteps.Length)
            {
                currentComboIndex = 0; // 마지막 콤보 후 순환
            }
            lastAttackTime = Time.time;
        }
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
    // 최종 데미지 산출은 유지
    private float CalculateFinalDamage()
    {
        float adBonus = playerStats.AttackDamage.Value * weaponData.adScaling;
        float apBonus = playerStats.AbilityPower.Value * weaponData.apScaling;
        return weaponData.baseDamage + adBonus + apBonus;
    }

    // 공통 정보(명중, 관통)를 묶어주는 헬퍼 함수
    private DamageInfo CreateBaseDamageInfo(float baseDmg, Vector3 knockbackDirection, float knockbackForce)
    {
        // PlayerStatManager에 연결된 classData SO에서 명중과 관통 스탯을 가져옵니다.
        float accuracy = playerStats.classData != null ? playerStats.classData.accuracy : 100f;
        float penetration = 0f;

        if (playerStats.classData != null)
        {
            penetration = (weaponData.attackAttribute == AttackAttribute.Physical)
                ? playerStats.classData.physicalPenetration
                : playerStats.classData.magicPenetration;
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

        // 1. 근접 및 부채꼴 타격 연산
        if ((step.actionTypes & WeaponTypeFlags.Melee) != 0 || (step.actionTypes & WeaponTypeFlags.Slash) != 0)
        {
            int hitCount = Physics2D.OverlapCircle(transform.position, step.attackRange, enemyFilter, hitBuffer);

            for (int i = 0; i < hitCount; i++)
            {
                Vector3 dirToEnemy = (hitBuffer[i].transform.position - transform.position).normalized;

                if ((step.actionTypes & WeaponTypeFlags.Slash) != 0)
                {
                    float angleToEnemy = Vector2.Angle(direction, dirToEnemy);
                    if (angleToEnemy > step.slashAngle / 2f) continue;
                }

                // 타격 성공 시 DamageInfo 생성 후 전달
                DamageInfo info = CreateBaseDamageInfo(finalDamage, dirToEnemy, step.knockbackForce);

                IDamageable targetable = hitBuffer[i].GetComponent<IDamageable>();
                targetable?.TakeDamage(info);
            }
        }

        // 2. 투사체(Ranged) 발사 연산
        if ((step.actionTypes & WeaponTypeFlags.Ranged) != 0 && step.projectilePrefab != null)
        {
            Vector3 projDir = direction;

            if (step.projectileBehavior == ProjectileBehavior.Homing)
            {
                Transform bestTarget = FindBestTarget();
                if (bestTarget != null)
                {
                    projDir = (bestTarget.position - transform.position).normalized;
                }
            }

            GameObject projObj = Instantiate(step.projectilePrefab, transform.position, Quaternion.identity);
            ProjectileNetcode proj = projObj.GetComponent<ProjectileNetcode>();

            // 투사체에게 데미지와 넉백 정보를 포함한 info를 넘겨주도록 변경해야 함.
            DamageInfo projInfo = CreateBaseDamageInfo(finalDamage, projDir, step.knockbackForce);

            // 주의: ProjectileNetcode.cs의 Initialize 함수 역시 DamageInfo를 받도록 수정해야 완벽히 호환됩니다.
            proj.Initialize(projDir, step.projectileSpeed, projInfo);

            projObj.GetComponent<NetworkObject>().Spawn();
        }
    }

    // 유도형 투사체를 위한 우선순위 오토 타겟팅
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

    #region Orbiting System (Passive)
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

        if (IsServer)
        {
            orbitDamageTimer -= Time.deltaTime;
            if (orbitDamageTimer <= 0f)
            {
                PerformOrbitDamageServer();
                orbitDamageTimer = orbitDamageTickRate;
            }
        }
    }

    private void PerformOrbitDamageServer()
    {
        int hitCount = Physics2D.OverlapCircle(transform.position, orbitDamageRadius, enemyFilter, hitBuffer);
        float finalDamage = CalculateFinalDamage() * orbitDamageMultiplier;

        for (int i = 0; i < hitCount; i++)
        {
            Vector3 dirToEnemy = (hitBuffer[i].transform.position - transform.position).normalized;

            // 공전 무기의 넉백은 0으로 임시 고정 (필요시 WeaponDataSO에 orbitKnockback 추가)
            DamageInfo info = CreateBaseDamageInfo(finalDamage, dirToEnemy, 0f);

            IDamageable damageableTarget = hitBuffer[i].GetComponent<IDamageable>();
            damageableTarget?.TakeDamage(info);
        }
    }

    private void UpdateSortingOrder()
    {
        if (weaponSprite == null || playerSprite == null) return;
        weaponSprite.sortingOrder = (transform.position.y > playerTransform.position.y)
            ? playerSprite.sortingOrder - 1
            : playerSprite.sortingOrder + 1;
    }
    #endregion
}