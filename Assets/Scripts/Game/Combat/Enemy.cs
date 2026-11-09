using System;
using Unity.Netcode;
using UnityEngine;

public enum EnemyArchetype : byte
{
    Chaser,
    Strafer,
    Charger,
    Ranged
}

public enum EnemyAIState : byte
{
    Acquire,
    Approach,
    Telegraph,
    Attack,
    Recover
}

/// <summary>
/// Server-authoritative enemy state machine. A single prefab can be configured
/// into four readable behaviours by CombatEncounter's seeded spawn budget.
/// </summary>
public class Enemy : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 8f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
    [SerializeField, Min(0.1f)] private float attackDistance = 1.5f;
    [SerializeField, Min(0.1f)] private float preferredRangedDistance = 6f;
    [SerializeField, Min(0.1f)] private float chargeTriggerDistance = 8f;
    [SerializeField, Min(0.1f)] private float chargeSpeed = 9f;

    [Header("Attack")]
    [SerializeField, Min(1)] private int populationDamage = 1;
    [SerializeField, Min(0.1f)] private float attackInterval = 0.8f;
    [SerializeField, Min(0.05f)] private float telegraphDuration = 0.45f;
    [SerializeField, Min(0.05f)] private float recoverDuration = 0.45f;
    [SerializeField, Min(0.05f)] private float chargeDuration = 0.75f;

    [Header("Target")]
    [SerializeField] private Transform aimPoint;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator visualAnimator;

    private readonly NetworkVariable<float> currentHealth = new NetworkVariable<float>();
    private readonly NetworkVariable<bool> dead = new NetworkVariable<bool>();
    private readonly NetworkVariable<byte> replicatedArchetype = new NetworkVariable<byte>();
    private readonly NetworkVariable<byte> replicatedState = new NetworkVariable<byte>();

    private Transform player;
    private CrowdManager crowd;
    private RunnerController targetRunner;
    private StatusController statusController;
    private EnemyArchetype archetype;
    private EnemyAIState aiState = EnemyAIState.Acquire;
    private int laneId = -1;
    private float difficulty = 1f;
    private float nextAttackTime;
    private float stateEndsAt;
    private float nextTargetRefresh;
    private float strafeSign = 1f;
    private Vector3 chargeDirection;
    private bool chargeHit;
    private bool difficultyApplied;
    private Renderer[] visualRenderers;
    private MaterialPropertyBlock visualProperties;
    private EnemyArchetype lastVisualArchetype = (EnemyArchetype)byte.MaxValue;
    private EnemyAIState lastVisualState = (EnemyAIState)byte.MaxValue;

    public bool IsDead => dead.Value;
    public float CurrentHealth => currentHealth.Value;
    public int LaneId => laneId;
    public EnemyArchetype Archetype => IsSpawned
        ? (EnemyArchetype)replicatedArchetype.Value
        : archetype;
    public EnemyAIState State => IsSpawned
        ? (EnemyAIState)replicatedState.Value
        : aiState;

    public event Action<float, ElementType> Damaged;
    public event Action<Enemy> Died;

    public Vector3 AimPosition => aimPoint != null
        ? aimPoint.position
        : transform.position + Vector3.up * 0.8f;

    private void Awake()
    {
        statusController = GetComponent<StatusController>();
        if (visualRoot == null)
            visualRoot = transform.Find("Visual");

        Transform rendererRoot = visualRoot != null ? visualRoot : transform;
        visualRenderers = rendererRoot.GetComponentsInChildren<Renderer>(true);
        visualProperties = new MaterialPropertyBlock();
    }

    public void ConfigureServer(
        int seed,
        int configuredLaneId,
        float configuredDifficulty,
        EnemyArchetype configuredArchetype)
    {
        laneId = configuredLaneId;
        difficulty = Mathf.Max(0.25f, configuredDifficulty);
        archetype = configuredArchetype;
        strafeSign = (seed & 1) == 0 ? -1f : 1f;

        LaneScope scope = GetComponent<LaneScope>();
        if (scope == null)
            scope = gameObject.AddComponent<LaneScope>();
        scope.Configure(laneId);

        ApplyDifficultyOnce();
        if (IsSpawned && IsServer)
        {
            replicatedArchetype.Value = (byte)archetype;
            currentHealth.Value = maxHealth;
            SetState(EnemyAIState.Acquire, 0f);
        }
    }

    public override void OnNetworkSpawn()
    {
        replicatedArchetype.OnValueChanged += HandleVisualArchetypeChanged;
        replicatedState.OnValueChanged += HandleVisualStateChanged;

        if (!IsServer)
        {
            ApplyVisualFeedback(true);
            return;
        }

        ApplyDifficultyOnce();
        replicatedArchetype.Value = (byte)archetype;
        currentHealth.Value = maxHealth;
        dead.Value = false;
        FindNearestPlayer();
        SetState(player != null ? EnemyAIState.Approach : EnemyAIState.Acquire, 0f);
        ApplyVisualFeedback(true);
    }

    public override void OnNetworkDespawn()
    {
        replicatedArchetype.OnValueChanged -= HandleVisualArchetypeChanged;
        replicatedState.OnValueChanged -= HandleVisualStateChanged;
    }

    public ReactionResult ApplyElement(
        ElementType element,
        float amount = 1f,
        float duration = -1f,
        float sourceDamage = 0f)
    {
        return statusController == null
            ? ReactionResult.None
            : statusController.ApplyElement(element, amount, duration, sourceDamage);
    }

    private void Update()
    {
        ApplyVisualFeedback(false);

        if (!IsServer || dead.Value)
            return;
        if (statusController != null && statusController.IsFrozen)
            return;

        if (Time.time >= nextTargetRefresh || !IsTargetValid())
        {
            nextTargetRefresh = Time.time + 0.6f;
            FindNearestPlayer();
        }

        if (!IsTargetValid())
        {
            SetState(EnemyAIState.Acquire, 0f);
            return;
        }

        Vector3 toTarget = player.position - transform.position;
        toTarget.y = 0f;

        switch (aiState)
        {
            case EnemyAIState.Acquire:
                SetState(EnemyAIState.Approach, 0f);
                break;
            case EnemyAIState.Approach:
                UpdateApproach(toTarget);
                break;
            case EnemyAIState.Telegraph:
                Face(toTarget);
                if (Time.time >= stateEndsAt)
                    ExecuteTelegraphedAttack(toTarget);
                break;
            case EnemyAIState.Attack:
                UpdateChargeAttack();
                break;
            case EnemyAIState.Recover:
                if (Time.time >= stateEndsAt)
                    SetState(EnemyAIState.Approach, 0f);
                break;
        }
    }

    private void UpdateApproach(Vector3 toTarget)
    {
        float distance = toTarget.magnitude;
        bool attackReady = Time.time >= nextAttackTime;

        switch (archetype)
        {
            case EnemyArchetype.Strafer:
                MoveStrafing(toTarget, distance, preferredRangedDistance * 0.65f);
                if (attackReady && distance <= preferredRangedDistance)
                    BeginTelegraph(toTarget);
                break;
            case EnemyArchetype.Charger:
                if (attackReady && distance <= chargeTriggerDistance)
                    BeginTelegraph(toTarget);
                else
                    MoveTowards(toTarget, moveSpeed);
                break;
            case EnemyArchetype.Ranged:
                MoveStrafing(toTarget, distance, preferredRangedDistance);
                if (attackReady && distance <= preferredRangedDistance * 1.35f)
                    BeginTelegraph(toTarget);
                break;
            default:
                if (attackReady && distance <= attackDistance)
                    BeginTelegraph(toTarget);
                else
                    MoveTowards(toTarget, moveSpeed);
                break;
        }
    }

    private void BeginTelegraph(Vector3 toTarget)
    {
        chargeDirection = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : transform.forward;
        SetState(EnemyAIState.Telegraph, telegraphDuration);
    }

    private void ExecuteTelegraphedAttack(Vector3 toTarget)
    {
        if (archetype == EnemyArchetype.Charger)
        {
            chargeDirection = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : chargeDirection;
            chargeHit = false;
            SetState(EnemyAIState.Attack, chargeDuration);
            return;
        }

        float allowedDistance = archetype == EnemyArchetype.Chaser
            ? attackDistance * 1.35f
            : preferredRangedDistance * 1.5f;
        if (toTarget.sqrMagnitude <= allowedDistance * allowedDistance)
            DamageTarget();

        nextAttackTime = Time.time + attackInterval;
        SetState(EnemyAIState.Recover, recoverDuration);
    }

    private void UpdateChargeAttack()
    {
        transform.position += chargeDirection * (chargeSpeed * Time.deltaTime);
        Face(chargeDirection);

        if (!chargeHit && IsTargetValid())
        {
            Vector3 delta = player.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= attackDistance * attackDistance * 1.8f)
            {
                chargeHit = true;
                DamageTarget();
            }
        }

        if (Time.time >= stateEndsAt)
        {
            nextAttackTime = Time.time + attackInterval;
            SetState(EnemyAIState.Recover, recoverDuration * 1.4f);
        }
    }

    private void DamageTarget()
    {
        if (IsTargetValid())
            crowd.DamageHealth(Mathf.Max(1, populationDamage) * 6);
    }

    private void MoveStrafing(Vector3 toTarget, float distance, float desiredDistance)
    {
        if (toTarget.sqrMagnitude < 0.001f)
            return;

        Vector3 forward = toTarget.normalized;
        Vector3 lateral = Vector3.Cross(Vector3.up, forward) * strafeSign;
        Vector3 movement = lateral;

        if (distance > desiredDistance + 0.75f)
            movement += forward * 0.8f;
        else if (distance < desiredDistance - 0.75f)
            movement -= forward * 0.8f;

        MoveTowards(movement, moveSpeed * 0.85f);
        Face(forward);
    }

    private void MoveTowards(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude < 0.001f)
            return;

        Vector3 normalized = direction.normalized;
        transform.position += normalized * (speed * Time.deltaTime);
        Face(normalized);
    }

    private void Face(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        Transform facingTarget = visualRoot != null ? visualRoot : transform;
        facingTarget.rotation = Quaternion.LookRotation(direction.normalized);
    }

    private void FindNearestPlayer()
    {
        player = null;
        crowd = null;
        targetRunner = null;

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null)
            return;

        float nearestSqr = Mathf.Infinity;
        foreach (var pair in manager.ConnectedClients)
        {
            NetworkObject playerObject = pair.Value.PlayerObject;
            if (playerObject == null || !playerObject.IsSpawned)
                continue;

            RunnerController candidateRunner = playerObject.GetComponent<RunnerController>();
            CrowdManager candidateCrowd = playerObject.GetComponent<CrowdManager>();
            if (candidateRunner == null || candidateCrowd == null || candidateCrowd.Health <= 0)
                continue;
            if (laneId >= 0 && candidateRunner.LaneId != laneId)
                continue;
            if (candidateRunner.Phase == PlayerRunPhase.Eliminated)
                continue;

            float sqr = (playerObject.transform.position - transform.position).sqrMagnitude;
            if (sqr >= nearestSqr)
                continue;

            nearestSqr = sqr;
            player = playerObject.transform;
            crowd = candidateCrowd;
            targetRunner = candidateRunner;
        }
    }

    private bool IsTargetValid()
    {
        return player != null &&
               crowd != null &&
               targetRunner != null &&
               crowd.Population > 0 &&
               targetRunner.Phase != PlayerRunPhase.Eliminated &&
               (laneId < 0 || targetRunner.LaneId == laneId);
    }

    private void SetState(EnemyAIState state, float duration)
    {
        aiState = state;
        stateEndsAt = duration > 0f ? Time.time + duration : 0f;
        if (IsServer)
            replicatedState.Value = (byte)state;
    }

    private void HandleVisualArchetypeChanged(byte previous, byte current)
    {
        ApplyVisualFeedback(true);
    }

    private void HandleVisualStateChanged(byte previous, byte current)
    {
        ApplyVisualFeedback(true);
    }

    /// <summary>
    /// Makes the four behaviours and their telegraph windows readable without
    /// requiring bespoke enemy prefabs. MaterialPropertyBlock keeps this local
    /// to the enemy instance and avoids allocating duplicate materials.
    /// </summary>
    private void ApplyVisualFeedback(bool force)
    {
        if (visualRenderers == null || visualRenderers.Length == 0)
            return;

        EnemyArchetype visualArchetype = Archetype;
        EnemyAIState visualState = State;
        bool pulsing = visualState == EnemyAIState.Telegraph;
        if (!force && !pulsing &&
            lastVisualArchetype == visualArchetype &&
            lastVisualState == visualState)
        {
            return;
        }

        lastVisualArchetype = visualArchetype;
        lastVisualState = visualState;

        Color baseColor = visualArchetype switch
        {
            EnemyArchetype.Strafer => new Color(.42f,.53f,.57f),
            EnemyArchetype.Charger => new Color(.71f,.44f,.32f),
            EnemyArchetype.Ranged => new Color(.52f,.47f,.58f),
            _ => new Color(.64f,.36f,.31f)
        };

        Color displayColor = baseColor;
        switch (visualState)
        {
            case EnemyAIState.Telegraph:
                float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 18f) * 0.5f;
                displayColor = Color.Lerp(baseColor, new Color(.96f, .82f, .56f), pulse);
                break;
            case EnemyAIState.Attack:
                displayColor = Color.Lerp(baseColor, Color.white, 0.55f);
                break;
            case EnemyAIState.Recover:
                displayColor = Color.Lerp(baseColor, Color.black, 0.35f);
                break;
        }

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            Renderer targetRenderer = visualRenderers[i];
            if (targetRenderer == null)
                continue;

            targetRenderer.GetPropertyBlock(visualProperties);
            visualProperties.SetColor("_BaseColor", displayColor);
            visualProperties.SetColor("_Color", displayColor);
            targetRenderer.SetPropertyBlock(visualProperties);
        }
    }

    private void ApplyDifficultyOnce()
    {
        if (difficultyApplied)
            return;

        difficultyApplied = true;
        float scalar = Mathf.Max(0.25f, difficulty);
        maxHealth *= Mathf.Lerp(0.8f, 1.75f, Mathf.InverseLerp(0.5f, 4f, scalar));
        populationDamage = Mathf.Max(1, Mathf.RoundToInt(populationDamage * Mathf.Lerp(1f, 2.2f, Mathf.InverseLerp(1f, 4f, scalar))));
        moveSpeed *= Mathf.Lerp(0.9f, 1.25f, Mathf.InverseLerp(1f, 4f, scalar));
    }

    public void TakeDamage(float damage, ElementType element)
    {
        if (!IsServer || dead.Value || damage <= 0f)
            return;

        currentHealth.Value -= damage;
        DamageFeedbackClientRpc(damage, element);
        if (currentHealth.Value <= 0f)
            Die();
    }

    [ClientRpc]
    private void DamageFeedbackClientRpc(float damage, ElementType element)
    {
        Damaged?.Invoke(damage, element);
        if (visualAnimator != null)
            visualAnimator.SetTrigger("Hit");
    }

    private void Die()
    {
        if (!IsServer || dead.Value)
            return;

        dead.Value = true;
        Died?.Invoke(this);

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
        else
            Destroy(gameObject);
    }
}
