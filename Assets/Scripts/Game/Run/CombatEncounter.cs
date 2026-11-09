using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


[RequireComponent(typeof(BoxCollider))]
public class CombatEncounter : NetworkBehaviour
{

    [Header("References")]

    private GameManager gameManager;


    [Header("Enemy")]

    [SerializeField]
    private Enemy enemyPrefab;

    [SerializeField]
    private Enemy[] enemyVariants;

    [SerializeField]
    private Transform[] spawnPoints;

    [Header("Procedural Encounter")]
    [SerializeField, Min(1f)] private float baseSpawnBudget = 3f;
    [SerializeField, Min(1)] private int maximumEnemies = 10;
    [SerializeField, Min(0f)] private float spawnJitter = 1.6f;


    [Header("Barriers")]

    [SerializeField]
    private GameObject[] barriers;


    private readonly List<Enemy>
        aliveEnemies =
            new List<Enemy>();


    private bool started;

    private bool cleared;

    private bool finished;
    private RunnerController participant;
    private int contentSeed;
    private int laneId = -1;
    private float difficulty = 1f;
    private bool configured;

    private readonly NetworkVariable<int> replicatedSeed = new NetworkVariable<int>();
    private readonly NetworkVariable<int> replicatedLaneId = new NetworkVariable<int>(-1);
    private readonly NetworkVariable<float> replicatedDifficulty = new NetworkVariable<float>(1f);

    private NetworkVariable<bool>
        barriersActive =
            new NetworkVariable<bool>(
                false
            );
    public bool IsStarted =>
        started;

    public bool IsCleared =>
        cleared;

    public bool IsFinished =>
        finished;

    public int LaneId => laneId;

    void Awake()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
    }

    public override void OnNetworkSpawn()
    {
        gameManager = GameManager.instance;
        barriersActive.OnValueChanged += HandleBarrierChanged;
        HandleBarrierChanged(false,barriersActive.Value);

        if (IsServer && !configured)
        {
            LevelSection section = GetComponentInParent<LevelSection>();
            int fallbackSeed = section != null && section.HasRuntimePlan
                ? section.ContentSeed
                : ProceduralRandom.Derive(gameObject.scene.buildIndex, transform.GetSiblingIndex());
            int fallbackLane = section != null ? section.LaneIndex : -1;
            ConfigureServer(fallbackSeed, fallbackLane, 1f);
        }
        else if (!IsServer)
        {
            contentSeed = replicatedSeed.Value;
            laneId = replicatedLaneId.Value;
            difficulty = replicatedDifficulty.Value;
        }
        
    }

    public override void OnNetworkDespawn()
    {
        barriersActive.OnValueChanged -= HandleBarrierChanged;
        foreach(Enemy enemy in aliveEnemies)
        {
            if(enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
            }
        }
    }

    private void HandleBarrierChanged(bool previousValue, bool newValue)
    {
        if (barriers == null)
            return;

        foreach (
            GameObject barrier
            in barriers
        )
        {
            if (barrier == null)
                continue;


            barrier.SetActive(
                newValue
            );
        }
    }

    public void ConfigureServer(int seed, int configuredLaneId, float configuredDifficulty)
    {
        if (!IsServer)
            return;

        configured = true;
        contentSeed = seed;
        laneId = configuredLaneId;
        difficulty = Mathf.Max(0.25f, configuredDifficulty);
        replicatedSeed.Value = contentSeed;
        replicatedLaneId.Value = laneId;
        replicatedDifficulty.Value = difficulty;

        LaneScope scope = GetComponent<LaneScope>();
        if (scope == null)
            scope = gameObject.AddComponent<LaneScope>();
        scope.Configure(laneId);
    }

    public void BuildObstacleFieldServer(int seed, int configuredLaneId, float configuredDifficulty)
    {
        if (!IsServer)
            return;

        Transform root = ResolveSectionRoot();
        ProceduralObstacleField.Build(root, seed, configuredLaneId, configuredDifficulty, true);
        BuildObstacleFieldClientRpc(seed, configuredLaneId, configuredDifficulty);
    }

    [ClientRpc]
    private void BuildObstacleFieldClientRpc(int seed, int configuredLaneId, float configuredDifficulty)
    {
        if (IsServer)
            return;

        ProceduralObstacleField.Build(
            ResolveSectionRoot(), seed, configuredLaneId, configuredDifficulty, false);
    }

    public void FinishEncounter()
    {
        if (finished)
            return;


        finished =
            true;


        SetBarriers(
            false
        );

        if (IsServer)
        {
            for (int i = aliveEnemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = aliveEnemies[i];
                if (enemy == null)
                    continue;

                enemy.Died -= HandleEnemyDied;
                if (enemy.NetworkObject != null && enemy.NetworkObject.IsSpawned)
                    enemy.NetworkObject.Despawn(true);
                else
                    Destroy(enemy.gameObject);
            }

            aliveEnemies.Clear();
        }


        Debug.Log(
            $"{name} Finished"
        );
    }


    private void SetBarriers(
        bool active
    )
    {
        if(!IsServer) return;
        barriersActive.Value = active;
        
    }


    void Start()
    {
        gameManager = GameManager.instance;
        SetBarriers(false);
    }

    private void OnTriggerEnter(
        Collider other
    )
    {
        if(!IsServer) return;
        if (started)
            return;


        RunnerController player =
            other.GetComponentInParent<
                RunnerController
            >();


        if (player == null)
            return;

        // Parallel roads can be physically close enough for a large player
        // collider to brush a neighbouring encounter trigger. Never let one
        // lane claim another lane's encounter/runner pair.
        if (laneId >= 0 && player.LaneId != laneId)
            return;


        BeginEncounter(player);
    }


    // ========================================
    // Begin
    // ========================================

    public void BeginEncounter(RunnerController runner)
    {
        if (!IsServer || started || runner == null)
            return;

        if (laneId >= 0 && runner.LaneId != laneId)
            return;


        if (gameManager == null)
        {
            Debug.LogError(
                $"{name} 找不到 GameManager"
            );

            return;
        }


        if (!gameManager.BeginCombat(this, runner))
            return;

        participant = runner;
        started = true;


        Debug.Log(
            $"{name} Begin Encounter"
        );


        SetBarriers(
            true
        );


        SpawnEnemies();
    }


    // ========================================
    // Spawn
    // ========================================

    private void SpawnEnemies()
    {
        aliveEnemies.Clear();


        if (enemyPrefab == null && (enemyVariants == null || enemyVariants.Length == 0))
        {
            Debug.LogError(
                $"{name} 没有配置 Enemy Prefab"
            );

            return;
        }


        var random = new ProceduralRandom(contentSeed);
        float remainingBudget = Mathf.Max(1f, baseSpawnBudget * difficulty);
        int spawnedCount = 0;

        while (remainingBudget >= 0.75f && spawnedCount < maximumEnemies)
        {
            EnemyArchetype archetype = SelectArchetype(ref random, difficulty);
            float cost = GetArchetypeCost(archetype);
            if (cost > remainingBudget && spawnedCount > 0)
                break;

            Enemy prefab = SelectEnemyPrefab(ref random);
            if (prefab == null)
                break;

            GetSpawnPose(ref random, spawnedCount, out Vector3 position, out Quaternion rotation);


            Enemy enemy =
                Instantiate(
                    prefab,
                    position,
                    rotation
                );
            enemy.ConfigureServer(
                ProceduralRandom.Derive(contentSeed, spawnedCount, laneId),
                laneId,
                difficulty,
                archetype);
            NetworkObject networkObject = enemy.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"{enemy.name} is missing NetworkObject.");
                Destroy(enemy.gameObject);
                spawnedCount++;
                remainingBudget -= cost;
                continue;
            }

            networkObject.Spawn(true);

            enemy.Died +=
                HandleEnemyDied;


            aliveEnemies.Add(
                enemy
            );

            spawnedCount++;
            remainingBudget -= cost;
        }


        Debug.Log(
            $"{name} Spawn Enemy：{aliveEnemies.Count}"
        );


        if (
            aliveEnemies.Count ==
            0
        )
        {
            ClearEncounter();
        }
    }

    private Enemy SelectEnemyPrefab(ref ProceduralRandom random)
    {
        if (enemyVariants != null && enemyVariants.Length > 0)
        {
            int start = random.Range(0, enemyVariants.Length);
            for (int offset = 0; offset < enemyVariants.Length; offset++)
            {
                Enemy candidate = enemyVariants[(start + offset) % enemyVariants.Length];
                if (candidate != null)
                    return candidate;
            }
        }

        return enemyPrefab;
    }

    private EnemyArchetype SelectArchetype(ref ProceduralRandom random, float encounterDifficulty)
    {
        float roll = random.Range(0f, 1f);
        float advancedChance = Mathf.InverseLerp(0.75f, 3.5f, encounterDifficulty);

        if (roll < 0.38f - advancedChance * 0.12f)
            return EnemyArchetype.Chaser;
        if (roll < 0.66f)
            return EnemyArchetype.Strafer;
        if (roll < 0.82f + advancedChance * 0.08f)
            return EnemyArchetype.Charger;
        return EnemyArchetype.Ranged;
    }

    private static float GetArchetypeCost(EnemyArchetype archetype)
    {
        return archetype switch
        {
            EnemyArchetype.Charger => 1.6f,
            EnemyArchetype.Ranged => 1.35f,
            EnemyArchetype.Strafer => 1.1f,
            _ => 1f
        };
    }

    private void GetSpawnPose(
        ref ProceduralRandom random,
        int index,
        out Vector3 position,
        out Quaternion rotation)
    {
        Transform point = null;
        if (spawnPoints != null && spawnPoints.Length > 0)
            point = spawnPoints[index % spawnPoints.Length];

        if (point != null)
        {
            position = point.position;
            rotation = point.rotation;
        }
        else
        {
            position = transform.position + transform.forward * (4f + index * 1.4f);
            rotation = transform.rotation;
        }

        position += transform.right * random.Range(-spawnJitter, spawnJitter);
        position += transform.forward * random.Range(-0.6f, 0.6f);
    }

    private Transform ResolveSectionRoot()
    {
        LevelSection section = GetComponentInParent<LevelSection>();
        return section != null ? section.transform : transform.root;
    }


    // ========================================
    // Death
    // ========================================

    private void HandleEnemyDied(
        Enemy enemy
    )
    {
        if(!IsServer) return;
        if (enemy == null)
            return;


        enemy.Died -=
            HandleEnemyDied;


        aliveEnemies.Remove(
            enemy
        );


        Debug.Log(
            $"{name} Remaining Enemy：{aliveEnemies.Count}"
        );


        if (
            aliveEnemies.Count ==
            0
        )
        {
            ClearEncounter();
        }
    }

    private void ClearEncounter()
    {
        if (cleared)
            return;


        cleared =
            true;


        Debug.Log(
            $"{name} Cleared"
        );


        gameManager.CompleteCombat(this, participant);
    }

}
