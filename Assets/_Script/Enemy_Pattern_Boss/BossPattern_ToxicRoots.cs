using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class BossPattern_ToxicRoots : BossPatternBase
{
    [Header("Spore Settings (둔화 장판)")]
    [Tooltip("상태 이상을 유발하는 별도의 장판 프리팹 (NetworkObject 필수)")]
    public GameObject sporePrefab;
    public float sporeDuration = 5f;

    [Header("Root Strike Settings (뿌리 찌르기)")]
    public int strikeCount = 2;
    public float strikeWindup = 0.25f;  // 시전 딜레이 (0.25초)
    public float strikeInterval = 0.2f; // 타격 간 간격 (0.2초)
    public float damage = 40f;
    public Vector2 strikeSize = new Vector2(5f, 1f); // 직사각형 크기 (X가 사거리)
    public LayerMask playerLayer;

    [Header("Visuals")]
    public GameObject aoeIndicatorPrefab;
    public string attackAnimatorTrigger = "Attack_Stab";

    protected override void OnPatternStart()
    {
        if (!IsServer) return;
        StartCoroutine(ToxicRootsRoutine());
    }

    private IEnumerator ToxicRootsRoutine()
    {
        // 플레이어 현재 위치에 독립된 둔화 장판 스폰
        if (currentTarget != null && sporePrefab != null)
        {
            GameObject spore = Instantiate(sporePrefab, currentTarget.position, Quaternion.identity);
            NetworkObject netObj = spore.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                // 장판 삭제는 spore 내부 스크립트에서 netObj.Despawn()을 호출하는 것이 정석입니다.
            }
        }

        // 직사각형 뿌리 찌르기 연속 시전
        for (int i = 0; i < strikeCount; i++)
        {
            // 매 타격마다 플레이어의 최신 위치를 추적하여 각도 갱신
            Vector2 targetPos = currentTarget != null ? (Vector2)currentTarget.position : (Vector2)transform.position + Vector2.right;
            Vector2 attackDir = (targetPos - (Vector2)transform.position).normalized;
            float rawAngle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;
            float angle = Mathf.Round(rawAngle / 5f) * 5f;
            attackDir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            // 클라이언트에게 인디케이터 표시 지시
            ShowStrikeIndicatorClientRpc(transform.position, angle, strikeWindup);

            if (!string.IsNullOrEmpty(attackAnimatorTrigger))
            {
                PlayPatternAnimationClientRpc(attackAnimatorTrigger);
            }

            // 0.25초 대기 (Windup)
            yield return new WaitForSeconds(strikeWindup);

            // 대미지 판정
            ExecuteStrikeDamage(attackDir, angle);

            // 다음 타격까지 대기 (0.2초 간격)
            yield return new WaitForSeconds(strikeInterval);
        }

        FinishPattern();
    }

    private void ExecuteStrikeDamage(Vector2 attackDir, float angle)
    {
        Vector2 boxCenter = (Vector2)transform.position + (attackDir * (strikeSize.x * 0.5f));
        Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, strikeSize, angle, playerLayer);

        SpawnHitVfxClientRpc(boxCenter, angle);

        foreach (var col in hits)
        {
            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                DamageInfo info = new DamageInfo { damageAmount = this.damage, attackType = AttackAttribute.Physical };
                target.TakeDamage(info);
            }
        }
    }

    [ClientRpc]
    private void ShowStrikeIndicatorClientRpc(Vector2 origin, float angle, float duration)
    {
        if (aoeIndicatorPrefab == null) return;

        // 보스 위치에서 지정된 각도로 인디케이터 즉시 생성
        GameObject indObj = Instantiate(aoeIndicatorPrefab, origin, Quaternion.Euler(0, 0, angle));

        Image fillImage = indObj.GetComponentInChildren<Image>();
        if (fillImage != null)
        {
            RectTransform rt = fillImage.rectTransform;
            Vector3 canvasScale = fillImage.canvas.transform.localScale;

            // 피벗을 좌측 중앙(0, 0.5)으로 설정하여 보스 발밑에서부터 앞으로 뻗어나가도록 고정
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(strikeSize.x / canvasScale.x, strikeSize.y / canvasScale.y);
            rt.localPosition = Vector3.zero;

            fillImage.fillAmount = 0f;
            StartCoroutine(FillAndDestroyRoutine(indObj, fillImage, duration));
        }
        else
        {
            Destroy(indObj, duration);
        }
    }

    private IEnumerator FillAndDestroyRoutine(GameObject indObj, Image fillImage, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = elapsed / duration;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 게이지가 다 차면(0.25초) 인디케이터 즉시 삭제
        if (indObj != null) Destroy(indObj);
    }
}