using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private Color textColor;
    private float disappearTimer;
    private const float DISAPPEAR_TIMER_MAX = 0.5f;

    [Header("Font Size Settings")]
    public float baseFontSize = 3f;
    public float criticalFontSize = 5f;

    void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
    }

    public void Setup(float damageAmount, bool isCritical, bool isMiss)
    {
        if (isMiss)
        {
            textMesh.SetText("Miss");
            textMesh.color = Color.gray;
            textMesh.fontSize = baseFontSize;
        }
        else
        {
            textMesh.SetText(damageAmount.ToString("0"));

            if (isCritical)
            {
                textMesh.color = Color.yellow;
                textMesh.fontSize = criticalFontSize;
            }
            else
            {
                textMesh.color = Color.black;
                textMesh.fontSize = baseFontSize;
            }
        }

        transform.localScale = Vector3.one;
        textColor = textMesh.color;
        disappearTimer = DISAPPEAR_TIMER_MAX;
    }

    void Update()
    {
        transform.position += new Vector3(0, 0.5f, 0) * Time.deltaTime;

        disappearTimer -= Time.deltaTime;
        if (disappearTimer < 0)
        {
            textColor.a -= 3f * Time.deltaTime;
            textMesh.color = textColor;

            if (textColor.a < 0)
            {
                DamagePopupManager.Instance.ReturnPopup(this.gameObject);
            }
        }
    }
}