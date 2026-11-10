using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// A stretch of road containing stationary defenders. Enemies are prepared
/// before they enter view; passing or clearing the group never stops movement.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class CombatEncounter : NetworkBehaviour
{
    [Header("Enemy")]
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private Enemy[] enemyVariants;
    [SerializeField] private Transform[] spawnPoints;
    [Header("Procedural Encounter")]
    [SerializeField, Min(1)] private float baseSpawnBudget = 3;
    [SerializeField, Min(1)] private int maximumEnemies = 10;
    [SerializeField, Min(0)] private float spawnJitter = 1.6f;
    [Header("Legacy barriers (always open)")]
    [SerializeField] private GameObject[] barriers;

    private readonly List<Enemy> aliveEnemies = new List<Enemy>();
    private GameManager gameManager;
    private RunnerController participant;
    private bool configured, prepared, started, cleared, finished;
    private int contentSeed, laneId = -1;
    private float difficulty = 1, retirementZ = 26;
    private readonly NetworkVariable<int> replicatedSeed = new NetworkVariable<int>();
    private readonly NetworkVariable<int> replicatedLaneId = new NetworkVariable<int>(-1);
    private readonly NetworkVariable<float> replicatedDifficulty = new NetworkVariable<float>(1);

    public bool IsStarted => started;
    public bool IsCleared => cleared;
    public bool IsFinished => finished;
    public bool IsPrepared => prepared;
    public int LaneId => IsSpawned ? replicatedLaneId.Value : laneId;
    public IReadOnlyList<Enemy> AliveEnemies => aliveEnemies;

    private void Awake()
    {
        var trigger = GetComponent<BoxCollider>(); trigger.isTrigger = true;
        OpenBarriers();
    }

    public override void OnNetworkSpawn()
    {
        gameManager = GameManager.instance;
        OpenBarriers();
        if (IsServer && !configured)
        {
            var section = GetComponentInParent<LevelSection>();
            // Planned roads are configured by ProceduralSectionRuntime after
            // obstacles exist, so placement can reject their swept footprints.
            if (section == null || !section.HasRuntimePlan)
            {
                int seed = ProceduralRandom.Derive(gameObject.scene.buildIndex, transform.GetSiblingIndex());
                ConfigureServer(seed, section != null ? section.LaneIndex : -1, 1);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        foreach (var enemy in aliveEnemies) if (enemy != null) enemy.Died -= HandleEnemyDied;
        if (IsServer) RetireRemainingEnemies();
    }

    public void ConfigureServer(int seed, int configuredLaneId, float configuredDifficulty)
    {
        if (!IsServer || prepared) return;
        configured = true; contentSeed = seed; laneId = configuredLaneId;
        difficulty = Mathf.Max(.25f, configuredDifficulty);
        replicatedSeed.Value = contentSeed; replicatedLaneId.Value = laneId;
        replicatedDifficulty.Value = difficulty;
        var scope = GetComponent<LaneScope>() ?? gameObject.AddComponent<LaneScope>(); scope.Configure(laneId);
        OpenBarriers();
        SpawnEnemies();
    }

    public void BuildObstacleFieldServer(int seed, int configuredLaneId, float configuredDifficulty)
    {
        if (!IsServer) return;
        ProceduralObstacleField.Build(ResolveSectionRoot(), seed, configuredLaneId, configuredDifficulty, true);
        BuildObstacleFieldClientRpc(seed, configuredLaneId, configuredDifficulty);
    }

    [ClientRpc]
    private void BuildObstacleFieldClientRpc(int seed, int configuredLaneId, float configuredDifficulty)
    {
        if (!IsServer) ProceduralObstacleField.Build(ResolveSectionRoot(), seed, configuredLaneId, configuredDifficulty, false);
    }

    private void Update()
    {
        if (!IsServer || !prepared || finished) return;
        if (!started) TryBeginForApproachingRunner();
        if (participant == null) return;
        float forwardPosition = ResolveSectionRoot().InverseTransformPoint(participant.transform.position).z;
        if (!participant.IsSpawned || participant.Phase == PlayerRunPhase.Eliminated || forwardPosition > retirementZ)
        {
            // Passing a defender is not a kill and must never create a drop.
            gameManager?.CompleteCombat(this, participant);
            FinishEncounter();
        }
    }

    private void TryBeginForApproachingRunner()
    {
        if (NetworkManager == null) return;
        Transform root = ResolveSectionRoot();
        foreach (var pair in NetworkManager.ConnectedClients)
        {
            var player = pair.Value.PlayerObject;
            var runner = player != null ? player.GetComponent<RunnerController>() : null;
            if (runner == null || !runner.IsSpawned || (laneId >= 0 && runner.LaneId != laneId) ||
                (runner.Phase != PlayerRunPhase.Running && runner.Phase != PlayerRunPhase.Combat)) continue;
            float distance = root.InverseTransformPoint(runner.transform.position).z;
            if (distance >= -RunnerEnemyAttackPattern.ActivationRange && distance < retirementZ)
            { BeginEncounter(runner); return; }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsServer && !started) BeginEncounter(other.GetComponentInParent<RunnerController>());
    }

    public void BeginEncounter(RunnerController runner)
    {
        if (!IsServer || started || finished || runner == null || (laneId >= 0 && runner.LaneId != laneId)) return;
        if (runner.Phase != PlayerRunPhase.Running && runner.Phase != PlayerRunPhase.Combat) return;
        if (gameManager == null) gameManager = GameManager.instance;
        if (!prepared)
        {
            if (!configured) ConfigureServer(ProceduralRandom.Derive(gameObject.scene.buildIndex, GetInstanceID()), runner.LaneId, 1);
            else SpawnEnemies();
        }
        participant = runner; started = true;
        // Registration is advisory. Adjacent road groups may be active at the
        // same time; one group cannot lock out the next group or its shooting.
        gameManager?.BeginCombat(this, runner);
        OpenBarriers();
        if (cleared) gameManager?.CompleteCombat(this, participant);
    }

    public void FinishEncounter()
    {
        if (finished) return;
        finished = true; OpenBarriers();
        if (IsServer) RetireRemainingEnemies();
    }

    private void RetireRemainingEnemies()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            var enemy = aliveEnemies[i]; if (enemy == null) continue;
            enemy.Died -= HandleEnemyDied;
            // Despawn intentionally bypasses Enemy.Die and therefore loot.
            if (enemy.NetworkObject != null && enemy.NetworkObject.IsSpawned) enemy.NetworkObject.Despawn(true);
            else Destroy(enemy.gameObject);
        }
        aliveEnemies.Clear();
    }

    private void SpawnEnemies()
    {
        if (prepared) return;
        prepared = true;
        if (enemyPrefab == null && (enemyVariants == null || enemyVariants.Length == 0))
        { Debug.LogWarning($"{name}: no enemy prefab configured."); cleared = true; return; }
        var random = new ProceduralRandom(contentSeed);
        int count = Mathf.Clamp(Mathf.RoundToInt(baseSpawnBudget * Mathf.Sqrt(Mathf.Max(.5f, difficulty))) +
            random.Range(-1, 1), 2, Mathf.Min(4, Mathf.Max(2, maximumEnemies)));
        float lastZ = 0;
        for (int i = 0; i < count; i++)
        {
            var prefab = SelectEnemyPrefab(ref random); if (prefab == null) continue;
            if (!TryGetSpawnPose(ref random, i, count, out Vector3 position)) continue;
            var enemy = Instantiate(prefab, position, Quaternion.LookRotation(Vector3.back));
            var kind = SelectArchetype(ref random, difficulty);
            bool eliteDefender = i == 0 && GameManager.instance != null && GameManager.instance.IsEliteRoad;
            if (eliteDefender) kind = random.Chance(.5f) ? EnemyArchetype.Charger : EnemyArchetype.Ranged;
            enemy.ConfigureServer(ProceduralRandom.Derive(contentSeed, i, laneId), laneId,
                difficulty * (eliteDefender ? 1.25f : 1), kind);
            var networkObject = enemy.GetComponent<NetworkObject>();
            if (networkObject == null)
            { Destroy(enemy.gameObject); Debug.LogError("Enemy prefab requires a registered NetworkObject."); continue; }
            networkObject.Spawn(true);
            enemy.Died += HandleEnemyDied; aliveEnemies.Add(enemy);
            lastZ = Mathf.Max(lastZ, ResolveSectionRoot().InverseTransformPoint(position).z);
        }
        retirementZ = Mathf.Max(12, lastZ + 6);
        if (aliveEnemies.Count == 0) cleared = true;
    }

    private Enemy SelectEnemyPrefab(ref ProceduralRandom random)
    {
        if (enemyVariants != null && enemyVariants.Length > 0)
        {
            int first = random.Range(0, enemyVariants.Length);
            for (int i = 0; i < enemyVariants.Length; i++)
            { var candidate = enemyVariants[(first + i) % enemyVariants.Length]; if (candidate != null) return candidate; }
        }
        return enemyPrefab;
    }

    private static EnemyArchetype SelectArchetype(ref ProceduralRandom random, float difficulty)
    {
        float roll = random.Range(0f, 1f);
        float specialChance = Mathf.InverseLerp(.75f, 3.5f, difficulty);
        if (roll < .50f - specialChance * .12f) return EnemyArchetype.Chaser;
        if (roll < .75f) return EnemyArchetype.Strafer;
        if (roll < .87f) return EnemyArchetype.Charger;
        return EnemyArchetype.Ranged;
    }

    private bool TryGetSpawnPose(ref ProceduralRandom random, int index, int count, out Vector3 position)
    {
        Transform root = ResolveSectionRoot();
        var section = root.GetComponent<LevelSection>();
        if (section == null && spawnPoints != null && spawnPoints.Length > 0)
        {
            var point = spawnPoints[index % spawnPoints.Length];
            if (point != null && IsSafePlacement(root, root.InverseTransformPoint(point.position)))
            { position = point.position; return true; }
        }
        float length = 20;
        if (section != null && section.TryGetExitPose(out var exit, out _))
            length = Mathf.Max(12, root.InverseTransformPoint(exit).z);
        float depth = Mathf.Lerp(6.5f, length - 2.5f, index / (float)Mathf.Max(1, count - 1));
        int startSlot = random.Range(0, 5);
        position = root.TransformPoint(new Vector3((startSlot - 2) * 2.5f, 0, depth));
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int slot = (startSlot + attempt * 2) % 5;
            float jitter = Mathf.Clamp(spawnJitter * .12f, 0, .25f);
            float x = (slot - 2) * 2.5f + random.Range(-jitter, jitter);
            float z = Mathf.Clamp(depth + random.Range(-.35f, .35f), 5.5f, length - 1.8f);
            Vector3 local = new Vector3(x, 0, z);
            if (!IsSafePlacement(root, local)) continue;
            position = root.TransformPoint(local); return true;
        }
        // The planner reserves a shoulder. Validate both edges rather than
        // silently putting an enemy into a moving obstacle on unlucky seeds.
        float side = random.Chance(.5f) ? -1 : 1;
        for (int i = 0; i < 2; i++)
        {
            var local = new Vector3(side * 6.25f, 0, depth);
            if (IsSafePlacement(root, local)) { position = root.TransformPoint(local); return true; }
            side = -side;
        }
        return false;
    }

    private bool IsSafePlacement(Transform sectionRoot, Vector3 local)
    {
        foreach (var obstacle in RunnerObstacleRuntime.ActiveObstacles)
        {
            if (obstacle == null || obstacle.IsDestroyed || obstacle.LaneId != laneId ||
                obstacle.GetComponentInParent<LevelSection>()?.transform != sectionRoot) continue;
            if (RunnerDefenderPlacement.OverlapsObstacle(local, obstacle.Plan)) return false;
        }
        Vector3 world = sectionRoot.TransformPoint(local);
        foreach (var enemy in aliveEnemies)
            if (enemy != null && (world - enemy.transform.position).sqrMagnitude < 2.2f * 2.2f) return false;
        return true;
    }

    private Transform ResolveSectionRoot()
    {
        var section = GetComponentInParent<LevelSection>();
        return section != null ? section.transform : transform;
    }
    private void OpenBarriers()
    {
        if (barriers != null) foreach (var barrier in barriers) if (barrier != null) barrier.SetActive(false);
    }
    private void HandleEnemyDied(Enemy enemy)
    {
        if (!IsServer || enemy == null) return;
        enemy.Died -= HandleEnemyDied; aliveEnemies.Remove(enemy);
        if (aliveEnemies.Count > 0 || cleared) return;
        cleared = true;
        // Kill rewards are physical drops at Enemy.Died, never modal choices.
        if (participant != null) gameManager?.CompleteCombat(this, participant);
    }
}
