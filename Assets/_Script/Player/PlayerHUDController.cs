using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUDController : MonoBehaviour
{
    public static PlayerHUDController Instance;

    [Header("UI Components")]
    public Slider hpSlider;
    public TextMeshProUGUI hpText;

    public Slider shieldSlider;
    public TextMeshProUGUI shieldText;

    private PlayerStatManager localPlayerStats;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 스폰된 플레이어가 로컬 플레이어일 때 호출되어 UI 이벤트를 바인딩함
    public void BindLocalPlayer(PlayerStatManager statManager)
    {
        localPlayerStats = statManager;

        // 이벤트 구독 (값 변경 시 UI 자동 갱신)
        localPlayerStats.CurrentHealth.OnValueChanged += UpdateHPUI;
        localPlayerStats.MaxHealth.OnValueChanged += UpdateHPUI;

        localPlayerStats.CurrentShield.OnValueChanged += UpdateShieldUI;
        localPlayerStats.MaxShield.OnValueChanged += UpdateShieldUI;

        // 바인딩 직후 초기 UI 갱신
        UpdateHPUI(0, localPlayerStats.CurrentHealth.Value);
        UpdateShieldUI(0, localPlayerStats.CurrentShield.Value);
    }

    private void UpdateHPUI(float previousValue, float newValue)
    {
        if (localPlayerStats == null) return;

        float current = localPlayerStats.CurrentHealth.Value;
        float max = localPlayerStats.MaxHealth.Value;

        if (hpSlider != null) hpSlider.value = current / max;
        if (hpText != null) hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void UpdateShieldUI(float previousValue, float newValue)
    {
        if (localPlayerStats == null) return;

        float current = localPlayerStats.CurrentShield.Value;
        float max = localPlayerStats.MaxShield.Value;

        // 쉴드가 0보다 클 때만 UI 출력 처리 가능
        if (shieldSlider != null)
        {
            shieldSlider.gameObject.SetActive(max > 0);
            shieldSlider.value = max > 0 ? current / max : 0;
        }

        if (shieldText != null)
        {
            shieldText.gameObject.SetActive(max > 0);
            shieldText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }

    private void OnDestroy()
    {
        // 메모리 누수 방지를 위한 이벤트 해제
        if (localPlayerStats != null)
        {
            localPlayerStats.CurrentHealth.OnValueChanged -= UpdateHPUI;
            localPlayerStats.MaxHealth.OnValueChanged -= UpdateHPUI;
            localPlayerStats.CurrentShield.OnValueChanged -= UpdateShieldUI;
            localPlayerStats.MaxShield.OnValueChanged -= UpdateShieldUI;
        }
    }
}
