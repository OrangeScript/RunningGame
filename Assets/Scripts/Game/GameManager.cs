using System;
using System.Collections.Generic;
using System.Data.Common;
using Unity.Netcode;
using Unity.VisualScripting;
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
        BuildUpgradeLookup();
    
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
    private NetworkVariable<RunState> runState = 
        new NetworkVariable<RunState>(RunState.Lobby,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    
    private NetworkVariable<int> connectedPlayerCount
        = new NetworkVariable<int>(0,NetworkVariableReadPermission.Everyone,
                                NetworkVariableWritePermission.Server);
    public RunState state => runState.Value;

    public event Action<RunState> StateChanged;
    private CombatEncounter currentEncounter;
    public int ConnectedPlayerCount => connectedPlayerCount.Value;
    public event Action<int> PlayerCountChanged;


    [Header("Upgrade")]
    [SerializeField]
    UpgradeData[] upgradePool;
    Dictionary<int,UpgradeData> upgradeById = new Dictionary<int, UpgradeData>();
    Dictionary<ulong,int[]> offeredUpgradeIds = new Dictionary<ulong, int[]>();
    HashSet<ulong> pendingUpgradeClients = new HashSet<ulong>();


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
        if (sectionManager == null)
        {
            Debug.LogError(
                "GameManager 找不到 SectionManager"
            );

            return;
        }


        // 地图直到真正开局才生成
        sectionManager
            .BuildFiniteRun();


        SetState(
            RunState.Running
        );
    }


    public override void OnNetworkSpawn()
    {
        runState.OnValueChanged += HandleRunStateChanged;
        connectedPlayerCount.OnValueChanged += HandlePlayerCountChanged;

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

    private void RefreshPlayerCount()
    {
        if(!IsServer) return;
        connectedPlayerCount.Value = NetworkManager.ConnectedClientsIds.Count;
    }

    private void HandleClientDisconnected(ulong obj)
    {
        if(!IsServer) return;
        RefreshPlayerCount();
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
        StateChanged?.Invoke(newValue);
    }

    void Start()
    {
        ApplyState();
    }

    public void BeginCombat(
        CombatEncounter encounter
    )
    {
        if(!IsServer) return;
        if (encounter == null)
            return;


        if (
            state !=
            RunState.Running
        )
        {
            return;
        }


        currentEncounter =
            encounter;


        SetState(
            RunState.Combat
        );
    }

    public void CompleteCombat(
        CombatEncounter encounter
    )
    {
        if(!IsServer) return;
        if (
            state !=
            RunState.Combat
        )
        {
            return;
        }


        if (
            currentEncounter !=
            encounter
        )
        {
            return;
        }

        currentEncounter.FinishEncounter();
        currentEncounter = null;
        SetState(
            RunState.UpgradeSelection
        );
        BeginUpgradeSelection();


        // OpenUpgradeSelection();
    }

    private void BeginUpgradeSelection()
    {
        if(!IsServer) return;
        offeredUpgradeIds.Clear();
        pendingUpgradeClients.Clear();

        foreach(ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            int[] options = CreateUpgradeOptions();
            offeredUpgradeIds[clientId] = options;
            pendingUpgradeClients.Add(clientId);
            SendUpgradeOptionsToClient(clientId,options);

        }

        if(pendingUpgradeClients.Count == 0)
        {
            FinishUpgradeSelection();
        }
    }

    private void FinishUpgradeSelection()
    {
        if (!IsServer)
        return;


        if (currentEncounter != null)
        {
            currentEncounter
                .FinishEncounter();
        }


        currentEncounter =
            null;


        offeredUpgradeIds.Clear();

        pendingUpgradeClients.Clear();


        SetState(
            RunState.Running
        );
    }

    private void SendUpgradeOptionsToClient(ulong clientId, int[] options)
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
            options,rpcParams
        );
    }


    [ClientRpc]
    private void ShowUpgradeOptionsClientRpc(int[] options, ClientRpcParams rpcParams)
    {
        List<UpgradeData> datas = new List<UpgradeData>();
        foreach(int id in options)
        {
            UpgradeData data = GetUpgradeById(id);
            datas.Add(data);
        }
        upgradeSelectionUI.Show(datas.ToArray(),HandleLocalUpgradeSelected);
    }

    private void HandleLocalUpgradeSelected(UpgradeData data)
    {
        SubmitUpgradeSelectionServerRpc(data.NetworkId);
    }



    [ServerRpc(RequireOwnership = false)]
    private void SubmitUpgradeSelectionServerRpc(int networkId,ServerRpcParams rpcParams=default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if(state!=RunState.UpgradeSelection) return;
        if(!pendingUpgradeClients.Contains(senderClientId)) return;

        pendingUpgradeClients.Remove(senderClientId);

        if(!offeredUpgradeIds.TryGetValue(senderClientId,out int[] offeredIds)) return;
        bool allowed = Array.IndexOf(offeredIds,networkId) >= 0;
        if(!allowed) return;
        

        NetworkClient client = NetworkManager.ConnectedClients[senderClientId];
        ItemManager itemManager = client.PlayerObject.GetComponent<ItemManager>();
        itemManager.AcquireServer(upgradeById[networkId]);
        pendingUpgradeClients.Remove(senderClientId);
        offeredUpgradeIds.Remove(senderClientId);
        if (
            pendingUpgradeClients.Count ==
            0
        )
        {
            FinishUpgradeSelection();
        }
    }

    private UpgradeData GetUpgradeById(
        int id
    )
    {
        upgradeById.TryGetValue(id,out UpgradeData upgrade);
        return upgrade;
    }


    // private void OpenUpgradeSelection()
    // {
    //     upgradeSelectionUI.Show(FinishUpgradeSelection);
    // }

    // private void FinishUpgradeSelection(UpgradeData upgrade)
    // {
    //     if (
    //         upgrade != null &&
    //         itemManager != null
    //     )
    //     {
    //         itemManager.Acquire(
    //             upgrade
    //         );
    //     }


    //     if (
    //         currentEncounter !=
    //         null
    //     )
    //     {
    //         currentEncounter
    //             .FinishEncounter();
    //     }


    //     currentEncounter =
    //         null;


    //     SetState(
    //         RunState.Running
    //     );
    // }

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

    }

    private void BuildUpgradeLookup()
    {
        upgradeById.Clear();
        foreach(UpgradeData upgrade
            in upgradePool)
        {
            upgradeById.Add(upgrade.NetworkId,upgrade);
        }
    }

    public int[] CreateUpgradeOptions()
    {
        const int optionCount = 3;
        List<UpgradeData> candidates = new List<UpgradeData>(upgradeById.Values);
        int[] result = new int[optionCount];
        for(int i = 0;
            i < optionCount;
            i++)
        {
            int randomIndex =
            UnityEngine.Random.Range(
                0,
                candidates.Count
            );


            UpgradeData selected =
                candidates[
                    randomIndex
                ];


            result[i] =
                selected.NetworkId;


            candidates.RemoveAt(
                randomIndex
            );
        }
        return result;

    }


}
