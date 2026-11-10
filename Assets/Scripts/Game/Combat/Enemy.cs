using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Serialized values remain compatible with the existing enemy prefab/catalog.
// Chaser = sentry; Strafer = fan gunner; Charger = locked sniper; Ranged = burst gunner.
public enum EnemyArchetype : byte { Chaser, Strafer, Charger, Ranged }
public enum EnemyAIState : byte { Acquire, Approach, Telegraph, Attack, Recover }

/// <summary>
/// A stationary lane defender. Target acquisition selects when to shoot, never
/// where an ordinary bullet goes. Only the sniper may lock a limited aim angle.
/// </summary>
public class Enemy : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1)] private float maxHealth = 8;
    [Header("Attack")]
    [SerializeField, Min(1)] private int populationDamage = 1;
    [SerializeField, Min(.1f)] private float attackInterval = .8f;
    [SerializeField, Min(.65f)] private float telegraphDuration = .7f;
    [SerializeField, Min(.1f)] private float recoverDuration = .45f;
    [Header("Target / Presentation")]
    [SerializeField] private Transform aimPoint;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator visualAnimator;

    private readonly NetworkVariable<float> currentHealth = new NetworkVariable<float>();
    private readonly NetworkVariable<float> replicatedMaxHealth = new NetworkVariable<float>();
    private readonly NetworkVariable<bool> dead = new NetworkVariable<bool>();
    private readonly NetworkVariable<byte> replicatedArchetype = new NetworkVariable<byte>();
    private readonly NetworkVariable<byte> replicatedState = new NetworkVariable<byte>();
    private readonly NetworkVariable<int> replicatedLane = new NetworkVariable<int>(-1);
    private readonly NetworkVariable<Vector3> lockedDirection = new NetworkVariable<Vector3>(Vector3.back);
    private readonly NetworkVariable<double> warningEndsAt = new NetworkVariable<double>();

    private static readonly HashSet<Enemy> activeEnemies = new HashSet<Enemy>();
    public static IReadOnlyCollection<Enemy> ActiveEnemies => activeEnemies;
    public static event Action<Enemy> ServerSpawned;
    public event Action<float, ElementType> Damaged;
    public event Action<Enemy> Died;

    private StatusController statusController;
    private RunnerController targetRunner;
    private EnemyArchetype archetype;
    private EnemyAIState aiState;
    private int laneId = -1;
    private int contentSeed;
    private float difficulty = 1;
    private float nextAttackTime;
    private float stateEndsAt;
    private float nextTargetRefresh;
    private bool difficultyApplied;
    private Renderer[] visualRenderers;
    private MaterialPropertyBlock visualProperties;
    private RunnerEnemyTelegraph attackTelegraph;
    private EnemyArchetype lastVisualArchetype = (EnemyArchetype)byte.MaxValue;
    private EnemyAIState lastVisualState = (EnemyAIState)byte.MaxValue;

    public bool IsDead => dead.Value;
    public bool IsFrozen => statusController != null && statusController.IsFrozen;
    public float CurrentHealth => currentHealth.Value;
    public float MaxHealth => IsSpawned&&replicatedMaxHealth.Value>0?replicatedMaxHealth.Value:maxHealth;
    public int LaneId => IsSpawned ? replicatedLane.Value : laneId;
    public float Difficulty => difficulty;
    public int ContentSeed => contentSeed;
    public EnemyArchetype Archetype => IsSpawned ? (EnemyArchetype)replicatedArchetype.Value : archetype;
    public EnemyAIState State => IsSpawned ? (EnemyAIState)replicatedState.Value : aiState;
    public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position + Vector3.up * .85f;
    public Vector3 MuzzlePosition => transform.position + Vector3.up * .9f + Vector3.back * .7f;
    public string ArchetypeLabel => Archetype switch
    {
        EnemyArchetype.Strafer => "扇射手", EnemyArchetype.Charger => "狙击手",
        EnemyArchetype.Ranged => "连射手", _ => "驻守卫兵"
    };

    private void Awake()
    {
        statusController = GetComponent<StatusController>();
        if (visualRoot == null) visualRoot = transform.Find("Visual");
        visualRenderers = (visualRoot != null ? visualRoot : transform).GetComponentsInChildren<Renderer>(true);
        visualProperties = new MaterialPropertyBlock();
        attackTelegraph = gameObject.AddComponent<RunnerEnemyTelegraph>();
        gameObject.AddComponent<RunnerEnemyStatusView>();
        // Defender bodies can be shot but can never stop the runner.
        foreach (var body in GetComponentsInChildren<Collider>(true))
            if (!(body is CharacterController)) body.isTrigger = true;
    }

    public void ConfigureServer(int seed, int configuredLaneId, float configuredDifficulty, EnemyArchetype configuredArchetype)
    {
        contentSeed = seed;
        laneId = configuredLaneId;
        difficulty = Mathf.Max(.25f, configuredDifficulty);
        archetype = configuredArchetype;
        var scope = GetComponent<LaneScope>() ?? gameObject.AddComponent<LaneScope>();
        scope.Configure(laneId);
        ApplyDifficultyOnce();
        if (IsSpawned && IsServer)
        {
            replicatedArchetype.Value = (byte)archetype;
            replicatedLane.Value = laneId;
            currentHealth.Value = maxHealth;
            replicatedMaxHealth.Value=maxHealth;
        }
    }

    public override void OnNetworkSpawn()
    {
        activeEnemies.Add(this);
        RunnerEnemyProjectileSystem.Ensure();
        replicatedArchetype.OnValueChanged += HandleVisualArchetypeChanged;
        replicatedState.OnValueChanged += HandleVisualStateChanged;
        replicatedLane.OnValueChanged += HandleLaneChanged;
        if (IsServer)
        {
            ApplyDifficultyOnce();
            replicatedArchetype.Value = (byte)archetype;
            replicatedLane.Value = laneId;
            currentHealth.Value = maxHealth;
            replicatedMaxHealth.Value=maxHealth;
            dead.Value = false;
            // Different defenders do not start every warning on the same frame.
            nextAttackTime = Time.time + .2f + (contentSeed & 255) / 255f * .55f;
            SetState(EnemyAIState.Acquire, 0);
            ServerSpawned?.Invoke(this);
        }
        HandleLaneChanged(-1, LaneId);
        FaceForward();
        ApplyVisualFeedback(true);
    }

    public override void OnNetworkDespawn()
    {
        activeEnemies.Remove(this);
        replicatedArchetype.OnValueChanged -= HandleVisualArchetypeChanged;
        replicatedState.OnValueChanged -= HandleVisualStateChanged;
        replicatedLane.OnValueChanged -= HandleLaneChanged;
    }

    public override void OnDestroy() { activeEnemies.Remove(this); base.OnDestroy(); }

    public ReactionResult ApplyElement(ElementType element, float amount = 1, float duration = -1, float sourceDamage = 0)
        => statusController == null ? ReactionResult.None : statusController.ApplyElement(element, amount, duration, sourceDamage);

    private void Update()
    {
        ApplyVisualFeedback(false);
        UpdateTelegraph();
        if (!IsServer || dead.Value) return;
        if (IsFrozen)
        {
            // Freeze interrupts the windup; thawing requires a fresh warning.
            if (aiState == EnemyAIState.Telegraph) SetState(EnemyAIState.Recover, .25f);
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + .35f);
            return;
        }
        if (Time.time >= nextTargetRefresh || !IsTargetValid())
        {
            nextTargetRefresh = Time.time + .2f;
            FindApproachingRunner();
        }
        if (!IsTargetValid())
        {
            SetState(EnemyAIState.Acquire, 0);
            return;
        }
        switch (aiState)
        {
            case EnemyAIState.Telegraph:
                if (Time.time >= stateEndsAt) FireLockedAttack();
                break;
            case EnemyAIState.Recover:
                if (Time.time >= stateEndsAt) SetState(EnemyAIState.Approach, 0);
                break;
            default:
                if (Time.time >= nextAttackTime) BeginTelegraph();
                break;
        }
    }

    private void BeginTelegraph()
    {
        lockedDirection.Value = RunnerEnemyAttackPattern.LockDirection(archetype, MuzzlePosition, targetRunner.transform.position);
        float duration = Mathf.Max(.65f, telegraphDuration) + (archetype == EnemyArchetype.Charger ? .4f : 0);
        warningEndsAt.Value = NetworkManager.ServerTime.Time + duration;
        SetState(EnemyAIState.Telegraph, duration);
    }

    private void FireLockedAttack()
    {
        if (!IsTargetValid()) { SetState(EnemyAIState.Acquire, 0); return; }
        float speed = RunnerEnemyAttackPattern.ProjectileSpeed(archetype, difficulty);
        int count = RunnerEnemyAttackPattern.ProjectileCount(archetype);
        int damage = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(1, populationDamage) * 8 *
            Mathf.Lerp(1, 1.5f, Mathf.InverseLerp(1, 4, difficulty))), 6, 12);
        double now = NetworkManager.ServerTime.Time;
        var system = RunnerEnemyProjectileSystem.Ensure();
        for (int index = 0; index < count; index++)
        {
            Vector3 direction = RunnerEnemyAttackPattern.ShotDirection(archetype, lockedDirection.Value, index);
            double launch = now + (archetype == EnemyArchetype.Ranged ? index * .18 : 0);
            system.SpawnServer(this, MuzzlePosition, direction, speed, damage, .15f, 4.5f,
                laneId, targetRunner.LaneCenterX, launch);
        }
        nextAttackTime = Time.time + Mathf.Max(1.4f, attackInterval + .9f) +
            (archetype == EnemyArchetype.Ranged ? .5f : 0);
        SetState(EnemyAIState.Recover, Mathf.Max(.3f, recoverDuration));
    }

    private void FindApproachingRunner()
    {
        targetRunner = null;
        if (NetworkManager == null) return;
        float best = float.PositiveInfinity;
        foreach (var pair in NetworkManager.ConnectedClients)
        {
            var pawn = pair.Value.PlayerObject;
            if (pawn == null || !pawn.IsSpawned) continue;
            var runner = pawn.GetComponent<RunnerController>();
            var stats = pawn.GetComponent<CrowdManager>();
            if (runner == null || stats == null || stats.Health <= 0 ||
                (laneId >= 0 && runner.LaneId != laneId) || !IsMovingPhase(runner.Phase)) continue;
            float distance = transform.position.z - runner.transform.position.z;
            if (!RunnerEnemyAttackPattern.CanEngage(distance) || distance >= best) continue;
            best = distance; targetRunner = runner;
        }
    }

    private bool IsTargetValid()
    {
        if (targetRunner == null || !targetRunner.IsSpawned || !IsMovingPhase(targetRunner.Phase) ||
            (laneId >= 0 && targetRunner.LaneId != laneId)) return false;
        var stats = targetRunner.GetComponent<CrowdManager>();
        return stats != null && stats.Health > 0 &&
            RunnerEnemyAttackPattern.CanEngage(transform.position.z - targetRunner.transform.position.z);
    }

    private static bool IsMovingPhase(PlayerRunPhase phase)
        => phase == PlayerRunPhase.Running || phase == PlayerRunPhase.Combat;

    private void FaceForward()
    {
        var facing = visualRoot != null ? visualRoot : transform;
        facing.rotation = Quaternion.LookRotation(Vector3.back);
    }

    private void UpdateTelegraph()
    {
        bool visible = IsSpawned && !IsDead && !IsFrozen && State == EnemyAIState.Telegraph;
        attackTelegraph.Show(visible, MuzzlePosition, lockedDirection.Value, Archetype,
            Mathf.Max(0, (float)(warningEndsAt.Value - (NetworkManager != null ? NetworkManager.ServerTime.Time : Time.timeAsDouble))));
    }

    private void SetState(EnemyAIState state, float duration)
    {
        if (aiState == state && duration <= 0) return;
        aiState = state; stateEndsAt = Time.time + duration;
        if (IsServer) replicatedState.Value = (byte)state;
    }

    private void HandleVisualArchetypeChanged(byte previous, byte current) { ApplyVisualFeedback(true); }
    private void HandleVisualStateChanged(byte previous, byte current) { ApplyVisualFeedback(true); }
    private void HandleLaneChanged(int previous, int current)
    {
        var scope = GetComponent<LaneScope>() ?? gameObject.AddComponent<LaneScope>();
        scope.Configure(current);
    }
    private void ApplyVisualFeedback(bool force)
    {
        if (visualRenderers == null) return;
        var kind = Archetype; var state = State;
        bool pulse = state == EnemyAIState.Telegraph;
        if (!force && !pulse && lastVisualArchetype == kind && lastVisualState == state) return;
        lastVisualArchetype = kind; lastVisualState = state;
        Color tint = kind switch
        {
            EnemyArchetype.Strafer => new Color(.42f, .53f, .57f),
            EnemyArchetype.Charger => new Color(.71f, .44f, .32f),
            EnemyArchetype.Ranged => new Color(.52f, .47f, .58f), _ => new Color(.64f, .36f, .31f)
        };
        if (pulse) tint = Color.Lerp(tint, new Color(.96f, .82f, .56f), .5f + Mathf.Sin(Time.unscaledTime * 14) * .3f);
        else if (state == EnemyAIState.Recover) tint = Color.Lerp(tint, Color.black, .15f);
        foreach (var renderer in visualRenderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(visualProperties);
            visualProperties.SetColor("_BaseColor", tint); visualProperties.SetColor("_Color", tint);
            renderer.SetPropertyBlock(visualProperties);
        }
    }

    private void ApplyDifficultyOnce()
    {
        if (difficultyApplied) return;
        difficultyApplied = true;
        var random=new ProceduralRandom(ProceduralRandom.Derive(contentSeed,441,laneId));
        float role=archetype==EnemyArchetype.Charger?1.25f:archetype==EnemyArchetype.Ranged?1.1f:1;
        maxHealth*=Mathf.Max(.5f,.95f+.35f*Mathf.Pow(difficulty,1.08f))*random.Range(.9f,1.15f)*role;
        attackInterval*=random.Range(.9f,1.15f);
        populationDamage = Mathf.Max(1, populationDamage);
    }

    public void TakeDamage(float damage, ElementType element)
    {
        if (!IsServer || dead.Value || !ArenaProtocol.IsFinite(damage) || damage <= 0) return;
        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);
        DamageFeedbackClientRpc(damage, element);
        if (currentHealth.Value <= 0) Die();
    }

    [ClientRpc]
    private void DamageFeedbackClientRpc(float damage, ElementType element)
    {
        Damaged?.Invoke(damage, element);
        if (visualAnimator != null) visualAnimator.SetTrigger("Hit");
    }

    private void Die()
    {
        if (!IsServer || dead.Value) return;
        dead.Value = true;
        // Drop listeners see the exact stationary position before despawn.
        Died?.Invoke(this);
        if (NetworkObject != null && NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }
}
