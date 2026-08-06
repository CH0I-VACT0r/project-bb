using UnityEngine;
using UnityEngine.UIElements;

public class BarbarianIdentityUI
{
    private ProgressBar brainCellBar;
    private ProgressBar awakeningBar;
    private BarbarianIdentityNetcode barbarianNetcode;

    public void Bind(VisualElement identityRoot, BarbarianIdentityNetcode netcode)
    {
        barbarianNetcode = netcode;

        // UXML에서 바바리안 게이지 2개의 이름(ID)으로 요소 찾기
        brainCellBar = identityRoot.Q<ProgressBar>("braincell-bar");
        awakeningBar = identityRoot.Q<ProgressBar>("awakening-bar");

        // 네트워크 변수 구독
        netcode.CurrentBrainCells.OnValueChanged += UpdateBrainCellUI;
        netcode.MaxBrainCells.OnValueChanged += UpdateBrainCellUI;

        netcode.CurrentAwakeningProgress.OnValueChanged += UpdateAwakeningUI;
        netcode.RequiredAwakeningTime.OnValueChanged += UpdateAwakeningUI;
    }

    private void UpdateBrainCellUI(float previous, float current)
    {
        if (brainCellBar != null && barbarianNetcode != null)
        {
            brainCellBar.value = barbarianNetcode.CurrentBrainCells.Value;
            brainCellBar.highValue = barbarianNetcode.MaxBrainCells.Value;
            brainCellBar.title = $"{Mathf.CeilToInt(barbarianNetcode.CurrentBrainCells.Value)} / {Mathf.CeilToInt(barbarianNetcode.MaxBrainCells.Value)}";
        }
    }

    private void UpdateAwakeningUI(float previous, float current)
    {
        if (awakeningBar != null && barbarianNetcode != null)
        {
            awakeningBar.value = barbarianNetcode.CurrentAwakeningProgress.Value;
            awakeningBar.highValue = barbarianNetcode.RequiredAwakeningTime.Value;

            if (barbarianNetcode.IsAwakened.Value)
                awakeningBar.title = "AWAKENED!";
            else
                awakeningBar.title = $"{barbarianNetcode.CurrentAwakeningProgress.Value:F1}s / {barbarianNetcode.RequiredAwakeningTime.Value:F1}s";
        }
    }

    public void Unbind()
    {
        if (barbarianNetcode != null)
        {
            barbarianNetcode.CurrentBrainCells.OnValueChanged -= UpdateBrainCellUI;
            barbarianNetcode.MaxBrainCells.OnValueChanged -= UpdateBrainCellUI;
            barbarianNetcode.CurrentAwakeningProgress.OnValueChanged -= UpdateAwakeningUI;
            barbarianNetcode.RequiredAwakeningTime.OnValueChanged -= UpdateAwakeningUI;
        }
    }
}