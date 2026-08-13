using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponControllerNetcode : NetworkBehaviour
{
    public PlayerStatManager playerStats;
    public Transform playerTransform;
    public WeaponDataSO weaponData;
    public LayerMask enemyLayer;

    [Header("Aim Assist Settings")]
    public float aimAssistRadius = 0.4f; // 커서 주변 적 탐지 반경
    public float maxAssistAngle = 45f;   // 플레이어 조준 방향에서 허용되는 최대 보정 각도
    public LayerMask enemyLayerMask;

    [Header("Visuals")]
    public AoEIndicatorMesh aoeIndicator;

    [Header("State")]
    public bool isLobbyMode = false;

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
    private float autoAttackTimer = 0f;
    private bool isAttacking = false;
    private Coroutine aoeCoroutine;

    #region Unity Lifecycle
    public void SetLobbyMode(bool isLobby)
    {
        isLobbyMode = isLobby;
    }

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
            aoeIndicator.transform.SetParent(this.transform.root);
        }
    }

    public void EquipWeapon(WeaponDataSO newWeaponData)
    {
        weaponData = newWeaponData;

        if (weaponData == null)
        {
            Debug.LogWarning("무기 데이터가 없습니다!");
            return;
        }

        transform.localScale = new Vector3(weaponData.weaponScale, weaponData.weaponScale, 1f);

        if (playerTransform != null)
        {
            playerSprite = playerTransform.GetComponent<SpriteRenderer>();
        }

        targetOrbitSpeed = weaponData.orbitSpeed;
        currentOrbitSpeed = weaponData.orbitSpeed;

        if (weaponSprite != null && weaponData.weaponSprite != null)
        {
            weaponSprite.sprite = weaponData.weaponSprite;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();  // 실행시킨 모든 코루틴 즉시 정지
        isAttacking = false; // 공격 상태 강제 초기화 (Deadlock 방지)
        
        if (aoeCoroutine != null)
        {
            aoeCoroutine = null; // 단일 코루틴 참조 변수 초기화
        }
        currentState = WeaponState.Returning;
    }

    void Update()
    {
        if (playerTransform == null)
        {
            if (aoeIndicator != null) Destroy(aoeIndicator.gameObject);
            Destroy(gameObject);
            return;
        }

        UpdateOrbitAngle();

        if (IsOwner)
        {
            Vector3 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
            mousePos.z = 0;

            Vector3 assistedTargetPos = GetAssistedAimPosition(playerTransform.position, mousePos);
            Vector2 aimDir = (assistedTargetPos - playerTransform.position).normalized;

            RequestAimUpdateServerRpc(aimDir);
            HandleAutoAttack();
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
    }

    private void HandleReturnMove()
    {
        Vector3 currentOrbitPos = GetExpectedOrbitPosition();
        transform.position = Vector3.MoveTowards(transform.position, currentOrbitPos, weaponData.travelSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, currentOrbitPos) < 0.2f)
        {
            transform.position = currentOrbitPos;
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

    #region Auto Attack System
    private void HandleAutoAttack()
    {
        if (isLobbyMode) return;

        if (isAttacking || weaponData.actionSteps == null || weaponData.actionSteps.Length == 0) return;

        autoAttackTimer -= Time.deltaTime;

        if (autoAttackTimer <= 0f && currentState == WeaponState.Orbiting)
        {
            StartCoroutine(AutoAttackSequence());
        }
    }

    private IEnumerator AutoAttackSequence()
    {
        isAttacking = true;

        for (int i = 0; i < weaponData.actionSteps.Length; i++)
        {
            if (this == null || gameObject == null || transform == null || playerTransform == null)
            {
                yield break; // 코루틴 즉시 강제 종료
            }

            currentComboIndex = i;
            Vector3 direction = networkAimDir.Value;
            WeaponActionStep currentStep = weaponData.actionSteps[i];

            bool isAoEAttack = (currentStep.actionTypes & (WeaponTypeFlags.Melee | WeaponTypeFlags.Slash | WeaponTypeFlags.Laser | WeaponTypeFlags.Single)) != 0;

            Vector3 targetPos = CalculateVisualTargetPosition(direction);
            PlayAttackVisualLocal(direction, targetPos, currentComboIndex, isAoEAttack);

            RequestComboAttackServerRpc(currentComboIndex, direction, targetPos);

            yield return new WaitForSeconds(currentStep.stepDelay);
        }

        if (this != null && transform != null)
        {
            currentState = WeaponState.Returning;
            autoAttackTimer = weaponData.comboWindow;
            isAttacking = false;
        }
    }

    private void PlayAttackVisualLocal(Vector3 direction, Vector3 targetPos, int comboIdx, bool isAoE)
    {
        attackTargetPos = targetPos;
        currentState = WeaponState.Attacking;

        if (aoeCoroutine != null) StopCoroutine(aoeCoroutine);

        // 근접/스플래시 타격일 때만 빨간색 범위 인디케이터 표시
        if (isAoE)
        {
            aoeCoroutine = StartCoroutine(ShowAoEVisual(direction, weaponData.actionSteps[comboIdx]));
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        if (weaponSprite != null) weaponSprite.flipY = (direction.x < 0);
    }

    private Vector3 CalculateVisualTargetPosition(Vector3 direction)
    {
        float maxReach = weaponData.orbitRadius + weaponData.travelDistance;
        Vector3 defaultTarget = playerTransform.position + (direction * maxReach);

        // 최대 사거리 내의 모든 적 스캔
        int hitCount = Physics2D.OverlapCircle(playerTransform.position, maxReach, enemyFilter, hitBuffer);
        float closestDist = float.MaxValue;
        Transform closestEnemy = null;

        for (int i = 0; i < hitCount; i++)
        {
            Vector3 dirToEnemy = (hitBuffer[i].transform.position - playerTransform.position).normalized;

            if (Vector2.Angle(direction, dirToEnemy) < 60f)
            {
                float dist = Vector2.Distance(playerTransform.position, hitBuffer[i].transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestEnemy = hitBuffer[i].transform;
                }
            }
        }

        if (closestEnemy != null)
        {
            return closestEnemy.position;
        }
        return defaultTarget;
    }

    [ServerRpc]
    private void RequestComboAttackServerRpc(int comboIndex, Vector3 direction, Vector3 attackOrigin)
    {
        if (comboIndex < 0 || comboIndex >= weaponData.actionSteps.Length) return;

        WeaponActionStep step = weaponData.actionSteps[comboIndex];
        ExecuteStepActionServer(step, direction, attackOrigin);
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

    private void ExecuteStepActionServer(WeaponActionStep step, Vector3 direction, Vector3 attackOrigin)
    {
        float finalDamage = CalculateFinalDamage();
        bool isSingle = (step.actionTypes & WeaponTypeFlags.Single) != 0;
        bool isMelee = (step.actionTypes & WeaponTypeFlags.Melee) != 0;
        bool isSlash = (step.actionTypes & WeaponTypeFlags.Slash) != 0;

        // 1. 직접 타격 계열 (Single, Melee, Slash) 연산
        if (isSingle || isMelee || isSlash)
        {
            int hitCount = Physics2D.OverlapCircle(attackOrigin, step.attackRange, enemyFilter, hitBuffer);
            HashSet<IDamageable> alreadyHitEnemies = new HashSet<IDamageable>();

            if (isSingle && hitCount > 0)
            {
                float closestDist = float.MaxValue;
                Collider2D closestEnemy = null;

                for (int i = 0; i < hitCount; i++)
                {
                    float dist = Vector2.Distance(attackOrigin, hitBuffer[i].transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestEnemy = hitBuffer[i];
                    }
                }

                if (closestEnemy != null)
                {
                    IDamageable target = closestEnemy.GetComponentInParent<IDamageable>();
                    if (target != null)
                    {
                        Vector3 dirToEnemy = (closestEnemy.transform.position - attackOrigin).normalized;
                        DamageInfo info = CreateBaseDamageInfo(finalDamage, dirToEnemy, step.knockbackForce);
                        target.TakeDamage(info);
                    }
                }
            }
            // 기존 다수 타격(Melee, Slash) 처리
            else if (!isSingle)
            {
                for (int i = 0; i < hitCount; i++)
                {
                    Collider2D enemyCol = hitBuffer[i];

                    IDamageable target = enemyCol.GetComponentInParent<IDamageable>();
                    if (target == null) continue;
                    if (alreadyHitEnemies.Contains(target)) continue;

                    Vector3 dirToEnemy = (enemyCol.transform.position - attackOrigin).normalized;

                    if (isSlash)
                    {
                        float distToCenter = Vector2.Distance(attackOrigin, enemyCol.transform.position);
                        if (distToCenter > 0.1f)
                        {
                            float angleToEnemy = Vector2.Angle(direction, dirToEnemy);
                            if (angleToEnemy > step.slashAngle / 2f) continue;
                        }
                    }

                    // 타격 성공 시 해당 개체를 기록하고 대미지 적용
                    alreadyHitEnemies.Add(target);
                    DamageInfo info = CreateBaseDamageInfo(finalDamage, dirToEnemy, step.knockbackForce);
                    target.TakeDamage(info);
                }
            }
        }

        // 2. 투사체 연산 (Ranged) - 기존 로직 유지
        if ((step.actionTypes & WeaponTypeFlags.Ranged) != 0 && step.projectilePrefab != null)
        {
            StartCoroutine(SpawnProjectilesRoutine(step, direction, attackOrigin, finalDamage));
        }
    }

    private IEnumerator SpawnProjectilesRoutine(WeaponActionStep step, Vector3 direction, Vector3 origin, float finalDamage)
    {
        // 안전장치: 0 이하의 값이 들어오면 1로 보정
        int bCount = Mathf.Max(1, step.burstCount);
        int pCount = Mathf.Max(1, step.projectileCount);
        float pRange = step.projectileRange > 0f ? step.projectileRange : 15f;

        // 사정거리를 생존 시간으로 변환
        float lifeTime = pRange / step.projectileSpeed;

        for (int b = 0; b < bCount; b++)
        {
            // 타겟팅 유도(Homing) 기능이 켜져있다면, 기본 발사 방향(direction)을 타겟 방향으로 덮어씀
            Vector3 baseDir = direction;
            if (step.projectileBehavior == ProjectileBehavior.Homing)
            {
                Transform bestTarget = FindBestTarget();
                if (bestTarget != null) baseDir = (bestTarget.position - transform.position).normalized;
            }

            // 분사 각도(Spread) 세팅
            // 개수가 1개면 0도, 여러 개면 시작 각도(-60)부터 일정 간격(60)으로 배치
            float startAngle = 0f;
            float angleStep = 0f;

            if (pCount > 1)
            {
                if (step.spreadAngle >= 360f)
                {
                    startAngle = 0f;
                    angleStep = 360f / pCount;
                }
                else
                {
                    startAngle = -step.spreadAngle / 2f;
                    angleStep = step.spreadAngle / (pCount - 1);
                }
            }

            for (int p = 0; p < pCount; p++)
            {
                float currentAngleOffset = startAngle + (angleStep * p);
                Vector3 finalProjDir = Quaternion.Euler(0, 0, currentAngleOffset) * baseDir;

                float rotAngle = Mathf.Atan2(finalProjDir.y, finalProjDir.x) * Mathf.Rad2Deg;
                Quaternion projRotation = Quaternion.Euler(0, 0, rotAngle);

                GameObject projObj = Instantiate(step.projectilePrefab, origin, Quaternion.identity);

                ProjectileNetcode proj = projObj.GetComponent<ProjectileNetcode>();
                DamageInfo projInfo = CreateBaseDamageInfo(finalDamage, finalProjDir, step.knockbackForce);

                proj.Initialize(finalProjDir, step.projectileSpeed, projInfo);

                NetworkObject netObj = projObj.GetComponent<NetworkObject>();
                netObj.Spawn();

                StartCoroutine(DespawnProjectileAfterTime(netObj, lifeTime));
            }

            // 연사 횟수가 더 남았다면 다음 발사까지 대기
            if (b < bCount - 1)
            {
                yield return new WaitForSeconds(step.burstInterval);
            }
        }
    }

    private IEnumerator DespawnProjectileAfterTime(NetworkObject netObj, float lifeTime)
    {
        yield return new WaitForSeconds(lifeTime);
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
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

    // 공격 보정
    public Vector3 GetAssistedAimPosition(Vector3 playerPos, Vector3 cursorWorldPos)
    {
        if (weaponData == null) return cursorWorldPos;
       
        Vector3 mouseDir = (cursorWorldPos - playerPos).normalized; // 마우스가 가리키는 원본 방향
        float maxReach = weaponData.orbitRadius + weaponData.travelDistance; // 무기의 최대 타격 사거리 (궤도 반지름 + 전진 거리)

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(playerPos, maxReach, enemyLayerMask);
        Collider2D bestTarget = null;
        float closestDistanceToPlayer = float.MaxValue;

        foreach (Collider2D col in hitEnemies)
        {
            if (!col.CompareTag("Enemy")) continue;

            Vector3 toEnemy = (col.transform.position - playerPos).normalized;
            float distToPlayer = Vector2.Distance(playerPos, col.transform.position);

            // 마우스 방향 기준 좌우 45도 이내에 있는 적 필터링
            float angle = Vector3.Angle(mouseDir, toEnemy);
            if (angle <= 45f && distToPlayer < closestDistanceToPlayer)
            {
                closestDistanceToPlayer = distToPlayer;
                bestTarget = col;
            }
        }

        // 조건에 맞는 가장 가까운 적이 있다면 그 적의 중심 좌표로 조준선을 스냅(Snap), 없으면 원본 유지
        return bestTarget != null ? bestTarget.transform.position : cursorWorldPos;
    }
    #endregion

    #region Visuals
    private IEnumerator ShowAoEVisual(Vector3 direction, WeaponActionStep step)
    {
        if (aoeIndicator == null) yield break;
        MeshRenderer meshRenderer = aoeIndicator.GetComponent<MeshRenderer>();

        aoeIndicator.ClearMesh();
        aoeIndicator.transform.position = attackTargetPos;

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

        // 방향 설정 (부채꼴의 경우 진행 방향)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        aoeIndicator.transform.rotation = Quaternion.Euler(0, 0, angle);

        // 표시
        aoeIndicator.gameObject.SetActive(true);
        if (meshRenderer != null) meshRenderer.enabled = true;

        yield return new WaitForSeconds(0.15f);

        // 숨기기
        if (meshRenderer != null) meshRenderer.enabled = false;
        aoeIndicator.gameObject.SetActive(false);
        aoeIndicator.ClearMesh();
    }
    #endregion
}