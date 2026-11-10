using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public enum GateOperation : byte
{
    Add,
    Subtract,
    Multiply,
    Divide
}

[Serializable]
public readonly struct GateOffer
{
    public GateOffer(GateOperation operation, int value)
    {
        Operation = operation;
        Value = Mathf.Max(1, value);
    }

    public GateOperation Operation { get; }
    public int Value { get; }
}

/// <summary>Creates paired gate choices whose value depends on population.</summary>
public static class GateOfferGenerator
{
    public static void GeneratePair(
        int seed,
        float difficulty,
        out GateOffer left,
        out GateOffer right)
    {
        var random = new ProceduralRandom(seed);
        int stage = Mathf.Max(1, Mathf.CeilToInt(difficulty));

        // +N versus xM crosses over at a population threshold, so neither
        // choice is universally correct.
        if (random.Chance(0.72f))
        {
            int flat = random.Range(5 + stage * 2, 12 + stage * 5);
            int multiplier = random.Range(2, difficulty >= 2.5f ? 4 : 3);
            left = new GateOffer(GateOperation.Add, flat);
            right = new GateOffer(GateOperation.Multiply, multiplier);
        }
        else
        {
            // There is always one safe choice. A costly gate can carry a
            // passive, making its price an optional build decision.
            left = new GateOffer(GateOperation.Add,random.Range(3+stage,7+stage*2));
            right = random.Chance(.5f)?new GateOffer(GateOperation.Subtract,random.Range(2+stage,5+stage*2)):
                new GateOffer(GateOperation.Divide,2);
        }

        if (random.Chance(0.5f))
            (left, right) = (right, left);
    }
}

public class Gate : NetworkBehaviour
{
    [Header("Gate fallback")]
    [SerializeField] private GateOperation operation;
    [SerializeField] private int value = 1;

    [Header("Visual")]
    [SerializeField] private TMP_Text gateText;

    private readonly NetworkVariable<int> syncedOperation =
        new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> syncedValue =
        new NetworkVariable<int>(1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> syncedLaneId =
        new NetworkVariable<int>(-1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly HashSet<ulong> triggeredPlayers = new HashSet<ulong>();
    private readonly NetworkVariable<int> rewardItemId=new NetworkVariable<int>();
    private Transform rewardBoard;
    private TMP_Text numberLabel,itemLabel,sourceLabel;
    private Image itemIcon;
    public int RewardItemId=>rewardItemId.Value;
    public UpgradeData RewardItem=>RuntimeItemCatalog.Default?.Find(RewardItemId);
    public static int PreviewPower(GateOffer offer,int power)=>offer.Operation switch
    {
        GateOperation.Add=>(int)Math.Min(9999L,(long)power+offer.Value),
        GateOperation.Subtract=>Mathf.Max(1,power-offer.Value),
        GateOperation.Multiply=>(int)Math.Min(9999L,(long)power*offer.Value),
        GateOperation.Divide=>Mathf.Max(1,power/offer.Value),_=>power
    };

    public GateOperation Operation => IsSpawned
        ? (GateOperation)Mathf.Clamp(syncedOperation.Value, 0, 3)
        : operation;
    public int Value => IsSpawned ? Mathf.Max(1, syncedValue.Value) : Mathf.Max(1, value);
    public int LaneId => IsSpawned ? syncedLaneId.Value : -1;

    private void Start()
    {
        UpdateText();
    }

    public override void OnNetworkSpawn()
    {
        syncedOperation.OnValueChanged += HandleOperationChanged;
        syncedValue.OnValueChanged += HandleValueChanged;
        rewardItemId.OnValueChanged+=HandleRewardChanged;

        if (IsServer)
        {
            syncedOperation.Value = (int)operation;
            syncedValue.Value = Mathf.Max(1, value);
        }

        UpdateText();
    }

    public override void OnNetworkDespawn()
    {
        syncedOperation.OnValueChanged -= HandleOperationChanged;
        syncedValue.OnValueChanged -= HandleValueChanged;
        rewardItemId.OnValueChanged-=HandleRewardChanged;
        triggeredPlayers.Clear();
    }

    public void ConfigureServer(GateOffer offer, int laneId,int passiveItemId=0)
    {
        if (!IsServer)
            return;

        operation = offer.Operation;
        value = Mathf.Max(1, offer.Value);
        syncedOperation.Value = (int)operation;
        syncedValue.Value = value;
        syncedLaneId.Value = laneId;
        rewardItemId.Value=passiveItemId;
        triggeredPlayers.Clear();

        LaneScope scope = GetComponent<LaneScope>();
        if (scope == null)
            scope = gameObject.AddComponent<LaneScope>();
        scope.Configure(laneId);

        UpdateText();
    }

    public void BuildObstacleFieldServer(int seed, int laneId, float difficulty)
    {
        if (!IsServer)
            return;

        Transform root = ResolveSectionRoot();
        ProceduralObstacleField.Build(root, seed, laneId, difficulty, true);
        BuildObstacleFieldClientRpc(seed, laneId, difficulty);
    }

    [ClientRpc]
    private void BuildObstacleFieldClientRpc(int seed, int laneId, float difficulty)
    {
        if (IsServer)
            return;

        ProceduralObstacleField.Build(
            ResolveSectionRoot(), seed, laneId, difficulty, false);
    }

    private void OnTriggerEnter(Collider other)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer)
            return;

        CrowdManager crowd = other.GetComponentInParent<CrowdManager>();
        RunnerController runner = other.GetComponentInParent<RunnerController>();
        if (crowd == null || runner == null)
            return;
        if (LaneId >= 0 && runner.LaneId != LaneId)
            return;
        if (runner.Phase != PlayerRunPhase.Running) return;

        ulong ownerId = crowd.OwnerClientId;
        LevelSection section = GetComponentInParent<LevelSection>();
        if(section!=null)
        {
            Gate[] pair=section.GetComponentsInChildren<Gate>();
            if(pair.Length==2)
            {
                float center=(pair[0].transform.position.x+pair[1].transform.position.x)*.5f;
                bool left=transform.position.x<center;
                if(left!=(runner.transform.position.x<=center))return;
            }
        }
        if (section != null)
            foreach (Gate sibling in section.GetComponentsInChildren<Gate>())
                if (sibling != this && sibling.triggeredPlayers.Contains(ownerId)) return;
        if (!triggeredPlayers.Add(ownerId))
            return;

        ApplyGate(crowd);
        if(RewardItemId!=0)
        {
            var director=GameManager.instance!=null?GameManager.instance.GetComponent<RunLootDirector>():null;
            if(director!=null)director.GrantGate(runner,RewardItemId);
            else runner.GetComponent<ItemManager>()?.TryAcquireServer(RewardItem,ItemAcquisitionSource.RoadGate);
        }
    }

    private void ApplyGate(CrowdManager crowd)
    {
        switch (Operation)
        {
            case GateOperation.Add:
                crowd.AddPopulation(Value);
                break;
            case GateOperation.Subtract:
                crowd.SubtractPopulation(Value);
                break;
            case GateOperation.Multiply:
                crowd.MultiplyPopulation(Value);
                break;
            case GateOperation.Divide:
                crowd.DividePopulation(Value);
                break;
        }
    }

    private void HandleOperationChanged(int previous, int current)
    {
        operation = (GateOperation)Mathf.Clamp(current, 0, 3);
        UpdateText();
    }

    private void HandleValueChanged(int previous, int current)
    {
        value = Mathf.Max(1, current);
        UpdateText();
    }

    private void UpdateText()
    {
        if(gateText!=null)gateText.gameObject.SetActive(false);
        if(rewardBoard==null)BuildRewardBoard();

        string symbol = Operation switch
        {
            GateOperation.Add => "+",
            GateOperation.Subtract => "−",
            GateOperation.Divide => "÷",
            GateOperation.Multiply => "×",
            _ => string.Empty
        };

        numberLabel.text=$"{symbol}{Value}";
        bool positive=Operation==GateOperation.Add||Operation==GateOperation.Multiply;
        numberLabel.color=positive?MinimalUI.Ink:new Color(.65f,.30f,.24f);
        var item=RewardItem;
        itemLabel.text=item!=null?item.UpgradeName:"火力增幅";
        sourceLabel.text=item!=null?"被动 · 经过即获得":"数值改变攻击强度";
        itemIcon.sprite=item!=null?item.Icon:ItemIconLibrary.Get("gauge");
        itemIcon.color=item!=null?RunLootPolicy.Tint(item.Rarity):MinimalUI.Accent;
        Color frame = Operation == GateOperation.Add || Operation == GateOperation.Multiply
            ? new Color(.42f,.56f,.46f) : new Color(.67f,.43f,.34f);
        foreach (var renderer in GetComponentsInChildren<Renderer>())
            if (renderer.GetComponent<TMP_Text>() == null) RunSectionPresentation.Paint(renderer,frame);
    }
    private void HandleRewardChanged(int previous,int current)=>UpdateText();
    private void LateUpdate()
    {if(rewardBoard!=null&&Camera.main!=null)rewardBoard.rotation=Camera.main.transform.rotation;}
    private void BuildRewardBoard()
    {
        var board=new GameObject("GateRewardBoard",typeof(RectTransform),typeof(Canvas));
        rewardBoard=board.transform;rewardBoard.SetParent(transform,false);
        rewardBoard.localPosition=new Vector3(0,1.7f,-.20f);rewardBoard.localScale=Vector3.one*.016f;
        ((RectTransform)rewardBoard).sizeDelta=new Vector2(260,150);
        board.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        var paper=MinimalUI.Box(rewardBoard,"Paper",new Color(.98f,.98f,.94f,.94f));
        numberLabel=MinimalUI.Text(paper,"Operation","",34,MinimalUI.Ink,TextAlignmentOptions.Center);
        MinimalUI.Place(numberLabel.rectTransform,new Vector2(.08f,.64f),new Vector2(.92f,.98f));
        var icon=MinimalUI.Box(paper,"PassiveIcon",MinimalUI.Accent);
        MinimalUI.Place(icon,new Vector2(.10f,.23f),new Vector2(.32f,.59f));
        itemIcon=icon.GetComponent<Image>();itemIcon.preserveAspect=true;
        itemLabel=MinimalUI.Text(paper,"PassiveName","",20,MinimalUI.Ink,TextAlignmentOptions.MidlineLeft);
        MinimalUI.Place(itemLabel.rectTransform,new Vector2(.38f,.23f),new Vector2(.96f,.59f));
        sourceLabel=MinimalUI.Text(paper,"Source","",13,MinimalUI.Muted,TextAlignmentOptions.Center);
        MinimalUI.Place(sourceLabel.rectTransform,new Vector2(.03f,.02f),new Vector2(.97f,.20f));
    }

    private Transform ResolveSectionRoot()
    {
        LevelSection section = GetComponentInParent<LevelSection>();
        return section != null ? section.transform : transform.root;
    }
}
