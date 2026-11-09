using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

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
            // A trap pair remains population-dependent: subtracting is safer
            // for a large crowd while division can be safer for a tiny one.
            int subtraction = random.Range(2 + stage, 5 + stage * 3);
            int divisor = random.Range(2, difficulty >= 3f ? 4 : 3);
            left = new GateOffer(GateOperation.Subtract, subtraction);
            right = new GateOffer(GateOperation.Divide, divisor);
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
        triggeredPlayers.Clear();
    }

    public void ConfigureServer(GateOffer offer, int laneId)
    {
        if (!IsServer)
            return;

        operation = offer.Operation;
        value = Mathf.Max(1, offer.Value);
        syncedOperation.Value = (int)operation;
        syncedValue.Value = value;
        syncedLaneId.Value = laneId;
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
        if (section != null)
            foreach (Gate sibling in section.GetComponentsInChildren<Gate>())
                if (sibling != this && sibling.triggeredPlayers.Contains(ownerId)) return;
        if (!triggeredPlayers.Add(ownerId))
            return;

        ApplyGate(crowd);
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
        if (gateText == null)
            return;

        string symbol = Operation switch
        {
            GateOperation.Add => "+",
            GateOperation.Subtract => "−",
            GateOperation.Divide => "÷",
            GateOperation.Multiply => "×",
            _ => string.Empty
        };

        gateText.text = $"{symbol}{Value}";
        gateText.fontSize = 7;
        gateText.color = MinimalUI.Ink;
        Color frame = Operation == GateOperation.Add || Operation == GateOperation.Multiply
            ? new Color(.42f,.56f,.46f) : new Color(.67f,.43f,.34f);
        foreach (var renderer in GetComponentsInChildren<Renderer>())
            if (renderer.GetComponent<TMP_Text>() == null) RunSectionPresentation.Paint(renderer,frame);
    }

    private Transform ResolveSectionRoot()
    {
        LevelSection section = GetComponentInParent<LevelSection>();
        return section != null ? section.transform : transform.root;
    }
}
