using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI; // Image 컴포넌트를 사용하기 위해 필수

public enum AoEShape { Circle, Rectangle, Sector }

public class Pattern_ChargeAoE : BossPatternBase
{
    [Header("Charge Settings")]
    public float chargeTime = 1.5f;
    public float damage = 30f;
    public LayerMask playerLayer;

    [Header("Animation Settings")]
    public string windupAnimatorTriggerName = "Charge_Winding";

    [Header("Shape Config (타격 판정 범위)")]
    public AoEShape attackShape = AoEShape.Circle;

    [Tooltip("원형/부채꼴: X값(반지름)만 사용 / 직사각형: X(가로), Y(세로) 모두 사용")]
    public Vector2 attackSize = new Vector2(3f, 3f);

    [Range(0, 360)] public float sectorAngle = 90f;

    [Header("AoE Visuals (장판 시각 효과)")]
    [Tooltip("World Space Canvas 하위의 UI Image 객체")]
    public Image aoeFillImage;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent <Rigidbody2D>();
        if (aoeFillImage != null)
        {
            aoeFillImage.gameObject.SetActive(false);
        }
    }

    protected override void OnPatternStart()
    {
        if (!IsServer) return;
        StartCoroutine(ChargeAoERoutine());
    }

    private IEnumerator ChargeAoERoutine()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 공격 기준 방향 설정 (플레이어 방향)
        Vector2 attackDir = Vector2.right;
        if (currentTarget != null)
        {
            attackDir = (currentTarget.position - transform.position).normalized;
        }

        // 머리 위 느낌표 및 바닥 장판 채우기 시작
        ToggleWarningSpriteClientRpc(true);

        float angle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;
        StartAoEVisualClientRpc(angle);

        if (!string.IsNullOrEmpty(windupAnimatorTriggerName))
        {
            PlayPatternAnimationClientRpc(windupAnimatorTriggerName);
        }

        yield return new WaitForSeconds(chargeTime); //  기 모으기 대기

        ToggleWarningSpriteClientRpc(false); // 이펙트 끄기 및 공격 애니메이션
        EndAoEVisualClientRpc();

        if (!string.IsNullOrEmpty(animatorTriggerName))
        {
            PlayPatternAnimationClientRpc(animatorTriggerName);
        }
        // 서버에서 실제 물리 타격 판정 실행
        ExecuteDamage(attackDir);

        FinishPattern();
    }

    private void ExecuteDamage(Vector2 attackDir)
    {
        // 다단 히트 방지용 HashSet
        HashSet<IDamageable> alreadyHitEnemies = new HashSet<IDamageable>();
        Collider2D[] hits = new Collider2D[0];

        // 설정된 형태에 따라 Overlap 함수 분기
        switch (attackShape)
        {
            case AoEShape.Circle:
                hits = Physics2D.OverlapCircleAll(transform.position, attackSize.x, playerLayer);
                break;
            case AoEShape.Rectangle:
                float boxAngle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;
                hits = Physics2D.OverlapBoxAll(transform.position, attackSize, boxAngle, playerLayer);
                break;
            case AoEShape.Sector:
                hits = Physics2D.OverlapCircleAll(transform.position, attackSize.x, playerLayer);
                break;
        }

        foreach (var col in hits)
        {
            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null || alreadyHitEnemies.Contains(target)) continue;

            // 부채꼴일 경우, 공격 방향과 타겟 방향의 사이 각도가 sectorAngle의 절반 이내인지 검사
            if (attackShape == AoEShape.Sector)
            {
                Vector2 dirToTarget = (col.transform.position - transform.position).normalized;
                if (Vector2.Angle(attackDir, dirToTarget) > sectorAngle / 2f) continue;
            }

            alreadyHitEnemies.Add(target);
            DamageInfo info = new DamageInfo { damageAmount = this.damage, attackType = AttackAttribute.Magic };
            target.TakeDamage(info);
        }
    }

    // --- 장판 채우기 동기화 (클라이언트 렌더링) ---
    [ClientRpc]
    private void StartAoEVisualClientRpc(float rotationAngle)
    {
        if (aoeFillImage != null)
        {
            RectTransform rt = aoeFillImage.rectTransform;
            Vector3 canvasScale = aoeFillImage.canvas.transform.localScale;
            Vector2 worldSize = Vector2.zero;

            // 모양에 따른 피벗 자동 조절
            if (attackShape == AoEShape.Circle || attackShape == AoEShape.Sector)
            {
                worldSize = new Vector2(attackSize.x * 2f, attackSize.x * 2f);
                rt.pivot = new Vector2(0.5f, 0.5f); // 원형/부채꼴: 몬스터가 정중앙
            }
            else if (attackShape == AoEShape.Rectangle)
            {
                worldSize = attackSize;
                rt.pivot = new Vector2(0f, 0.5f); // 직사각형: 몬스터 발밑(좌측)에서 앞으로 뻗어나감
            }

            rt.sizeDelta = new Vector2(worldSize.x / canvasScale.x, worldSize.y / canvasScale.y);
            rt.localPosition = Vector3.zero; // 위치 어긋남 방지

            // 자식 객체 동기화 
            if (rt.childCount > 0)
            {
                RectTransform outlineRt = rt.GetChild(0).GetComponent<RectTransform>();
                if (outlineRt != null)
                {
                    outlineRt.pivot = rt.pivot;
                    outlineRt.anchorMin = Vector2.zero;
                    outlineRt.anchorMax = Vector2.one;
                    outlineRt.offsetMin = Vector2.zero; // Left, Bottom = 0
                    outlineRt.offsetMax = Vector2.zero; // Right, Top = 0
                }
            }

            float maxFill = (attackShape == AoEShape.Sector) ? (sectorAngle / 360f) : 1f;

            aoeFillImage.gameObject.SetActive(true);
            aoeFillImage.transform.rotation = Quaternion.Euler(0, 0, rotationAngle);
            aoeFillImage.fillAmount = 0f;

            StartCoroutine(FillVisualRoutine(maxFill));
        }
    }

    [ClientRpc]
    private void EndAoEVisualClientRpc()
    {
        if (aoeFillImage != null)
        {
            aoeFillImage.gameObject.SetActive(false);
        }
    }

    private IEnumerator FillVisualRoutine(float maxFill)
    {
        float elapsed = 0f;
        while (elapsed < chargeTime)
        {
            if (aoeFillImage != null)
            {
                // 시간에 비례하여 0에서 maxFill 까지만 차오름
                aoeFillImage.fillAmount = (elapsed / chargeTime) * maxFill;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 오차 보정을 위해 타이머 종료 시 정확한 최대치 강제 할당
        if (aoeFillImage != null)
        {
            aoeFillImage.fillAmount = maxFill;
        }
    }
}