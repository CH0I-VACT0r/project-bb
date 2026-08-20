using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class BossPattern_RandomAoE : BossPatternBase
{
    [Header("Random AoE Settings")]
    public int aoeCount = 4;
    public float spawnRadius = 10f;
    public float chargeTime = 1.5f;
    public float damage = 30f;
    public LayerMask playerLayer;

    [Header("Animation Settings")]
    public string windupAnimatorTriggerName = "Charge_Winding";

    [Header("Shape Config")]
    public AoEShape attackShape = AoEShape.Circle;
    public Vector2 attackSize = new Vector2(3f, 3f);

    [Range(0, 360)] public float sectorAngle = 90f;

    [Header("AoE Visuals")]
    [Tooltip("World Space Canvas가 세팅된 장판 프리팹을 넣으세요")]
    public GameObject aoeIndicatorPrefab;

    private List<GameObject> activeIndicators = new List<GameObject>();

    protected override void OnPatternStart()
    {
        if (!IsServer) return;
        StartCoroutine(RandomAoERoutine());
    }

    private IEnumerator RandomAoERoutine()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        Vector2[] spawnPos = new Vector2[aoeCount];
        float[] spawnAngles = new float[aoeCount];

        Vector2 centerPos = currentTarget != null ? (Vector2)currentTarget.position : (Vector2)transform.position;

        for (int i = 0; i < aoeCount; i++)
        {
            float randX = Mathf.Round(Random.Range(-spawnRadius, spawnRadius) / 5f) * 5f;
            float randY = Mathf.Round(Random.Range(-spawnRadius, spawnRadius) / 5f) * 5f;
            spawnPos[i] = centerPos + new Vector2(randX, randY);

            spawnAngles[i] = UnityEngine.Random.Range(0, 72) * 5f;
        }

        ToggleWarningSpriteClientRpc(true);
        if (!string.IsNullOrEmpty(windupAnimatorTriggerName)) PlayPatternAnimationClientRpc(windupAnimatorTriggerName);

        StartMultiAoEVisualClientRpc(spawnPos, spawnAngles);

        yield return new WaitForSeconds(chargeTime);

        ToggleWarningSpriteClientRpc(false);
        EndMultiAoEVisualClientRpc();

        if (!string.IsNullOrEmpty(animatorTriggerName)) PlayPatternAnimationClientRpc(animatorTriggerName);

        for (int i = 0; i < aoeCount; i++)
        {
            Vector2 attackDir = new Vector2(Mathf.Cos(spawnAngles[i] * Mathf.Deg2Rad), Mathf.Sin(spawnAngles[i] * Mathf.Deg2Rad));
            ExecuteDamage(spawnPos[i], attackDir);
        }

        FinishPattern();
    }

    private void ExecuteDamage(Vector2 origin, Vector2 attackDir)
    {
        HashSet<IDamageable> alreadyHitEnemies = new HashSet<IDamageable>();
        Collider2D[] hits = new Collider2D[0];

        switch (attackShape)
        {
            case AoEShape.Circle:
                hits = Physics2D.OverlapCircleAll(origin, attackSize.x, playerLayer);
                break;
            case AoEShape.Rectangle:
                float boxAngle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;
                hits = Physics2D.OverlapBoxAll(origin, attackSize, boxAngle, playerLayer);
                break;
            case AoEShape.Sector:
                hits = Physics2D.OverlapCircleAll(origin, attackSize.x, playerLayer);
                break;
        }

        SpawnHitVfxClientRpc(origin);

        foreach (var col in hits)
        {
            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null || alreadyHitEnemies.Contains(target)) continue;

            if (attackShape == AoEShape.Sector)
            {
                Vector2 dirToTarget = ((Vector2)col.transform.position - origin).normalized;
                if (Vector2.Angle(attackDir, dirToTarget) > sectorAngle / 2f) continue;
            }

            alreadyHitEnemies.Add(target);
            DamageInfo info = new DamageInfo { damageAmount = this.damage, attackType = AttackAttribute.Magic };
            target.TakeDamage(info);
        }
    }

    [ClientRpc]
    private void StartMultiAoEVisualClientRpc(Vector2[] positions, float[] angles)
    {
        if (aoeIndicatorPrefab == null) return;

        foreach (var ind in activeIndicators) if (ind != null) Destroy(ind);
        activeIndicators.Clear();

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject indObj = Instantiate(aoeIndicatorPrefab, positions[i], Quaternion.identity);
            activeIndicators.Add(indObj);

            Image fillImage = indObj.GetComponentInChildren<Image>();
            if (fillImage != null)
            {
                SetupVisual(fillImage, angles[i]);
                StartCoroutine(FillVisualRoutine(fillImage));
            }
        }
    }

    private void SetupVisual(Image aoeFillImage, float rotationAngle)
    {
        RectTransform rt = aoeFillImage.rectTransform;
        Vector3 canvasScale = aoeFillImage.canvas.transform.localScale;
        Vector2 worldSize = Vector2.zero;

        if (attackShape == AoEShape.Circle || attackShape == AoEShape.Sector)
        {
            worldSize = new Vector2(attackSize.x * 2f, attackSize.x * 2f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
        else if (attackShape == AoEShape.Rectangle)
        {
            worldSize = attackSize;
            rt.pivot = new Vector2(0f, 0.5f);
        }

        rt.sizeDelta = new Vector2(worldSize.x / canvasScale.x, worldSize.y / canvasScale.y);
        rt.localPosition = Vector3.zero;

        if (rt.childCount > 0)
        {
            RectTransform outlineRt = rt.GetChild(0).GetComponent<RectTransform>();
            if (outlineRt != null)
            {
                outlineRt.pivot = rt.pivot;
                outlineRt.anchorMin = Vector2.zero;
                outlineRt.anchorMax = Vector2.one;
                outlineRt.offsetMin = Vector2.zero;
                outlineRt.offsetMax = Vector2.zero;
            }
        }

        aoeFillImage.transform.rotation = Quaternion.Euler(0, 0, rotationAngle);
        aoeFillImage.fillAmount = 0f;
    }

    [ClientRpc]
    private void EndMultiAoEVisualClientRpc()
    {
        foreach (var ind in activeIndicators)
        {
            if (ind != null) Destroy(ind);
        }
        activeIndicators.Clear();
    }

    private IEnumerator FillVisualRoutine(Image aoeFillImage)
    {
        float maxFill = (attackShape == AoEShape.Sector) ? (sectorAngle / 360f) : 1f;
        float elapsed = 0f;

        while (elapsed < chargeTime)
        {
            if (aoeFillImage != null)
            {
                aoeFillImage.fillAmount = (elapsed / chargeTime) * maxFill;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (aoeFillImage != null) aoeFillImage.fillAmount = maxFill;
    }
}