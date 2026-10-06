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
    
    }

    [Header("Run Flow")]
    // [SerializeField] private RunnerController runnerController;
    // [SerializeField] private ItemManager itemManager;
    [SerializeField] private UpgradeSelectionUI upgradeSelectionUI;

    private NetworkVariable<RunState> runState = 
        new NetworkVariable<RunState>(RunState.Running,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    public RunState state => runState.Value;

    public event Action<RunState> StateChanged;
    private CombatEncounter currentEncounter;

    void Start()
    {
        ApplyState();
    }

    public void BeginCombat(
        CombatEncounter encounter
    )
    {
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


        SetState(
            RunState.UpgradeSelection
        );


        OpenUpgradeSelection();
    }


    private void OpenUpgradeSelection()
    {
        upgradeSelectionUI.Show(FinishUpgradeSelection);
    }

    private void FinishUpgradeSelection(UpgradeData upgrade)
    {
        if (
            upgrade != null &&
            itemManager != null
        )
        {
            itemManager.Acquire(
                upgrade
            );
        }


        if (
            currentEncounter !=
            null
        )
        {
            currentEncounter
                .FinishEncounter();
        }


        currentEncounter =
            null;


        SetState(
            RunState.Running
        );
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
