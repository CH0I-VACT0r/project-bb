using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponControllerNetcode : NetworkBehaviour
{
    public PlayerStatManager playerStats;
    public Transform playerTransform;
    public WeaponDataSO weaponData; // SO 데이터 참조
    public LayerMask enemyLayer;

    [Header("Visuals")]
    public SpriteRenderer aoeIndicator;

    [Header("Rendering")]
    public float orbitYMultiplier = 0.5f; // 타원의 납작한 정도 (1 = 정원, 0.5 = 절반 납작함)
    private SpriteRenderer weaponSprite;
    private SpriteRenderer playerSprite;

    private ContactFilter2D enemyFilter;
    private Collider2D[] hitBuffer = new Collider2D[64];

    [Header("Orbit Damage Settings")]
    public float orbitDamageTickRate = 0.5f; // 타격 주기 (0.5초마다 타격)
    public float orbitDamageRadius = 0.5f; // 무기 자체의 타격 판정 크기
    public float orbitDamageMultiplier = 0.5f; // 공전 데미지 배율 (기본 데미지의 50%)

    private float orbitDamageTimer = 0f;

    [Header("Dynamic Orbit Settings")]
    public float minOrbitSpeed = 90f; // 최소 공전 속도
    public float maxOrbitSpeed = 180f; // 최대 공전 속도

    private float targetOrbitSpeed;
    private float currentOrbitSpeed;
    private float previousAngle = 0f;

    private enum WeaponState { Orbiting, Attacking, Returning }
    private WeaponState currentState = WeaponState.Orbiting;

    private float currentAngle = 0f;
    private float currentCooldown = 0f;
    private Vector3 targetDirection;
    private Vector3 targetPosition;

    void Awake()
    {
        enemyFilter = new ContactFilter2D();
        enemyFilter.useLayerMask = true;
        enemyFilter.layerMask = enemyLayer;
        enemyFilter.useTriggers = true;

        weaponSprite = GetComponent<SpriteRenderer>();

        // 시작할 때 시각 효과 숨김
        if (aoeIndicator != null) aoeIndicator.enabled = false;
    }

    void Start()
    {
        // 부모인 플레이어 객체에서 SpriteRenderer를 찾아옵니다.
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

    private void UpdateOrbitAngle()
    {
        // 목표 속도를 향해 부드럽게 가감속 보간
        currentOrbitSpeed = Mathf.Lerp(currentOrbitSpeed, targetOrbitSpeed, Time.deltaTime * 3f);

        previousAngle = currentAngle;
        currentAngle += currentOrbitSpeed * Time.deltaTime;

        // 각도가 90도를 돌파하는 순간 (가장 높은 Y축 = 플레이어 등 뒤)
        if (previousAngle < 90f && currentAngle >= 90f)
        {
            // 5의 배수 단위로 무작위 목표 속도 산출
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

    private void PerformOrbitDamageServer()
    {
        int hitCount = Physics2D.OverlapCircle(transform.position, orbitDamageRadius, enemyFilter, hitBuffer);

        // 밸런스를 위해 공전 피해량은 배율(Multiplier)을 적용
        float finalDamage = (playerStats.AttackDamage.Value + weaponData.baseDamage) * orbitDamageMultiplier;

        for (int i = 0; i < hitCount; i++)
        {
            IDamageable damageableTarget = hitBuffer[i].GetComponent<IDamageable>();
            damageableTarget?.TakeDamage(finalDamage);
        }
    }

    private void UpdateSortingOrder()
    {
        if (weaponSprite == null || playerSprite == null) return;

        // 무기의 Y 좌표가 플레이어보다 크면 화면상 더 위쪽(뒤쪽)에 있다는 의미
        if (transform.position.y > playerTransform.position.y)
        {
            weaponSprite.sortingOrder = playerSprite.sortingOrder - 1; // 플레이어 뒤로
        }
        else
        {
            weaponSprite.sortingOrder = playerSprite.sortingOrder + 1; // 플레이어 앞으로
        }
    }

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
            // 변경점: attackRange 대신 travelDistance를 사용하여 타겟 위치 계산
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

    private IEnumerator ShowAoEVisual()
    {
        if (aoeIndicator == null) yield break;

        // 크기를 타격 반경(attackRange)에 맞게 조절 (지름 = 반지름 * 2)
        float diameter = weaponData.attackRange * 2f;
        aoeIndicator.transform.localScale = new Vector3(diameter, diameter, 1f);

        aoeIndicator.enabled = true;
        yield return new WaitForSeconds(0.15f); // 0.15초 동안 표시
        aoeIndicator.enabled = false;
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

    // 서버 전용: 근접 무기 피해 연산
    private void PerformMeleeDamageServer()
    {
        int hitCount = Physics2D.OverlapCircle(transform.position, weaponData.attackRange, enemyFilter, hitBuffer);
        float finalDamage = playerStats.AttackDamage.Value + weaponData.baseDamage;

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

    // 서버 전용: 투사체 생성
    private void SpawnProjectileServer(Vector3 direction)
    {
        GameObject projObj = Instantiate(weaponData.projectilePrefab, transform.position, Quaternion.identity);

        // 투사체 컴포넌트 초기화
        ProjectileNetcode proj = projObj.GetComponent<ProjectileNetcode>();
        float finalDamage = playerStats.AttackDamage.Value + weaponData.baseDamage;
        proj.Initialize(direction, weaponData.projectileSpeed, finalDamage);

        // 네트워크 객체 스폰 (모든 클라이언트에 동기화)
        projObj.GetComponent<NetworkObject>().Spawn();
    }
}