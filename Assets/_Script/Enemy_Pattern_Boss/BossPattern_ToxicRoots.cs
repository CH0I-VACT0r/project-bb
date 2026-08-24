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

    [Tooltip("하늘을 날아가는 궤적을 보여줄 단순 시각용 프리팹")]
    public GameObject sporeFlyingVisualPrefab;
    public float sporeFlightDuration = 1.0f;
    public float sporeArcHeight = 3.0f;

    protected override void OnPatternStart()
    {
        if (!IsServer) return;
        StartCoroutine(ToxicRootsRoutine());
    }

    private IEnumerator ToxicRootsRoutine()
    {
        Vector2 targetPos = currentTarget != null ? (Vector2)currentTarget.position : (Vector2)transform.position + Vector2.right;

        // 1. 포물선 시각 효과를 모든 클라이언트에 재생 지시
        ShootSporeVisualClientRpc(transform.position, targetPos, sporeFlightDuration);

        if (!string.IsNullOrEmpty(animatorTriggerName)) PlayPatternAnimationClientRpc(animatorTriggerName);

        // 2. 날아가는 시간(Flight Duration)만큼 서버도 대기
        yield return new WaitForSeconds(sporeFlightDuration);

        // 3. 도달 시점에 서버에서 실제 장판(NetworkObject) 스폰
        if (sporePrefab != null)
        {
            GameObject spore = Instantiate(sporePrefab, targetPos, Quaternion.identity);
            // ★ 꺾쇠 공백 적용
            NetworkObject netObj = spore.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Spawn();
        }

        // 4. 직사각형 뿌리 찌르기 연속 시전 로직 (복구됨)
        for (int i = 0; i < strikeCount; i++)
        {
            // 타겟 방향 계산
            Vector2 aimTarget = currentTarget != null ? (Vector2)currentTarget.position : (Vector2)transform.position + Vector2.right;
            Vector2 attackDir = (aimTarget - (Vector2)transform.position).normalized;

            // 5단위 각도 강제 정렬
            float rawAngle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;
            float angle = Mathf.Round(rawAngle / 5f) * 5f;
            attackDir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

            // 클라이언트에 찌르기 인디케이터 생성 지시
            ShowStrikeIndicatorClientRpc(transform.position, angle, strikeWindup);

            // 찌르기 선딜레이(Windup) 대기
            yield return new WaitForSeconds(strikeWindup);

            // 대미지 판정 및 타격 VFX 생성
            ExecuteStrikeDamage(attackDir, angle);

            // 다음 찌르기까지 간격 대기
            yield return new WaitForSeconds(strikeInterval);
        }
        FinishPattern();
    }

    [ClientRpc]
    private void ShootSporeVisualClientRpc(Vector2 start, Vector2 end, float duration)
    {
        if (sporeFlyingVisualPrefab == null) return;
        GameObject visual = Instantiate(sporeFlyingVisualPrefab, start, Quaternion.identity);
        StartCoroutine(SporeParabolaRoutine(visual, start, end, duration));
    }

    private IEnumerator SporeParabolaRoutine(GameObject visual, Vector2 start, Vector2 end, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (visual == null) break;
            float t = elapsed / duration;

            Vector2 currentPos = Vector2.Lerp(start, end, t);
            float height = Mathf.Sin(t * Mathf.PI) * sporeArcHeight;

            visual.transform.position = new Vector2(currentPos.x, currentPos.y + height);

            elapsed += Time.deltaTime;
            yield return null;
        }
        if (visual != null) Destroy(visual);
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

        if (indObj != null) Destroy(indObj);
    }
}