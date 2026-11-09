using TMPro;
using UnityEngine;

public sealed class ArenaHUD : MonoBehaviour
{
    private ArenaBootstrap arena;
    private GameObject canvasObject;
    private TMP_Text bossText;
    private TMP_Text healthText;
    private TMP_Text resultText;
    private RectTransform bossFill;
    private ArenaPlayerNetwork localPlayer;
    private float nextPlayerScan;
    private void Awake()
    {
        arena = GetComponent<ArenaBootstrap>();
        canvasObject = MinimalUI.Canvas(transform,"ArenaHUD",30);
        var top = MinimalUI.Box(canvasObject.transform,"TopBar",new Color(.95f,.95f,.91f,.96f));
        MinimalUI.Place(top,new Vector2(.035f,.87f),new Vector2(.965f,.96f));
        healthText = MinimalUI.Text(top,"Health","",18,MinimalUI.Muted);
        MinimalUI.Place(healthText.rectTransform,new Vector2(.025f,0),new Vector2(.3f,1));
        bossText = MinimalUI.Text(top,"Boss","首领",18,MinimalUI.Ink,TextAlignmentOptions.MidlineRight);
        MinimalUI.Place(bossText.rectTransform,new Vector2(.6f,.2f),new Vector2(.975f,1));
        var track = MinimalUI.Box(top,"BossTrack",MinimalUI.Line);
        MinimalUI.Place(track,new Vector2(.40f,.16f),new Vector2(.975f,.19f));
        bossFill = MinimalUI.Box(track,"BossLife",MinimalUI.Accent);
        var hint = MinimalUI.Text(canvasObject.transform,"Hint","WASD  移动     鼠标瞄准     空格  射击     Q / E  道具",16,MinimalUI.Ink,TextAlignmentOptions.Center);
        MinimalUI.Place(hint.rectTransform,new Vector2(.1f,.035f),new Vector2(.9f,.09f));
        resultText = MinimalUI.Text(canvasObject.transform,"Result","",42,MinimalUI.Ink,TextAlignmentOptions.Center);
        MinimalUI.Place(resultText.rectTransform,new Vector2(.1f,.40f),new Vector2(.9f,.62f));
    }
    private void Update()
    {
        if (arena == null) return;
        bossText.text = $"首领   {Mathf.CeilToInt(arena.BossHealth)}";
        bossFill.anchorMax = new Vector2(arena.BossMaxHealth>0 ? Mathf.Clamp01(arena.BossHealth/arena.BossMaxHealth):0,1);
        if ((localPlayer == null || !localPlayer.IsSpawned) && Time.unscaledTime >= nextPlayerScan)
        {
            nextPlayerScan=Time.unscaledTime+.5f;
            foreach (var player in FindObjectsByType<ArenaPlayerNetwork>(FindObjectsSortMode.None))
                if (player.IsSpawned && player.IsOwner && player.IsParticipatingPawn) { localPlayer=player; break; }
        }
        if (localPlayer != null)
            healthText.text = $"生命  {Mathf.CeilToInt(localPlayer.CurrentHealth)}    /    火力  {localPlayer.Firepower}";
        resultText.text = arena.Result == ArenaMatchResult.Victory ? "行程完成" : arena.Result == ArenaMatchResult.Defeat ? "行程结束" : "";
    }
    private void OnDestroy() { if (canvasObject != null) Destroy(canvasObject); }
}
