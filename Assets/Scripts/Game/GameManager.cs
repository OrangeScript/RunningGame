using System;
using System.Collections.Generic;
using Running.Game.Run.Generation;
using Running.Game.RunMap;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    
    public static GameManager instance
    {
        get;
        private set;
    }

    public void Awake()
    {
        if (
            instance != null &&
            instance != this
        )
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if(sectionManager == null)
        {
            sectionManager = GetComponent<SectionManager>();
        }
        runMapFlow = RunMapRuntimeFlow.Ensure(gameObject);
        mapCoordinator = runMapFlow.Coordinator;

        BuildUpgradeLookup();
        if (Application.isPlaying)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.83f,.86f,.82f);
            RenderSettings.fogStartDistance = 45;
            RenderSettings.fogEndDistance = 110;
            RenderSettings.ambientLight = new Color(.72f,.74f,.69f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = .35f;
                light.color = new Color(1,.97f,.91f);
            }
            foreach (var volume in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
                volume.weight = 0;
        }
    
    }

    public override void OnDestroy()
    {
        if (instance == this)
            instance = null;

        base.OnDestroy();
    }

    [Header("Run Flow")]
    // [SerializeField] private RunnerController runnerController;
    // [SerializeField] private ItemManager itemManager;
    [SerializeField] private UpgradeSelectionUI upgradeSelectionUI;

    [SerializeField] private int minMultiplayers = 2;
    [SerializeField] private int maxMultiplayers = 4;
    public int MinMultiplayerPlayers => minMultiplayers;
    public int MaxPlayers => maxMultiplayers;
    [SerializeField] private SectionManager sectionManager;
    private RunMapRuntimeFlow runMapFlow;
    private RunMapNetworkCoordinator mapCoordinator;
    private RunMapNodeSnapshot activeMapNode;
    private readonly HashSet<ulong> finishedLaneClients = new HashSet<ulong>();
    private readonly List<GameObject> laneExitTriggers = new List<GameObject>();
    private readonly Dictionary<ulong, ItemAcquisitionSource> offeredUpgradeSources =
        new Dictionary<ulong, ItemAcquisitionSource>();
    private bool nodeRewardInProgress;
    private NetworkVariable<RunState> runState = 
        new NetworkVariable<RunState>(RunState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    
    private NetworkVariable<int> connectedPlayerCount
        = new NetworkVariable<int>(0,NetworkVariableReadPermission.Everyone,
                                NetworkVariableWritePermission.Server);
    public RunState state => runState.Value;

    public event Action<RunState> StateChanged;
    private readonly Dictionary<ulong, CombatEncounter> activeEncounterByClient =
        new Dictionary<ulong, CombatEncounter>();
    private readonly Dictionary<ulong, RunnerController> runnerByClient =
        new Dictionary<ulong, RunnerController>();
    public int ConnectedPlayerCount => connectedPlayerCount.Value;
    public event Action<int> PlayerCountChanged;


    [Header("Upgrade")]
    [SerializeField]
    UpgradeData[] upgradePool;
    Dictionary<int,UpgradeData> upgradeById = new Dictionary<int, UpgradeData>();
    Dictionary<ulong,int[]> offeredUpgradeIds = new Dictionary<ulong, int[]>();
    HashSet<ulong> pendingUpgradeClients = new HashSet<ulong>();
    private readonly Dictionary<ulong,Queue<int>> recentRewardOffers=new Dictionary<ulong,Queue<int>>();
    private int rewardRoll;


    public void StartMultiplayerRun()
    {
        if (!IsServer)
            return;


        if (
            state !=
            RunState.Lobby
        )
        {
            return;
        }


        if (
            ConnectedPlayerCount <
            minMultiplayers
        )
        {
            Debug.Log(
                $"人数不足，当前 {ConnectedPlayerCount} 人"
            );

            return;
        }


        StartRunInternal();
    }


    public void StartSinglePlayerRun()
    {
        if (!IsServer)
            return;


        if (
            state !=
            RunState.Lobby
        )
        {
            return;
        }


        // 单机不检查最少人数
        StartRunInternal();
    }


    private void StartRunInternal()
    {
        recentRewardOffers.Clear();
        if (sectionManager == null)
        {
            Debug.LogError(
                "GameManager 找不到 SectionManager"
            );

            return;
        }


        InitializePlayerRuns();

        if (mapCoordinator == null)
        {
            Debug.LogError("GameManager is missing RunMapNetworkCoordinator.");
            return;
        }

        int seed = unchecked(Environment.TickCount ^ (int)DateTime.UtcNow.Ticks);
        SetState(RunState.MapSelection);
        if (!runMapFlow.StartRun(seed))
            Debug.LogError("Failed to initialize the run map and its first vote.");
    }


    public override void OnNetworkSpawn()
    {
        runState.OnValueChanged += HandleRunStateChanged;
        connectedPlayerCount.OnValueChanged += HandlePlayerCountChanged;
        if (runMapFlow != null)
            runMapFlow.NodeResolved += HandleMapNodeSelected;

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            RefreshPlayerCount(); // ???
            runState.Value = RunState.Lobby;
        }
        StateChanged?.Invoke(
            runState.Value
        );


        PlayerCountChanged?.Invoke(
            connectedPlayerCount.Value
        );
    }

    public override void OnNetworkDespawn()
    {
        runState.OnValueChanged -= HandleRunStateChanged;
        connectedPlayerCount.OnValueChanged -= HandlePlayerCountChanged;
        if (runMapFlow != null)
            runMapFlow.NodeResolved -= HandleMapNodeSelected;

        if (NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        offeredUpgradeIds.Clear();
        pendingUpgradeClients.Clear();
        activeEncounterByClient.Clear();
        runnerByClient.Clear();
        offeredUpgradeSources.Clear();
        ClearLaneExitTriggers();
    }

    private void RefreshPlayerCount()
    {
        if(!IsServer) return;
        connectedPlayerCount.Value = NetworkManager.ConnectedClientsIds.Count;
    }

    private void HandleClientDisconnected(ulong obj)
    {
        if(!IsServer) return;
        offeredUpgradeIds.Remove(obj);
        offeredUpgradeSources.Remove(obj);
        pendingUpgradeClients.Remove(obj);

        if (activeEncounterByClient.TryGetValue(obj, out CombatEncounter encounter))
        {
            encounter.FinishEncounter();
            activeEncounterByClient.Remove(obj);
        }

        runnerByClient.Remove(obj);
        finishedLaneClients.Remove(obj);
        RefreshPlayerCount();

        if (runnerByClient.Count == 0)
        {
            nodeRewardInProgress = false;
            SetState(RunState.GameOver);
            return;
        }

        if (nodeRewardInProgress && pendingUpgradeClients.Count == 0)
        {
            CompleteNodeAndOpenVote();
            return;
        }

        if (state == RunState.Running)
            TryCompleteRoadWhenAllParticipantsSettled();
    }

    private void HandleClientConnected(ulong obj)
    {
        if(!IsServer) return;
        RefreshPlayerCount();
    }

    private void HandlePlayerCountChanged(int previousValue, int newValue)
    {
        PlayerCountChanged?.Invoke(newValue);
    }

    private void HandleRunStateChanged(RunState previousValue, RunState newValue)
    {
        ApplyState();
        StateChanged?.Invoke(newValue);
    }

    void Start()
    {
        ApplyState();
    }

    public bool BeginCombat(CombatEncounter encounter, RunnerController runner)
    {
        if (!IsServer || encounter == null || runner == null)
            return false;
        if (state != RunState.Running || runner.Phase != PlayerRunPhase.Running)
            return false;

        ulong clientId = runner.OwnerClientId;
        if (activeEncounterByClient.ContainsKey(clientId))
            return false;

        activeEncounterByClient.Add(clientId, encounter);
        runnerByClient[clientId] = runner;
        runner.EnterCombatServer();
        return true;
    }

    public void CompleteCombat(CombatEncounter encounter, RunnerController runner)
    {
        if (!IsServer || encounter == null || runner == null)
            return;

        ulong clientId = runner.OwnerClientId;
        if (!activeEncounterByClient.TryGetValue(clientId, out CombatEncounter active) || active != encounter)
            return;

        encounter.FinishEncounter();
        activeEncounterByClient.Remove(clientId);
        runner.ResumeRunningServer();
    }

    private void BeginUpgradeSelection(
        ulong clientId,
        ItemAcquisitionSource source)
    {
        if(!IsServer) return;

        if (upgradeById.Count == 0)
        {
            Debug.LogWarning("Upgrade pool is empty; skipping reward selection.");
            FinishUpgradeSelection(clientId);
            return;
        }

        int[] options = CreateUpgradeOptions(source,clientId);
        if (options.Length == 0)
        {
            FinishUpgradeSelection(clientId);
            return;
        }

        offeredUpgradeIds[clientId] = options;
        offeredUpgradeSources[clientId] = source;
        pendingUpgradeClients.Add(clientId);
        SendUpgradeOptionsToClient(clientId, options, source);
    }

    private void FinishUpgradeSelection(ulong clientId)
    {
        if (!IsServer)
            return;

        if (activeEncounterByClient.TryGetValue(clientId, out CombatEncounter encounter))
        {
            encounter.FinishEncounter();
            activeEncounterByClient.Remove(clientId);
        }

        if (runnerByClient.TryGetValue(clientId, out RunnerController runner) &&
            runner != null && runner.Phase != PlayerRunPhase.Eliminated)
        {
            if (nodeRewardInProgress)
                runner.WaitAtExitServer();
            else
                runner.ResumeRunningServer();
        }

        offeredUpgradeIds.Remove(clientId);
        offeredUpgradeSources.Remove(clientId);
        pendingUpgradeClients.Remove(clientId);

        if (nodeRewardInProgress && pendingUpgradeClients.Count == 0)
            CompleteNodeAndOpenVote();
    }

    private void SendUpgradeOptionsToClient(
        ulong clientId,
        int[] options,
        ItemAcquisitionSource source)
    {
        ClientRpcParams rpcParams= new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[]
                {
                    clientId
                }
            }
        };
        ShowUpgradeOptionsClientRpc(
            options,
            (byte)source,
            rpcParams
        );
    }


    [ClientRpc]
    private void ShowUpgradeOptionsClientRpc(
        int[] options,
        byte sourceValue,
        ClientRpcParams rpcParams)
    {
        if (upgradeSelectionUI == null)
        {
            Debug.LogError("GameManager is missing UpgradeSelectionUI.");
            return;
        }

        List<UpgradeData> datas = new List<UpgradeData>();
        foreach(int id in options)
        {
            UpgradeData data = GetUpgradeById(id);
            if (data != null)
                datas.Add(data);
        }

        if (datas.Count == 0)
            return;

        ItemAcquisitionSource source = Enum.IsDefined(
            typeof(ItemAcquisitionSource),
            sourceValue)
            ? (ItemAcquisitionSource)sourceValue
            : ItemAcquisitionSource.LevelReward;
        upgradeSelectionUI.Show(
            datas.ToArray(),
            source,
            HandleLocalUpgradeSelected);
    }

    private void HandleLocalUpgradeSelected(UpgradeData data)
    {
        if (data == null)
            return;

        SubmitUpgradeSelectionServerRpc(data.NetworkId);
    }



    [ServerRpc(RequireOwnership = false)]
    private void SubmitUpgradeSelectionServerRpc(int networkId,ServerRpcParams rpcParams=default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if(state != RunState.UpgradeSelection) return;
        if(!pendingUpgradeClients.Contains(senderClientId)) return;

        if(!offeredUpgradeIds.TryGetValue(senderClientId,out int[] offeredIds)) return;
        bool allowed = Array.IndexOf(offeredIds,networkId) >= 0;
        if(!allowed) return;

        if (!upgradeById.TryGetValue(networkId, out UpgradeData selected)) return;
        if (!offeredUpgradeSources.TryGetValue(
                senderClientId,
                out ItemAcquisitionSource source)) return;
        if (NetworkManager.ConnectedClients.TryGetValue(senderClientId, out NetworkClient client) &&
            client.PlayerObject != null)
        {
            ItemManager itemManager = client.PlayerObject.GetComponent<ItemManager>();
            if (itemManager == null ||
                !itemManager.TryAcquireServer(selected, source))
            {
                Debug.LogWarning($"Could not grant upgrade {networkId} to client {senderClientId}.");
            }
        }

        FinishUpgradeSelection(senderClientId);
    }

    private UpgradeData GetUpgradeById(
        int id
    )
    {
        upgradeById.TryGetValue(id,out UpgradeData upgrade);
        return upgrade;
    }

    private void InitializePlayerRuns()
    {
        runnerByClient.Clear();
        List<ulong> clientIds = new List<ulong>(NetworkManager.ConnectedClientsIds);
        clientIds.Sort();

        for (int lane = 0; lane < clientIds.Count; lane++)
        {
            ulong clientId = clientIds[lane];
            NetworkObject playerObject = NetworkManager.ConnectedClients[clientId].PlayerObject;
            if (playerObject == null)
                continue;

            RunnerController runner = playerObject.GetComponent<RunnerController>();
            if (runner == null)
                continue;

            runnerByClient[clientId] = runner;
            runner.ConfigureLaneServer(lane, runner.transform.position.x, RunSectionPresentation.MovementHalfWidth);
            runner.WaitAtExitServer();

            ItemManager itemManager = playerObject.GetComponent<ItemManager>();
            itemManager?.SetGameplayModeServer(GameplayMode.Runner);
        }
    }

    public void NotifyRunnerEliminated(RunnerController runner)
    {
        if (!IsServer || runner == null)
            return;

        ulong clientId = runner.OwnerClientId;
        pendingUpgradeClients.Remove(clientId);
        offeredUpgradeIds.Remove(clientId);
        offeredUpgradeSources.Remove(clientId);

        if (activeEncounterByClient.TryGetValue(clientId, out CombatEncounter encounter))
        {
            encounter.FinishEncounter();
            activeEncounterByClient.Remove(clientId);
        }

        bool hasSurvivingRunner = false;
        foreach (RunnerController candidate in runnerByClient.Values)
        {
            if (candidate != null && candidate.Phase != PlayerRunPhase.Eliminated)
            {
                hasSurvivingRunner = true;
                break;
            }
        }

        if (!hasSurvivingRunner)
        {
            SetState(RunState.GameOver);
            return;
        }

        if (nodeRewardInProgress && pendingUpgradeClients.Count == 0)
        {
            CompleteNodeAndOpenVote();
            return;
        }

        if (state == RunState.Running)
            TryCompleteRoadWhenAllParticipantsSettled();
    }

    private void HandleMapNodeSelected(RunMapNodeSnapshot node)
    {
        if (!IsServer || node == null)
            return;

        activeMapNode = node.Clone();
        if (node.Type == RunMapNodeType.Boss)
        {
            SetState(RunState.BossTransition);
            RoomSession roomSession = FindFirstObjectByType<RoomSession>();
            if (roomSession == null || !roomSession.TransitionToBossArena())
            {
                Debug.LogError("Could not transition to the Boss Arena.");
                SetState(RunState.GameOver);
            }
            return;
        }

        switch (node.Type)
        {
            case RunMapNodeType.Combat:
            case RunMapNodeType.Elite:
                StartRoadNode(node);
                break;

            case RunMapNodeType.Treasure:
            case RunMapNodeType.Event:
            case RunMapNodeType.Reward:
                BeginNodeRewardSelection();
                break;
        }
    }

    private void StartRoadNode(RunMapNodeSnapshot node)
    {
        finishedLaneClients.Clear();
        ClearLaneExitTriggers();
        int laneCount = Mathf.Clamp(runnerByClient.Count, 1, 4);
        sectionManager.SetNodeDifficulty(
            node.Type == RunMapNodeType.Elite ? 1.7f : 1f);

        if (!sectionManager.TryBuildFiniteRun(node.ContentSeed, laneCount) ||
            sectionManager.CurrentPlan == null)
        {
            Debug.LogError($"Failed to build road for map node {node.Id}.");
            BeginNodeRewardSelection();
            return;
        }

        List<ulong> clientIds = new List<ulong>(runnerByClient.Keys);
        clientIds.Sort();
        SectionRunPlan plan = sectionManager.CurrentPlan;
        for (int lane = 0; lane < clientIds.Count; lane++)
        {
            ulong clientId = clientIds[lane];
            RunnerController runner = runnerByClient[clientId];
            if (runner == null || runner.Phase == PlayerRunPhase.Eliminated ||
                !plan.TryGetLane(lane, out LaneRunPlan lanePlan))
            {
                continue;
            }

            runner.TeleportToLaneServer(lane, lanePlan.WorldOrigin, RunSectionPresentation.MovementHalfWidth);
            runner.BeginRunServer();
            CreateLaneExitTrigger(lanePlan);
        }

        SetState(RunState.Running);
    }

    private void CreateLaneExitTrigger(LaneRunPlan lanePlan)
    {
        if (!sectionManager.TryGetSpawnedSections(
                lanePlan.LaneIndex,
                out IReadOnlyList<LevelSection> sections) ||
            sections.Count == 0)
        {
            return;
        }

        LevelSection finalSection = sections[sections.Count - 1];
        if (finalSection == null || finalSection.ExitPoint == null)
            return;

        GameObject triggerObject = new GameObject($"Lane_{lanePlan.LaneIndex}_Exit");
        triggerObject.transform.SetPositionAndRotation(
            finalSection.ExitPoint.position,
            finalSection.ExitPoint.rotation);
        BoxCollider trigger = triggerObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(RunSectionPresentation.RoadWidth, 4f, 2f);
        RunLaneExitTrigger laneExit = triggerObject.AddComponent<RunLaneExitTrigger>();
        laneExit.Configure(this, lanePlan.LaneIndex);
        laneExitTriggers.Add(triggerObject);
    }

    public void NotifyLaneFinished(RunnerController runner, int laneIndex)
    {
        if (!IsServer || state != RunState.Running || runner == null ||
            runner.LaneId != laneIndex || runner.Phase == PlayerRunPhase.Eliminated)
        {
            return;
        }

        if (!finishedLaneClients.Add(runner.OwnerClientId))
            return;

        runner.WaitAtExitServer();
        TryCompleteRoadWhenAllParticipantsSettled();
    }

    /// <summary>
    /// Completes a parallel road only when every connected, non-eliminated
    /// participant has reached its own exit. This same check is used after a
    /// finish, disconnect, or elimination so a departed final runner cannot
    /// strand everyone else in Running forever.
    /// </summary>
    private bool TryCompleteRoadWhenAllParticipantsSettled()
    {
        if (!IsServer || state != RunState.Running || nodeRewardInProgress)
            return false;

        bool hasSurvivingRunner = false;
        foreach (KeyValuePair<ulong, RunnerController> pair in runnerByClient)
        {
            RunnerController candidate = pair.Value;
            if (candidate == null || candidate.Phase == PlayerRunPhase.Eliminated)
                continue;

            hasSurvivingRunner = true;
            if (!finishedLaneClients.Contains(pair.Key))
                return false;
        }

        if (!hasSurvivingRunner)
            return false;

        ClearLaneExitTriggers();
        sectionManager.ClearFiniteRun();
        BeginNodeRewardSelection();
        return true;
    }

    private void BeginNodeRewardSelection()
    {
        if (!IsServer || nodeRewardInProgress)
            return;

        nodeRewardInProgress = true;
        pendingUpgradeClients.Clear();
        offeredUpgradeIds.Clear();
        offeredUpgradeSources.Clear();
        ItemAcquisitionSource source = ResolveRewardSource();

        List<ulong> recipients = new List<ulong>();
        foreach (KeyValuePair<ulong, RunnerController> pair in runnerByClient)
        {
            if (pair.Value == null || pair.Value.Phase == PlayerRunPhase.Eliminated)
                continue;

            pair.Value.EnterRewardChoiceServer();
            recipients.Add(pair.Key);
            pendingUpgradeClients.Add(pair.Key);
        }

        SetState(RunState.UpgradeSelection);
        for (int i = 0; i < recipients.Count; i++)
            BeginUpgradeSelection(recipients[i], source);

        if (recipients.Count == 0)
            CompleteNodeAndOpenVote();
    }

    private ItemAcquisitionSource ResolveRewardSource()
    {
        if (activeMapNode == null)
            return ItemAcquisitionSource.LevelReward;

        return activeMapNode.Type switch
        {
            RunMapNodeType.Treasure => ItemAcquisitionSource.TreasureChest,
            RunMapNodeType.Event => ItemAcquisitionSource.Event,
            RunMapNodeType.Reward => ItemAcquisitionSource.LevelReward,
            _ => ItemAcquisitionSource.LevelReward
        };
    }

    private void CompleteNodeAndOpenVote()
    {
        if (!IsServer || !nodeRewardInProgress)
            return;

        nodeRewardInProgress = false;
        foreach (RunnerController runner in runnerByClient.Values)
        {
            if (runner != null && runner.Phase != PlayerRunPhase.Eliminated)
                runner.WaitAtExitServer();
        }

        SetState(RunState.MapSelection);
        if (!runMapFlow.CompleteCurrentNodeAndOpenVote() &&
            mapCoordinator.ReachableNodeIds.Count == 0)
        {
            SetState(RunState.Victory);
        }
    }

    private void ClearLaneExitTriggers()
    {
        for (int i = 0; i < laneExitTriggers.Count; i++)
        {
            if (laneExitTriggers[i] != null)
                Destroy(laneExitTriggers[i]);
        }

        laneExitTriggers.Clear();
    }

    private void SetState(
        RunState newState
    )
    {
        if(!IsServer) return;
        if (state == newState)
            return;


        runState.Value =
            newState;




        Debug.Log(
            $"Run State → {state}"
        );
    }
    
    private void ApplyState()
    {
        // Scene presentation systems subscribe to StateChanged. This hook is
        // intentionally kept side-effect free so late-joining clients can
        // safely apply the current replicated value.
    }

    private void BuildUpgradeLookup()
    {
        upgradeById.Clear();
        if (upgradePool == null)
            upgradePool=Array.Empty<UpgradeData>();

        foreach(UpgradeData upgrade
            in upgradePool)
        {
            if (upgrade == null)
                continue;

            int id = upgrade.NetworkId;
            if (upgradeById.ContainsKey(id))
            {
                Debug.LogError($"Duplicate upgrade id {id}: {upgrade.name}");
                continue;
            }

            upgradeById.Add(id,upgrade);
        }
        if(RuntimeItemCatalog.Default!=null&&RuntimeItemCatalog.Default.Items!=null)
            foreach(var upgrade in RuntimeItemCatalog.Default.Items)
                if(upgrade!=null&&!upgradeById.ContainsKey(upgrade.NetworkId))upgradeById.Add(upgrade.NetworkId,upgrade);
    }

    public int[] CreateUpgradeOptions(
        ItemAcquisitionSource source = ItemAcquisitionSource.LevelReward,
        ulong clientId = ulong.MaxValue)
    {
        const int optionCount = 3;
        List<UpgradeData> candidates = new List<UpgradeData>();
        foreach (UpgradeData upgrade in upgradeById.Values)
        {
            if (!ItemAcquisitionPolicy.CanAcquire(upgrade, source))
                continue;
            if (source == ItemAcquisitionSource.Event &&
                upgrade.Category != ItemCategory.Temporary)
                continue;
            if (source == ItemAcquisitionSource.LevelReward &&
                upgrade.Category == ItemCategory.Temporary)
                continue;

            candidates.Add(upgrade);
        }

        if (source == ItemAcquisitionSource.TreasureChest)
        {
            List<UpgradeData> highQuality = candidates.FindAll(
                upgrade => upgrade.Rarity >= ItemRarity.Epic);
            if (highQuality.Count > 0)
                candidates = highQuality;
        }

        var owned=new HashSet<int>();
        if(NetworkManager!=null&&NetworkManager.ConnectedClients.TryGetValue(clientId,out var player)&&player.PlayerObject!=null)
        {
            var inventory=player.PlayerObject.GetComponent<ItemManager>();
            if(inventory!=null)for(int i=0;i<inventory.SyncedItemCount;i++)owned.Add(inventory.GetSyncedItemId(i));
        }
        candidates.RemoveAll(item=>item.StackPolicy==ItemStackPolicy.Unique&&owned.Contains(item.NetworkId));
        if(!recentRewardOffers.TryGetValue(clientId,out var history))recentRewardOffers[clientId]=history=new Queue<int>();
        int seed=unchecked((activeMapNode?.ContentSeed??17) ^ (int)clientId ^ ++rewardRoll*7919);
        int[] result=RewardOfferSampler.Draw(candidates,owned,new HashSet<int>(history),seed,optionCount);
        foreach(int id in result)history.Enqueue(id);
        while(history.Count>12)history.Dequeue();
        return result;

    }


}
