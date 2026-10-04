using System;
using UnityEngine;

public class GameManager : MonoBehaviour
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
    [SerializeField] private RunnerController runnerController;
    [SerializeField] private ItemManager itemManager;
    [SerializeField] private UpgradeSelectionUI upgradeSelectionUI;

    public RunState state
    {
        get;
        private set;
    } = RunState.Running;

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
        if (state == newState)
            return;


        state =
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
        if(runnerController == null) return;
        bool enableForwardMovement = state == RunState.Running;
        runnerController.SetForwardMovementEnabled(
                enableForwardMovement
            );
    }

}
