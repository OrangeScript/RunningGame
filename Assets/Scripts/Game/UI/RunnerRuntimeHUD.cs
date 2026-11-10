using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    private TMP_Text inventoryCount,rewardTitle,rewardDescription;
    private RectTransform rewardPanel,progressFill;
    private readonly Image[] inventoryIcons=new Image[8];
    private Image rewardIcon;
    private ItemManager items;
    private float rewardUntil;
    private SectionManager sectionManager;
    private GameObject endActions;
    private TMP_Text endTitle;
    private Button retry;
    public static RunnerRuntimeHUD Ensure(RunnerController runner,CrowdManager stats,ItemManager items)
    {
        var hud = runner.GetComponent<RunnerRuntimeHUD>();
        if (hud == null) hud = runner.gameObject.AddComponent<RunnerRuntimeHUD>();
        hud.runner = runner; hud.stats = stats;
        if(hud.items!=items)
        {
            if(hud.items!=null)hud.items.InventoryChanged-=hud.RefreshInventory;
            hud.items=items;if(items!=null)items.InventoryChanged+=hud.RefreshInventory;
        }
        hud.Build();hud.RefreshInventory();
        return hud;
    }
    private void Build()
    {
        if (canvasObject != null) return;
        canvasObject = MinimalUI.Canvas(transform,"RunnerHUD",30);
        panel = MinimalUI.Box(canvasObject.transform,"HUD",Color.clear);
        var bar = MinimalUI.Box(panel,"StatusBar",new Color(.95f,.95f,.91f,.96f));
        MinimalUI.Place(bar,new Vector2(.035f,.915f),new Vector2(.965f,.972f));
        phase = MinimalUI.Text(bar,"Phase","前行",18);
        MinimalUI.Place(phase.rectTransform,new Vector2(.025f,0),new Vector2(.3f,1));
        health = MinimalUI.Text(bar,"Health","",18,MinimalUI.Muted,TextAlignmentOptions.Center);
        MinimalUI.Place(health.rectTransform,new Vector2(.4f,0),new Vector2(.62f,1));
        power = MinimalUI.Text(bar,"Power","",18,MinimalUI.Accent,TextAlignmentOptions.MidlineRight);
        MinimalUI.Place(power.rectTransform,new Vector2(.65f,0),new Vector2(.975f,1));
        hint = MinimalUI.Text(panel,"Hint","A / D  移动     空格 / 2  主动道具",16,MinimalUI.Ink,TextAlignmentOptions.Center);
        MinimalUI.Place(hint.rectTransform,new Vector2(.15f,.035f),new Vector2(.85f,.095f));
        var track=MinimalUI.Box(bar,"RoadProgress",MinimalUI.Line);
        MinimalUI.Place(track,new Vector2(.02f,.02f),new Vector2(.98f,.045f));
        progressFill=MinimalUI.Box(track,"Fill",MinimalUI.Accent);
        inventoryCount=MinimalUI.Text(panel,"PassiveCount","",14,MinimalUI.Muted);
        MinimalUI.Place(inventoryCount.rectTransform,new Vector2(.04f,.13f),new Vector2(.16f,.18f));
        for(int i=0;i<inventoryIcons.Length;i++)
        {
            var icon=MinimalUI.Box(panel,"Passive_"+i,MinimalUI.Accent);
            MinimalUI.Place(icon,new Vector2(.15f+i*.035f,.135f),new Vector2(.175f+i*.035f,.18f));
            inventoryIcons[i]=icon.GetComponent<Image>();inventoryIcons[i].preserveAspect=true;inventoryIcons[i].enabled=false;
        }
        rewardPanel=MinimalUI.Box(panel,"Acquired",new Color(.98f,.98f,.94f,.95f));
        MinimalUI.Place(rewardPanel,new Vector2(.04f,.74f),new Vector2(.37f,.86f));
        var rewardImage=MinimalUI.Box(rewardPanel,"Icon",MinimalUI.Accent);
        MinimalUI.Place(rewardImage,new Vector2(.035f,.25f),new Vector2(.16f,.83f));
        rewardIcon=rewardImage.GetComponent<Image>();rewardIcon.preserveAspect=true;
        rewardTitle=MinimalUI.Text(rewardPanel,"Name","",20);
        MinimalUI.Place(rewardTitle.rectTransform,new Vector2(.20f,.53f),new Vector2(.97f,.92f));
        rewardDescription=MinimalUI.Text(rewardPanel,"Effect","",14,MinimalUI.Muted);
        MinimalUI.Place(rewardDescription.rectTransform,new Vector2(.20f,.06f),new Vector2(.97f,.52f));
        rewardPanel.gameObject.SetActive(false);
        endActions=new GameObject("RunEndActions",typeof(RectTransform));endActions.transform.SetParent(panel,false);
        MinimalUI.Place((RectTransform)endActions.transform,new Vector2(.32f,.37f),new Vector2(.68f,.45f));
        retry=MinimalUI.Button(endActions.transform,"RetryRun","重新出发",()=>FindFirstObjectByType<RoomSession>()?.RetryRunning(),true);
        MinimalUI.Place((RectTransform)retry.transform,Vector2.zero,new Vector2(.48f,1));
        var back=MinimalUI.Button(endActions.transform,"BackToMenu","返回菜单",()=>FindFirstObjectByType<RoomSession>()?.LeaveRoom());
        MinimalUI.Place((RectTransform)back.transform,new Vector2(.52f,0),Vector2.one);endActions.SetActive(false);
        endTitle=MinimalUI.Text(panel,"RunEndTitle","行程结束",36,MinimalUI.Ink,TextAlignmentOptions.Center);
        MinimalUI.Place(endTitle.rectTransform,new Vector2(.2f,.48f),new Vector2(.8f,.60f));endTitle.gameObject.SetActive(false);
        RunLootDirector.LocalReward+=HandleReward;
    }
    private void Update()
    {
        if (runner == null || panel == null) return;
        RunState state = GameManager.instance != null ? GameManager.instance.state : RunState.Lobby;
        panel.gameObject.SetActive(runner.IsOwner && (state == RunState.Running || state == RunState.Combat || state == RunState.GameOver));
        if (!panel.gameObject.activeSelf) return;
        endActions.SetActive(state==RunState.GameOver);
        endTitle.gameObject.SetActive(state==RunState.GameOver);
        retry.gameObject.SetActive(runner.IsServer);
        health.text = $"生命  {stats.Health} / {stats.MaxHealth}"+(stats.Shield>0?$"  ·  护盾 {stats.Shield}":"");
        power.text = $"火力  {stats.Firepower}";
        phase.text = runner.Phase switch
        {
            PlayerRunPhase.Combat => "战斗", PlayerRunPhase.WaitingAtExit => "已抵达 · 等待队友",
            PlayerRunPhase.Eliminated => "本次行程结束", _ => "前行"
        };
        hint.text = runner.Phase == PlayerRunPhase.Combat ? "A / D  闪避     自动射击     空格 / 2  主动道具" :
            "A / D 横移 · 对齐敌人，自动前射 · 拾取图标获得被动 · 空格 / 2 主动道具";
        rewardPanel.gameObject.SetActive(Time.unscaledTime<rewardUntil);
        if(sectionManager==null)sectionManager=FindFirstObjectByType<SectionManager>();
        var sections=sectionManager;
        float progress=0;
        if(sections!=null&&sections.TryGetSpawnedSections(runner.LaneId,out var road)&&road.Count>0&&road[road.Count-1]!=null)
        {
            float start=road[0].transform.position.z,end=road[road.Count-1].ExitPoint.position.z;
            progress=Mathf.InverseLerp(start,end,runner.transform.position.z);
        }
        else if(sections!=null&&sections.CurrentPlan!=null&&sections.CurrentPlan.TryGetLane(runner.LaneId,out var lane))
        {
            float length=0;
            foreach(var piece in lane.Sections)
                if(piece.Prefab!=null&&piece.Prefab.ExitPoint!=null)
                    length+=Vector3.Distance(piece.Prefab.transform.position,piece.Prefab.ExitPoint.position);
            progress=Mathf.InverseLerp(lane.WorldOrigin.z,lane.WorldOrigin.z+length,runner.transform.position.z);
        }
        progressFill.anchorMax=new Vector2(progress,1);
    }
    private void RefreshInventory()
    {
        if(items==null||inventoryCount==null)return;
        var definitions=new System.Collections.Generic.List<UpgradeData>();
        foreach(var definition in items.CaptureDefinitions())if(definition.Category==ItemCategory.Passive)definitions.Add(definition);
        inventoryCount.text=$"被动  {definitions.Count}";
        for(int i=0;i<inventoryIcons.Length;i++)
        {
            int index=definitions.Count-1-i;var definition=index>=0?definitions[index]:null;
            inventoryIcons[i].enabled=definition!=null;
            if(definition!=null){inventoryIcons[i].sprite=definition.Icon;inventoryIcons[i].color=RunLootPolicy.Tint(definition.Rarity);}
        }
    }
    private void HandleReward(UpgradeData item,int shield,ItemRarity rarity)
    {
        rewardUntil=Time.unscaledTime+3;
        rewardIcon.sprite=item!=null?item.Icon:ItemIconLibrary.Get("shield");rewardIcon.color=RunLootPolicy.Tint(rarity);
        rewardTitle.text=item!=null?"获得 · "+item.UpgradeName:$"护盾 +{shield}";
        rewardDescription.text=item!=null?item.Description:"重复或已集齐的被动，转化为护盾碎片。";
        RefreshInventory();
    }
    private void OnDestroy()
    {
        RunLootDirector.LocalReward-=HandleReward;if(items!=null)items.InventoryChanged-=RefreshInventory;
        if (canvasObject != null) Destroy(canvasObject);
    }
}
