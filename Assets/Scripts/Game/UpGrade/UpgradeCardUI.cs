using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeCardUI : MonoBehaviour
{
    private TMP_Text category;
    private TMP_Text title;
    private TMP_Text description;
    private TMP_Text usage;
    private Button button;
    public void Setup(UpgradeData item,Action<UpgradeData> selected)
    {
        if (button == null) Build();
        string kind = item.Category == ItemCategory.Active ? "主动" : item.Category == ItemCategory.Temporary ? "临时" : "被动";
        category.text = $"{kind}   /   {Rarity(item.Rarity)}";
        title.text = item.UpgradeName;
        description.text = item.Description;
        usage.text = item.Category == ItemCategory.Temporary ? $"持续 {item.TemporaryDuration:0} 秒" :
            item.Category == ItemCategory.Active ? "空格 / 2 使用 · 首领战 Q / E" : "获得后持续生效";
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(()=>selected?.Invoke(item));
    }
    private void Build()
    {
        button = gameObject.AddComponent<Button>();
        button.targetGraphic = GetComponent<Image>();
        var colors = button.colors;
        colors.highlightedColor = new Color(.88f,.92f,.86f); colors.pressedColor = new Color(.8f,.87f,.79f);
        colors.selectedColor = Color.white; colors.fadeDuration = .12f; button.colors = colors;
        var outline = gameObject.AddComponent<Outline>(); outline.effectColor = MinimalUI.Line; outline.effectDistance = new Vector2(1,-1);
        category = MinimalUI.Text(transform,"Category","",14,MinimalUI.Muted);
        MinimalUI.Place(category.rectTransform,new Vector2(.10f,.78f),new Vector2(.9f,.92f));
        title = MinimalUI.Text(transform,"Name","",26);
        MinimalUI.Place(title.rectTransform,new Vector2(.10f,.58f),new Vector2(.9f,.8f));
        description = MinimalUI.Text(transform,"Description","",17,MinimalUI.Muted,TextAlignmentOptions.TopLeft);
        MinimalUI.Place(description.rectTransform,new Vector2(.10f,.22f),new Vector2(.9f,.56f));
        description.enableAutoSizing = true; description.fontSizeMin = 14; description.fontSizeMax = 17;
        usage = MinimalUI.Text(transform,"Usage","",14,MinimalUI.Accent);
        MinimalUI.Place(usage.rectTransform,new Vector2(.10f,.06f),new Vector2(.9f,.2f));
    }
    private static string Rarity(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Epic => "史诗", ItemRarity.Legendary => "传说", ItemRarity.Rare => "稀有",
        ItemRarity.Uncommon => "精良", _ => "普通"
    };
}
