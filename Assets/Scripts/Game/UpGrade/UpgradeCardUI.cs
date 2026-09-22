
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeCardUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField]private TMP_Text nameText;
    [SerializeField]private TMP_Text descriptionText;
    [SerializeField]private Image iconImage;
    [SerializeField]private Button button;

    private UpgradeData currentUpgrade;
    private Action<UpgradeData> onSelected;
    public void Setup(UpgradeData upgrade,Action<UpgradeData> action)
    {
        currentUpgrade = upgrade;
        onSelected = action;
        nameText.text = upgrade.UpgradeName;
        descriptionText.text = upgrade.Description;

        iconImage.sprite = upgrade.Icon;
        iconImage.gameObject.SetActive(true);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HandleClick);
    }

    void HandleClick()
    {
        if(currentUpgrade == null)  return;
        onSelected?.Invoke(currentUpgrade);
    }
    
}
