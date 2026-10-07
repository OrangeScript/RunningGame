using System;
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


        // OpenUpgradeSelection();
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


        ApplyState();


        StateChanged?.Invoke(
            state
        );


        Debug.Log(
            $"Run State → {state}"
        );
    }
    
    private void ApplyState()
    {

    }

}
