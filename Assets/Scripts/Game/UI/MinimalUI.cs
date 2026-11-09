using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Shared visual language for menus, route selection, rewards and HUD.</summary>
public static class MinimalUI
{
    private static TMP_FontAsset interfaceFont;
    public static TMP_FontAsset Font
    {
        get
        {
            if (interfaceFont != null) return interfaceFont;
            var source = Resources.Load<Font>("UI/InterfaceChinese");
            if (source == null) return TMP_Settings.defaultFontAsset;
            interfaceFont = TMP_FontAsset.CreateFontAsset(source);
            interfaceFont.name = "Running Interface";
            interfaceFont.isMultiAtlasTexturesEnabled = true;
            return interfaceFont;
        }
    }
    public static readonly Color Paper = new Color32(242, 240, 233, 255);
    public static readonly Color Surface = new Color32(250, 249, 245, 255);
    public static readonly Color Ink = new Color32(43, 49, 46, 255);
    public static readonly Color Muted = new Color32(119, 124, 116, 255);
    public static readonly Color Line = new Color32(215, 216, 206, 255);
    public static readonly Color Accent = new Color32(76, 105, 87, 255);

    public static GameObject Canvas(Transform parent, string name, int order)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(parent, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;
        return root;
    }

    public static RectTransform Box(Transform parent, string name, Color color, bool raycast = false)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        var image = root.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        var rect = (RectTransform)root.transform;
        Stretch(rect);
        return rect;
    }

    public static TMP_Text Text(Transform parent, string name, string content, float size = 20,
        Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        var text = root.GetComponent<TextMeshProUGUI>();
        text.font = Font;
        text.text = content;
        text.fontSize = size;
        text.color = color ?? Ink;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        text.fontStyle = FontStyles.Normal;
        Stretch(text.rectTransform);
        return text;
    }

    public static Button Button(Transform parent, string name, string label, UnityAction action,
        bool primary = false)
    {
        var rect = Box(parent, name, primary ? Ink : Surface, true);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.91f, 0.94f, 0.89f);
        colors.pressedColor = new Color(0.78f, 0.84f, 0.77f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1, 1, 1, 0.4f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        button.onClick.AddListener(action);
        Text(rect, "Label", label, 20, primary ? Paper : Ink, TextAlignmentOptions.Center);
        return button;
    }

    public static void Stretch(RectTransform rect, float inset = 0)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = -Vector2.one * inset;
    }

    public static void Place(RectTransform rect, Vector2 min, Vector2 max,
        Vector2? offsetMin = null, Vector2? offsetMax = null)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin ?? Vector2.zero;
        rect.offsetMax = offsetMax ?? Vector2.zero;
    }

    public static void Fixed(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static void Rule(Transform parent, float y)
    {
        var rule = Box(parent, "Rule", Line);
        Place(rule, new Vector2(0.06f, y), new Vector2(0.94f, y));
        rule.sizeDelta = new Vector2(0, 1);
    }
}
