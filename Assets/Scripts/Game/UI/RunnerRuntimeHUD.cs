using TMPro;
using UnityEngine;

/// <summary>One quiet HUD for a single runner; reads replicated state only.</summary>
public sealed class RunnerRuntimeHUD : MonoBehaviour
{
    private RunnerController runner;
    private CrowdManager stats;
    private GameObject canvasObject;
    private RectTransform panel;
    private TMP_Text phase;
    private TMP_Text health;
    private TMP_Text power;
    private TMP_Text hint;
    public static RunnerRuntimeHUD Ensure(RunnerController runner,CrowdManager stats,ItemManager items)
    {
        var hud = runner.GetComponent<RunnerRuntimeHUD>();
        if (hud == null) hud = runner.gameObject.AddComponent<RunnerRuntimeHUD>();
        hud.runner = runner; hud.stats = stats; hud.Build();
        return hud;
    }
    private void Build()
    {
        if (canvasObject != null) return;
        canvasObject = MinimalUI.Canvas(transform,"RunnerHUD",30);
        panel = MinimalUI.Box(canvasObject.transform,"HUD",Color.clear);
        var bar = MinimalUI.Box(panel,"StatusBar",new Color(.95f,.95f,.91f,.96f));
        MinimalUI.Place(bar,new Vector2(.035f,.88f),new Vector2(.965f,.96f));
        phase = MinimalUI.Text(bar,"Phase","前行",18);
        MinimalUI.Place(phase.rectTransform,new Vector2(.025f,0),new Vector2(.3f,1));
        health = MinimalUI.Text(bar,"Health","",18,MinimalUI.Muted,TextAlignmentOptions.Center);
        MinimalUI.Place(health.rectTransform,new Vector2(.4f,0),new Vector2(.62f,1));
        power = MinimalUI.Text(bar,"Power","",18,MinimalUI.Accent,TextAlignmentOptions.MidlineRight);
        MinimalUI.Place(power.rectTransform,new Vector2(.65f,0),new Vector2(.975f,1));
        hint = MinimalUI.Text(panel,"Hint","A / D  移动     空格 / 2  主动道具",16,MinimalUI.Ink,TextAlignmentOptions.Center);
        MinimalUI.Place(hint.rectTransform,new Vector2(.15f,.035f),new Vector2(.85f,.095f));
    }
    private void Update()
    {
        if (runner == null || panel == null) return;
        RunState state = GameManager.instance != null ? GameManager.instance.state : RunState.Lobby;
        panel.gameObject.SetActive(runner.IsOwner && (state == RunState.Running || state == RunState.Combat || state == RunState.GameOver));
        if (!panel.gameObject.activeSelf) return;
        health.text = $"生命  {stats.Health} / {stats.MaxHealth}";
        power.text = $"火力  {stats.Firepower}";
        phase.text = runner.Phase switch
        {
            PlayerRunPhase.Combat => "战斗", PlayerRunPhase.WaitingAtExit => "已抵达 · 等待队友",
            PlayerRunPhase.Eliminated => "本次行程结束", _ => "前行"
        };
        hint.text = runner.Phase == PlayerRunPhase.Combat ? "A / D  闪避     自动射击     空格 / 2  主动道具" :
            "A / D  移动     选择门，提升火力";
    }
    private void OnDestroy() { if (canvasObject != null) Destroy(canvasObject); }
}
