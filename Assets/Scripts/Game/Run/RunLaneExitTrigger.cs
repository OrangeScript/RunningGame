using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class RunLaneExitTrigger : MonoBehaviour
{
    private readonly HashSet<ulong> triggeredClients = new HashSet<ulong>();
    private GameManager gameManager;
    private int laneIndex;

    public void Configure(GameManager manager, int lane)
    {
        gameManager = manager;
        laneIndex = lane;
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (gameManager == null || !gameManager.IsServer)
            return;

        RunnerController runner = other.GetComponentInParent<RunnerController>();
        if (runner == null || runner.LaneId != laneIndex ||
            runner.Phase != PlayerRunPhase.Running ||
            !triggeredClients.Add(runner.OwnerClientId))
        {
            return;
        }

        gameManager.NotifyLaneFinished(runner, laneIndex);
    }
}
