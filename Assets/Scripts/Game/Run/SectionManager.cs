using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Running.Game.Run.Generation;
using Unity.Netcode;
using UnityEngine;

public class SectionManager : NetworkBehaviour
{
    private const int MaxLaneCount = 4;

    [Header("Start")]
    [SerializeField]
    private Transform startPoint;

    [Header("Legacy Prefabs")]
    [Tooltip("Backward-compatible fallback. Used as weighted candidates when no valid catalog is configured.")]
    [SerializeField]
    private LevelSection[] sectionPrefabs;

    [Header("Generation Catalog")]
    [SerializeField]
    private WeightedSectionEntry[] sectionCatalog;

    [Min(0)]
    [SerializeField]
    private int bodySectionCount = 8;

    [SerializeField]
    private bool appendBoss = true;

    [SerializeField]
    private bool appendFinish = true;

    [Header("Parallel Lanes")]
    [Tooltip("When networking is active, one lane is generated per connected player (clamped to 1-4).")]
    [SerializeField]
    private bool useConnectedPlayerCount = true;

    [Range(1, MaxLaneCount)]
    [SerializeField]
    private int fallbackLaneCount = 1;

    [Min(0f)]
    [SerializeField]
    private float laneSpacing = RunSectionPresentation.LaneSpacing;

    [Header("Seed")]
    [SerializeField]
    private bool useFixedSeed;

    [SerializeField]
    private int fixedSeed = 12345;

    private float nodeDifficultyMultiplier = 1f;

    private readonly NetworkVariable<RunPlanHeader> activePlanHeader =
        new NetworkVariable<RunPlanHeader>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly List<LevelSection> spawnedSections =
        new List<LevelSection>();

    private readonly Dictionary<int, List<LevelSection>> spawnedSectionsByLane =
        new Dictionary<int, List<LevelSection>>();

    private readonly Dictionary<int, ReadOnlyCollection<LevelSection>> spawnedViewsByLane =
        new Dictionary<int, ReadOnlyCollection<LevelSection>>();

    private ReadOnlyCollection<LevelSection> spawnedSectionsView;
    private SectionRunPlan currentPlan;
    private uint revision;

    public int ActiveSeed => activePlanHeader.Value.Seed;
    public int ActiveLaneCount => activePlanHeader.Value.LaneCount;
    public RunPlanHeader ActivePlanHeader => activePlanHeader.Value;
    public SectionRunPlan CurrentPlan => currentPlan;

    /// <summary>
    /// Server-owned spawned instances. Clients should use CurrentPlan for the
    /// deterministic plan and normal NGO spawning for scene instances.
    /// </summary>
    public IReadOnlyList<LevelSection> SpawnedSections
    {
        get
        {
            if (spawnedSectionsView == null)
            {
                spawnedSectionsView = spawnedSections.AsReadOnly();
            }

            return spawnedSectionsView;
        }
    }

    public event Action<RunPlanHeader> ActivePlanChanged;
    public event Action<SectionRunPlan> PlanGenerated;
    public event Action<SectionRunPlan> RunBuilt;
    public event Action<int, LaneRunPlan, IReadOnlyList<LevelSection>> LaneBuilt;
    public event Action RunCleared;

    public void SetNodeDifficulty(float multiplier)
    {
        nodeDifficultyMultiplier = Mathf.Clamp(multiplier, 0.5f, 5f);
    }

    public override void OnNetworkSpawn()
    {
        activePlanHeader.OnValueChanged += HandleActivePlanHeaderChanged;
        revision = activePlanHeader.Value.Revision;
        HandleActivePlanHeaderChanged(default, activePlanHeader.Value);
    }

    public override void OnNetworkDespawn()
    {
        activePlanHeader.OnValueChanged -= HandleActivePlanHeaderChanged;
    }

    /// <summary>
    /// Backward-compatible entry point used by GameManager.
    /// </summary>
    public void BuildFiniteRun()
    {
        if (!CanMutateOnServer(nameof(BuildFiniteRun)))
        {
            return;
        }

        int seed = useFixedSeed ? fixedSeed : CreateServerSeed();
        int lanes = ResolveLaneCount();
        TryBuildFiniteRun(seed, lanes);
    }

    public bool TryBuildFiniteRun(int seed, int requestedLaneCount)
    {
        if (!CanMutateOnServer(nameof(TryBuildFiniteRun)))
        {
            return false;
        }

        if (!TryCreatePlan(seed, requestedLaneCount, out SectionRunPlan plan))
        {
            return false;
        }

        PlanGenerated?.Invoke(plan);
        return TrySpawnPlan(plan);
    }

    /// <summary>
    /// Pure planning step: it does not spawn, despawn, or change network state.
    /// This can be used by previews and deterministic tests.
    /// </summary>
    public bool TryCreatePlan(
        int seed,
        int requestedLaneCount,
        out SectionRunPlan plan)
    {
        plan = null;
        if (!TryBuildCandidateCatalog(out List<SectionCandidate> candidates))
        {
            return false;
        }

        Transform origin = startPoint != null ? startPoint : transform;
        int laneCount = Mathf.Clamp(requestedLaneCount, 1, MaxLaneCount);

        ReportCatalogLimitations(candidates);

        plan = DeterministicSectionPlanner.Generate(
            seed,
            laneCount,
            origin.position,
            origin.rotation,
            Mathf.Max(RunSectionPresentation.LaneSpacing, laneSpacing),
            Mathf.Max(0, bodySectionCount),
            appendBoss,
            appendFinish,
            candidates);

        return plan.LaneCount > 0;
    }

    /// <summary>
    /// Re-instantiates the exact immutable plan, including each section's
    /// content seed. Catalog randomization is not run again.
    /// </summary>
    public bool ReplayCurrentPlan()
    {
        if (!CanMutateOnServer(nameof(ReplayCurrentPlan)))
        {
            return false;
        }

        SectionRunPlan planToReplay = currentPlan;
        if (planToReplay == null)
        {
            Debug.LogWarning("SectionManager: there is no run plan to replay.", this);
            return false;
        }

        return TrySpawnPlan(planToReplay);
    }

    public bool TrySpawnPlan(SectionRunPlan plan)
    {
        if (!CanMutateOnServer(nameof(TrySpawnPlan)))
        {
            return false;
        }

        if (plan == null)
        {
            Debug.LogError("SectionManager: cannot spawn a null run plan.", this);
            return false;
        }

        if (!ValidatePlanForNetworkSpawn(plan))
        {
            return false;
        }

        ClearSpawnedSectionsInternal(false);

        for (int laneListIndex = 0; laneListIndex < plan.Lanes.Count; laneListIndex++)
        {
            LaneRunPlan lanePlan = plan.Lanes[laneListIndex];
            var laneInstances = new List<LevelSection>(lanePlan.Sections.Count);
            ReadOnlyCollection<LevelSection> laneView = laneInstances.AsReadOnly();
            spawnedSectionsByLane.Add(lanePlan.LaneIndex, laneInstances);
            spawnedViewsByLane.Add(lanePlan.LaneIndex, laneView);

            Vector3 nextPosition = lanePlan.WorldOrigin;
            Quaternion nextRotation = lanePlan.WorldRotation;

            for (int sectionIndex = 0; sectionIndex < lanePlan.Sections.Count; sectionIndex++)
            {
                PlannedSection plannedSection = lanePlan.Sections[sectionIndex];
                if (!TrySpawnSection(
                        lanePlan,
                        plannedSection,
                        nextPosition,
                        nextRotation,
                        out LevelSection section,
                        out nextPosition,
                        out nextRotation))
                {
                    Debug.LogError(
                        $"SectionManager: spawning stopped at lane {lanePlan.LaneIndex}, " +
                        $"section {plannedSection.SequenceIndex} ({plannedSection.DefinitionId}). " +
                        "All partially spawned sections will be removed.",
                        this);
                    ClearSpawnedSectionsInternal(true);
                    return false;
                }

                laneInstances.Add(section);
                spawnedSections.Add(section);
            }

            LaneBuilt?.Invoke(lanePlan.LaneIndex, lanePlan, laneView);
        }

        currentPlan = plan;
        PublishPlanHeader(plan);
        RunBuilt?.Invoke(plan);
        return true;
    }

    public void ClearFiniteRun()
    {
        if (!CanMutateOnServer(nameof(ClearFiniteRun)))
        {
            return;
        }

        ClearSpawnedSectionsInternal(true);
    }

    public bool TryGetLanePlan(int laneIndex, out LaneRunPlan lanePlan)
    {
        if (currentPlan != null)
        {
            return currentPlan.TryGetLane(laneIndex, out lanePlan);
        }

        lanePlan = null;
        return false;
    }

    public bool TryGetSpawnedSections(
        int laneIndex,
        out IReadOnlyList<LevelSection> laneSections)
    {
        if (spawnedViewsByLane.TryGetValue(
                laneIndex,
                out ReadOnlyCollection<LevelSection> view))
        {
            laneSections = view;
            return true;
        }

        laneSections = Array.Empty<LevelSection>();
        return false;
    }

    private void HandleActivePlanHeaderChanged(
        RunPlanHeader previous,
        RunPlanHeader current)
    {
        revision = Math.Max(revision, current.Revision);
        ActivePlanChanged?.Invoke(current);

        if (IsServer)
        {
            return;
        }

        if (!current.HasPlan)
        {
            currentPlan = null;
            return;
        }

        if (TryCreatePlan(current.Seed, current.LaneCount, out SectionRunPlan synchronizedPlan))
        {
            currentPlan = synchronizedPlan;
            PlanGenerated?.Invoke(synchronizedPlan);
        }
        else
        {
            currentPlan = null;
            Debug.LogError(
                $"SectionManager: client could not reconstruct plan revision {current.Revision}. " +
                "Verify that server and client use the same section catalog.",
                this);
        }
    }

    private bool TrySpawnSection(
        LaneRunPlan lanePlan,
        PlannedSection plannedSection,
        Vector3 position,
        Quaternion rotation,
        out LevelSection spawnedSection,
        out Vector3 exitPosition,
        out Quaternion exitRotation)
    {
        spawnedSection = null;
        exitPosition = position;
        exitRotation = rotation;

        LevelSection instance = null;
        try
        {
            instance = Instantiate(plannedSection.Prefab, position, rotation);
            instance.ConfigureRuntime(
                lanePlan.LaneIndex,
                plannedSection.SequenceIndex,
                plannedSection.ContentSeed,
                plannedSection.DefinitionId,
                position,
                rotation);

            if (!instance.TryGetComponent(out NetworkObject networkObject))
            {
                Debug.LogError(
                    $"SectionManager: instantiated section '{plannedSection.Prefab.name}' " +
                    "does not have a root NetworkObject.",
                    instance);
                Destroy(instance.gameObject);
                return false;
            }

            networkObject.Spawn(true);

            float difficulty = nodeDifficultyMultiplier *
                (1f + plannedSection.SequenceIndex * 0.12f);
            ProceduralSectionRuntime.ConfigureServer(
                instance,
                plannedSection.ContentSeed,
                lanePlan.LaneIndex,
                difficulty);

            if (plannedSection.Type == SectionType.Run)
            {
                BuildRunSectionVisualsClientRpc(
                    new NetworkObjectReference(networkObject),
                    plannedSection.ContentSeed,
                    lanePlan.LaneIndex,
                    difficulty);
            }

            if (!instance.TryGetExitPose(out exitPosition, out exitRotation))
            {
                Debug.LogError(
                    $"SectionManager: section '{instance.name}' has no ExitPoint.",
                    instance);
                networkObject.Despawn(true);
                return false;
            }

            spawnedSection = instance;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"SectionManager: failed to spawn '{plannedSection.DefinitionId}'.\n{exception}",
                this);

            if (instance != null)
            {
                NetworkObject networkObject = instance.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn(true);
                }
                else
                {
                    Destroy(instance.gameObject);
                }
            }

            return false;
        }
    }

    [ClientRpc]
    private void BuildRunSectionVisualsClientRpc(
        NetworkObjectReference sectionReference,
        int seed,
        int laneId,
        float difficulty)
    {
        if (IsServer)
            return;

        if (!sectionReference.TryGet(out NetworkObject networkObject) ||
            networkObject == null ||
            !networkObject.TryGetComponent(out LevelSection section))
        {
            Debug.LogWarning("Could not resolve a procedural Run section on this client.");
            return;
        }

        ProceduralSectionRuntime.BuildClientVisuals(section, seed, laneId, difficulty);
    }

    private bool ValidatePlanForNetworkSpawn(SectionRunPlan plan)
    {
        if (plan.LaneCount < 1 || plan.LaneCount > MaxLaneCount)
        {
            Debug.LogError(
                $"SectionManager: plan lane count {plan.LaneCount} is outside 1-{MaxLaneCount}.",
                this);
            return false;
        }

        if (NetworkManager == null || !NetworkManager.IsListening)
        {
            Debug.LogError(
                "SectionManager: NetworkManager must be listening before sections can spawn.",
                this);
            return false;
        }

        var validatedPrefabs = new HashSet<GameObject>();
        var laneIds = new HashSet<int>();

        for (int laneIndex = 0; laneIndex < plan.Lanes.Count; laneIndex++)
        {
            LaneRunPlan lane = plan.Lanes[laneIndex];
            if (lane == null ||
                lane.LaneIndex < 0 ||
                lane.LaneIndex >= MaxLaneCount ||
                !laneIds.Add(lane.LaneIndex))
            {
                Debug.LogError(
                    "SectionManager: plan contains a null, duplicate, or out-of-range lane.",
                    this);
                return false;
            }

            for (int sectionIndex = 0; sectionIndex < lane.Sections.Count; sectionIndex++)
            {
                PlannedSection planned = lane.Sections[sectionIndex];
                if (planned == null || planned.Prefab == null)
                {
                    Debug.LogError(
                        $"SectionManager: lane {lane.LaneIndex}, section {sectionIndex} has no prefab.",
                        this);
                    return false;
                }

                if (planned.SequenceIndex != sectionIndex)
                {
                    Debug.LogError(
                        $"SectionManager: lane {lane.LaneIndex} has a non-contiguous sequence index " +
                        $"({planned.SequenceIndex}, expected {sectionIndex}).",
                        this);
                    return false;
                }

                if (planned.Prefab.Type != planned.Type)
                {
                    Debug.LogError(
                        $"SectionManager: plan type {planned.Type} does not match prefab " +
                        $"'{planned.Prefab.name}' type {planned.Prefab.Type}.",
                        planned.Prefab);
                    return false;
                }

                if (planned.Prefab.ExitPoint == null)
                {
                    Debug.LogError(
                        $"SectionManager: prefab '{planned.Prefab.name}' has no ExitPoint.",
                        planned.Prefab);
                    return false;
                }

                GameObject prefabObject = planned.Prefab.gameObject;
                if (!validatedPrefabs.Add(prefabObject))
                {
                    continue;
                }

                if (!prefabObject.TryGetComponent(out NetworkObject _))
                {
                    Debug.LogError(
                        $"SectionManager: prefab '{planned.Prefab.name}' requires a root NetworkObject.",
                        planned.Prefab);
                    return false;
                }

                if (!NetworkManager.NetworkConfig.Prefabs.Contains(prefabObject))
                {
                    Debug.LogError(
                        $"SectionManager: prefab '{planned.Prefab.name}' is not registered in " +
                        "NetworkManager.NetworkConfig.Prefabs. Add it to DefaultNetworkPrefabs " +
                        "before generating the run.",
                        planned.Prefab);
                    return false;
                }
            }
        }

        return true;
    }

    private bool TryBuildCandidateCatalog(out List<SectionCandidate> candidates)
    {
        candidates = new List<SectionCandidate>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        bool catalogConfigured = sectionCatalog != null && sectionCatalog.Length > 0;
        if (catalogConfigured)
        {
            for (int i = 0; i < sectionCatalog.Length; i++)
            {
                WeightedSectionEntry entry = sectionCatalog[i];
                if (entry == null || !entry.Enabled)
                {
                    continue;
                }

                SectionDefinition definition = entry.Definition;
                if (definition == null)
                {
                    Debug.LogWarning(
                        $"SectionManager: catalog entry {i} has no SectionDefinition and was skipped.",
                        this);
                    continue;
                }

                if (definition.Prefab == null)
                {
                    Debug.LogError(
                        $"SectionManager: definition '{definition.name}' has no prefab and was skipped.",
                        definition);
                    continue;
                }

                if (!definition.IsPrefabTypeConsistent)
                {
                    Debug.LogError(
                        $"SectionManager: definition '{definition.DefinitionId}' says {definition.Type}, " +
                        $"but prefab '{definition.Prefab.name}' says {definition.Prefab.Type}.",
                        definition);
                    continue;
                }

                float weight = definition.BaseWeight * entry.WeightMultiplier;
                if (weight <= 0f)
                {
                    continue;
                }

                string id = definition.DefinitionId;
                if (!ids.Add(id))
                {
                    Debug.LogError(
                        $"SectionManager: duplicate section definition id '{id}' was skipped.",
                        definition);
                    continue;
                }

                candidates.Add(new SectionCandidate(
                    id,
                    definition.Type,
                    definition.Prefab,
                    weight));
            }
        }

        if (candidates.Count == 0)
        {
            if (catalogConfigured)
            {
                Debug.LogWarning(
                    "SectionManager: the configured generation catalog has no valid entries; " +
                    "falling back to the legacy sectionPrefabs array.",
                    this);
            }

            AddLegacyCandidates(candidates, ids);
        }

        if (candidates.Count == 0)
        {
            Debug.LogError(
                "SectionManager: no valid section candidates exist. Configure sectionCatalog " +
                "or keep at least one non-null entry in sectionPrefabs.",
                this);
            return false;
        }

        // Catalog order should not change a seeded replay.
        candidates.Sort((left, right) =>
            string.CompareOrdinal(left.DefinitionId, right.DefinitionId));
        return true;
    }

    private void AddLegacyCandidates(
        List<SectionCandidate> candidates,
        HashSet<string> ids)
    {
        if (sectionPrefabs == null)
        {
            return;
        }

        for (int i = 0; i < sectionPrefabs.Length; i++)
        {
            LevelSection prefab = sectionPrefabs[i];
            if (prefab == null)
            {
                continue;
            }

            string id = $"legacy/{prefab.Type}/{prefab.name}/{i}";
            if (!ids.Add(id))
            {
                continue;
            }

            candidates.Add(new SectionCandidate(
                id,
                prefab.Type,
                prefab,
                1f));
        }
    }

    private void ReportCatalogLimitations(IReadOnlyList<SectionCandidate> candidates)
    {
        bool hasRun = HasCandidate(candidates, SectionType.Run);
        bool hasGate = HasCandidate(candidates, SectionType.Gate);
        bool hasCombat = HasCandidate(candidates, SectionType.Combat);

        if (!hasRun || !hasGate || !hasCombat)
        {
            Debug.LogWarning(
                "SectionManager: a complete interleaved run requires Run, Gate, and Combat " +
                $"candidates. Available: Run={hasRun}, Gate={hasGate}, Combat={hasCombat}. " +
                "Generation will use the safest available alternating fallback.",
                this);
        }

        if (appendBoss && !HasCandidate(candidates, SectionType.Boss))
        {
            Debug.LogWarning(
                "SectionManager: appendBoss is enabled but the catalog has no Boss candidate; " +
                "the final Boss section will be skipped.",
                this);
        }

        if (appendFinish && !HasCandidate(candidates, SectionType.Finish))
        {
            Debug.LogWarning(
                "SectionManager: appendFinish is enabled but the catalog has no Finish candidate; " +
                "the Finish section will be skipped.",
                this);
        }
    }

    private static bool HasCandidate(
        IReadOnlyList<SectionCandidate> candidates,
        SectionType type)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Type == type && candidates[i].Weight > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private int ResolveLaneCount()
    {
        int resolved = Mathf.Clamp(fallbackLaneCount, 1, MaxLaneCount);
        if (useConnectedPlayerCount &&
            NetworkManager != null &&
            NetworkManager.IsListening &&
            NetworkManager.ConnectedClientsIds.Count > 0)
        {
            int connectedCount = NetworkManager.ConnectedClientsIds.Count;
            if (connectedCount > MaxLaneCount)
            {
                Debug.LogWarning(
                    $"SectionManager: {connectedCount} clients are connected, but only " +
                    $"{MaxLaneCount} lanes are supported. Extra clients will not get a lane.",
                    this);
            }

            resolved = Mathf.Clamp(connectedCount, 1, MaxLaneCount);
        }

        return resolved;
    }

    private int CreateServerSeed()
    {
        long ticks = DateTime.UtcNow.Ticks;
        int low = unchecked((int)ticks);
        int high = unchecked((int)(ticks >> 32));
        return DeterministicRandom.DeriveSeed(
            low ^ Environment.TickCount,
            high);
    }

    private bool CanMutateOnServer(string operation)
    {
        if (!IsSpawned)
        {
            Debug.LogError(
                $"SectionManager: {operation} requires this NetworkBehaviour to be spawned.",
                this);
            return false;
        }

        if (!IsServer)
        {
            Debug.LogWarning(
                $"SectionManager: only the server can execute {operation}.",
                this);
            return false;
        }

        return true;
    }

    private void PublishPlanHeader(SectionRunPlan plan)
    {
        revision = NextRevision(revision);
        activePlanHeader.Value = new RunPlanHeader(
            plan.Seed,
            plan.LaneCount,
            revision);
    }

    private void PublishClearedHeader()
    {
        revision = NextRevision(revision);
        activePlanHeader.Value = new RunPlanHeader(0, 0, revision);
    }

    private static uint NextRevision(uint current)
    {
        unchecked
        {
            current++;
        }

        return current == 0u ? 1u : current;
    }

    private void ClearSpawnedSectionsInternal(bool publishHeader)
    {
        bool hadRun = currentPlan != null || spawnedSections.Count > 0;

        for (int i = spawnedSections.Count - 1; i >= 0; i--)
        {
            LevelSection section = spawnedSections[i];
            if (section == null)
            {
                continue;
            }

            NetworkObject networkObject = section.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError(
                    $"SectionManager: spawned section '{section.name}' lost its NetworkObject; " +
                    "destroying the local object directly.",
                    section);
                Destroy(section.gameObject);
                continue;
            }

            if (networkObject.IsSpawned)
            {
                networkObject.Despawn(true);
            }
            else
            {
                Destroy(section.gameObject);
            }
        }

        spawnedSections.Clear();
        spawnedSectionsByLane.Clear();
        spawnedViewsByLane.Clear();
        currentPlan = null;

        if (publishHeader)
        {
            PublishClearedHeader();
        }

        if (hadRun)
        {
            RunCleared?.Invoke();
        }
    }
}
