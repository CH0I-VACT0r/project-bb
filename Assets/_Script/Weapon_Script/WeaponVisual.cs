using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class WeaponVisual : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        // 컴포넌트 참조 캐싱
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(WeaponDataSO weaponData)
    {
        if (weaponData == null)
        {
            Debug.LogError("WeaponDataSO가 할당되지 않았습니다.");
            return;
        }

        if (weaponData.weaponSprite != null)
        {
            // SO에 등록된 이미지를 실제 렌더러에 적용
            spriteRenderer.sprite = weaponData.weaponSprite;
        }
        else
        {
            Debug.LogWarning($"{weaponData.weaponName} 데이터에 무기 이미지가 누락되어 있습니다.");
        }
    }
}
