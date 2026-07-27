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
        currentAngle += weaponData.orbitSpeed * Time.deltaTime;
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
            // Mouse.current 참조 삭제 및 기존 Input.mousePosition 사용
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
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