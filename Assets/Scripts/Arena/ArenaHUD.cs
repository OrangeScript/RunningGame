using TMPro;
using UnityEngine;

public sealed class ArenaHUD : MonoBehaviour
{
    private ArenaBootstrap arena;
    private GameObject canvasObject;
    private TMP_Text bossText;
    private TMP_Text healthText;
    private TMP_Text resultText;
    private TMP_Text mechanicsText;
    private TMP_Text buildText;
    private GameObject resultActions;
    private UnityEngine.UI.Button retryButton;
    private RectTransform focusPoint;
    private readonly UnityEngine.UI.Image[] activeIcons=new UnityEngine.UI.Image[2];
    private readonly TMP_Text[] activeLabels=new TMP_Text[2];
    private RectTransform bossFill;
    private ArenaPlayerNetwork localPlayer;
    private float nextPlayerScan;
    private void Awake()
    {
        arena = GetComponent<ArenaBootstrap>();
        canvasObject = MinimalUI.Canvas(transform,"ArenaHUD",30);
        var top = MinimalUI.Box(canvasObject.transform,"TopBar",Color.clear);
        MinimalUI.Place(top,new Vector2(.035f,.87f),new Vector2(.965f,.96f));
        var left=MinimalUI.Box(top,"PlayerPaper",new Color(.95f,.95f,.91f,.94f));
        MinimalUI.Place(left,Vector2.zero,new Vector2(.30f,1));
        var right=MinimalUI.Box(top,"BossPaper",new Color(.95f,.95f,.91f,.94f));
        MinimalUI.Place(right,new Vector2(.60f,0),Vector2.one);
        healthText = MinimalUI.Text(top,"Health","",18,MinimalUI.Muted);
        MinimalUI.Place(healthText.rectTransform,new Vector2(.025f,0),new Vector2(.3f,1));
        bossText = MinimalUI.Text(top,"Boss","首领",18,MinimalUI.Ink,TextAlignmentOptions.MidlineRight);
        MinimalUI.Place(bossText.rectTransform,new Vector2(.6f,.2f),new Vector2(.975f,1));
        var track = MinimalUI.Box(top,"BossTrack",MinimalUI.Line);
        MinimalUI.Place(track,new Vector2(.62f,.16f),new Vector2(.975f,.19f));
        bossFill = MinimalUI.Box(track,"BossLife",MinimalUI.Accent);
        var hint = MinimalUI.Text(canvasObject.transform,"Hint","WASD  移动     鼠标瞄准     空格  射击     Q / E  道具",16,MinimalUI.Ink,TextAlignmentOptions.Center);
        MinimalUI.Place(hint.rectTransform,new Vector2(.1f,.035f),new Vector2(.9f,.09f));
        resultText = MinimalUI.Text(canvasObject.transform,"Result","",42,MinimalUI.Ink,TextAlignmentOptions.Center);
        MinimalUI.Place(resultText.rectTransform,new Vector2(.1f,.40f),new Vector2(.9f,.62f));
        resultActions=new GameObject("ResultActions",typeof(RectTransform));resultActions.transform.SetParent(canvasObject.transform,false);
        MinimalUI.Place((RectTransform)resultActions.transform,new Vector2(.32f,.32f),new Vector2(.68f,.40f));
        retryButton=MinimalUI.Button(resultActions.transform,"Retry","再试一次",()=>FindFirstObjectByType<RoomSession>()?.RetryBossEncounter(),true);
        MinimalUI.Place((RectTransform)retryButton.transform,Vector2.zero,new Vector2(.48f,1));
        var back=MinimalUI.Button(resultActions.transform,"Back","返回菜单",()=>FindFirstObjectByType<RoomSession>()?.LeaveRoom());
        MinimalUI.Place((RectTransform)back.transform,new Vector2(.52f,0),Vector2.one);resultActions.SetActive(false);
        mechanicsText=MinimalUI.Text(canvasObject.transform,"SpellCard","",16,MinimalUI.Ink,TextAlignmentOptions.Center);
        MinimalUI.Place(mechanicsText.rectTransform,new Vector2(.25f,.955f),new Vector2(.75f,.99f));
        hint.text="WASD 移动 · Shift 精确移动 · 鼠标 / 空格 射击 · Q / E 道具 · 擦弹蓄满后 R 消弹";
        buildText=MinimalUI.Text(canvasObject.transform,"TeamBuilds","",14,MinimalUI.Muted);
        MinimalUI.Place(buildText.rectTransform,new Vector2(.04f,.65f),new Vector2(.30f,.79f));
        var point=new GameObject("FocusHitPoint",typeof(RectTransform),typeof(ArenaHitPointGraphic));
        point.transform.SetParent(canvasObject.transform,false);focusPoint=(RectTransform)point.transform;
        point.GetComponent<ArenaHitPointGraphic>().raycastTarget=false;
        focusPoint.anchorMin=focusPoint.anchorMax=new Vector2(.5f,.5f);point.SetActive(false);
        for(int i=0;i<2;i++)
        {
            var slot=MinimalUI.Box(canvasObject.transform,"ActiveSlot"+i,new Color(.95f,.95f,.91f,.92f));
            MinimalUI.Place(slot,new Vector2(.04f+i*.16f,.11f),new Vector2(.19f+i*.16f,.18f));
            var rect=MinimalUI.Box(slot,"Icon",MinimalUI.Accent);
            MinimalUI.Place(rect,new Vector2(.05f,.15f),new Vector2(.22f,.85f));
            activeIcons[i]=rect.GetComponent<UnityEngine.UI.Image>();activeIcons[i].preserveAspect=true;
            activeLabels[i]=MinimalUI.Text(slot,"Label","",13,MinimalUI.Ink);
            MinimalUI.Place(activeLabels[i].rectTransform,new Vector2(.28f,0),new Vector2(.98f,1));
        }
    }
    private void Update()
    {
        if (arena == null) return;
        canvasObject.SetActive(Unity.Netcode.NetworkManager.Singleton!=null&&Unity.Netcode.NetworkManager.Singleton.IsListening);
        resultActions.SetActive(arena.Result!=ArenaMatchResult.None);
        retryButton.gameObject.SetActive(arena.IsServerAuthority);
        bossText.text = $"镜庭守望者   {Mathf.CeilToInt(arena.BossHealth)}";
        mechanicsText.text=arena.SpellName+"   /   "+(arena.ExposedSeconds>0?$"破绽 {arena.ExposedSeconds:0.0} 秒":$"破甲 {arena.BreakGauge:0}%")+
            (string.IsNullOrEmpty(arena.LastReaction)?"":"   ·   "+arena.LastReaction);
        bossFill.anchorMax = new Vector2(arena.BossMaxHealth>0 ? Mathf.Clamp01(arena.BossHealth/arena.BossMaxHealth):0,1);
        if ((localPlayer == null || !localPlayer.IsSpawned) && Time.unscaledTime >= nextPlayerScan)
        {
            nextPlayerScan=Time.unscaledTime+.5f;
            foreach (var player in FindObjectsByType<ArenaPlayerNetwork>(FindObjectsSortMode.None))
                if (player.IsSpawned && player.IsOwner && player.IsParticipatingPawn) { localPlayer=player; break; }
        }
        if (localPlayer != null)
        {
            healthText.text = $"生命 {Mathf.CeilToInt(localPlayer.CurrentHealth)}  /  擦弹 {localPlayer.GrazeCharge:0}%";
            var inventory=localPlayer.GetComponent<ItemManager>();
            for(int i=0;i<2;i++)
            {
                var definition=inventory!=null?inventory.GetActiveDefinition(i):null;
                activeIcons[i].sprite=definition!=null?definition.Icon:null;activeIcons[i].enabled=definition!=null;
                float remaining=inventory!=null?inventory.GetActiveCooldown(i):0;
                activeLabels[i].text=(i==0?"Q  ":"E  ")+(definition==null?"空":definition.UpgradeName)+(remaining>0?$"  {remaining:0}s":"");
            }
            string builds="";
            foreach(var player in ArenaPlayerNetwork.ActivePlayers)
            {
                if(player==null||!player.IsParticipatingPawn)continue;
                var items=player.GetComponent<ItemManager>();var spec=new ProjectileSpec();items?.ModifyProjectile(spec);
                builds+=(player.IsOwner?"你":$"队友 {player.OwnerClientId}")+"  ·  "+ElementLabel(spec.element)+
                    (spec.secondaryElement!=ElementType.None?" / "+ElementLabel(spec.secondaryElement):"")+"\n";
            }
            buildText.text=builds;
        }
        resultText.text = arena.Result == ArenaMatchResult.Victory ? "行程完成" : arena.Result == ArenaMatchResult.Defeat ? "行程结束" : "";
    }
    private void LateUpdate()=>RefreshFocusPoint();
    public void RefreshFocusPoint()
    {
        bool visible=localPlayer!=null&&localPlayer.IsFocused&&localPlayer.IsArenaAlive&&arena.Result==ArenaMatchResult.None;
        focusPoint.gameObject.SetActive(visible);
        if(!visible||Camera.main==null)return;
        Vector3 position=localPlayer.transform.position;position.y=.75f;
        Vector3 viewport=Camera.main.WorldToViewportPoint(position);
        Vector2 canvasSize=((RectTransform)canvasObject.transform).rect.size;
        focusPoint.anchoredPosition=Vector2.Scale(new Vector2(viewport.x-.5f,viewport.y-.5f),canvasSize);
        float diameter=localPlayer.HitRadius*canvasSize.y/Camera.main.orthographicSize;
        focusPoint.sizeDelta=Vector2.one*Mathf.Max(7,diameter);
    }
    private static string ElementLabel(ElementType element)=>element switch
    {
        ElementType.Fire=>"火",ElementType.Water=>"水",ElementType.Ice=>"冰",ElementType.Lightning=>"雷",
        ElementType.Wind=>"风",ElementType.Earth=>"土",ElementType.Nature=>"草",_=>"物理"
    };
    private void OnDestroy() { if (canvasObject != null) Destroy(canvasObject); }
}
