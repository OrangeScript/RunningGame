using System;
using TMPro;
using UnityEngine;

public class UpgradeSelectionUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private UpgradeCardUI[] cards;
    [SerializeField] private TMP_Text headerText;
    private GameObject canvasObject;
    private bool isOpen;
    private Action<UpgradeData> onSelected;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
        canvasObject = MinimalUI.Canvas(null,"RewardCanvas",110);
        panel = MinimalUI.Box(canvasObject.transform,"RewardPanel",MinimalUI.Paper,true).gameObject;
        var eyebrow = MinimalUI.Text(panel.transform,"Eyebrow","行 程 收 获",14,MinimalUI.Muted,TextAlignmentOptions.Center);
        MinimalUI.Place(eyebrow.rectTransform,new Vector2(.1f,.79f),new Vector2(.9f,.84f));
        headerText = MinimalUI.Text(panel.transform,"Title","选择一件道具",34,null,TextAlignmentOptions.Center);
        MinimalUI.Place(headerText.rectTransform,new Vector2(.1f,.70f),new Vector2(.9f,.79f));
        var note = MinimalUI.Text(panel.transform,"Subtitle","选择后继续行程",16,MinimalUI.Muted,TextAlignmentOptions.Center);
        MinimalUI.Place(note.rectTransform,new Vector2(.1f,.64f),new Vector2(.9f,.7f));
        var row = new GameObject("Choices",typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(panel.transform,false);
        MinimalUI.Place(row,new Vector2(.16f,.23f),new Vector2(.84f,.59f));
        cards = new UpgradeCardUI[3];
        for (int i=0;i<3;i++)
        {
            var rect = MinimalUI.Box(row,$"Choice_{i}",MinimalUI.Surface,true);
            MinimalUI.Place(rect,new Vector2(i/3f+.012f,0),new Vector2((i+1)/3f-.012f,1));
            cards[i] = rect.gameObject.AddComponent<UpgradeCardUI>();
        }
        panel.SetActive(false);
    }
    public void Show(UpgradeData[] options,ItemAcquisitionSource source,Action<UpgradeData> callback)
    {
        if (options == null || options.Length == 0) { callback?.Invoke(null); return; }
        isOpen = true; onSelected = callback;
        panel.SetActive(true);
        headerText.text = source == ItemAcquisitionSource.TreasureChest ? "宝藏 · 三选一" :
            source == ItemAcquisitionSource.Event ? "未知的馈赠" : "选择一件道具";
        for (int i=0;i<cards.Length;i++)
        {
            bool visible = i<options.Length && options[i]!=null;
            cards[i].gameObject.SetActive(visible);
            if (visible) cards[i].Setup(options[i],Select);
        }
    }
    private void Select(UpgradeData selected)
    {
        if (!isOpen) return;
        isOpen = false; panel.SetActive(false);
        var callback = onSelected; onSelected = null;
        callback?.Invoke(selected);
    }
    private void OnDestroy() { if (canvasObject != null) Destroy(canvasObject); }
}
