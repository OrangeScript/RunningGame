using System;
using Unity.Netcode;
using UnityEngine;

public readonly struct ArenaDamageInfo
{
    public readonly float Amount;
    public readonly int SourceTeamId;
    public readonly ulong SourceClientId;
    public readonly Vector3 HitPoint;
    public readonly Vector3 Direction;
    public readonly ElementType Element;

    public ArenaDamageInfo(
        float amount,
        int sourceTeamId,
        ulong sourceClientId,
        Vector3 hitPoint,
        Vector3 direction,
        ElementType element = ElementType.None)
    {
        Amount = amount;
        SourceTeamId = sourceTeamId;
        SourceClientId = sourceClientId;
        HitPoint = hitPoint;
        Direction = direction;
        Element = element;
    }
}

/// <summary>
/// Implement on boss, player, or destructible components that can receive
/// authoritative Arena damage.
/// </summary>
public interface IArenaDamageable
{
    int ArenaTeamId { get; }
    bool IsArenaAlive { get; }
    void ApplyArenaDamage(ArenaDamageInfo damage);
}

public struct ArenaProjectileState :
    INetworkSerializable,
    IEquatable<ArenaProjectileState>
{
    public bool Initialized;
    public Vector3 Direction;
    public float Speed;
    public float Lifetime;
    public float Damage;
    public float SizeMultiplier;
    public int TeamId;
    public ulong SourceClientId;
    public double SpawnServerTime;
    public ElementType Element;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref Initialized);
        serializer.SerializeValue(ref Direction);
        serializer.SerializeValue(ref Speed);
        serializer.SerializeValue(ref Lifetime);
        serializer.SerializeValue(ref Damage);
        serializer.SerializeValue(ref SizeMultiplier);
        serializer.SerializeValue(ref TeamId);
        serializer.SerializeValue(ref SourceClientId);
        serializer.SerializeValue(ref SpawnServerTime);
        serializer.SerializeValue(ref Element);
    }

    public bool Equals(ArenaProjectileState other)
    {
        return Initialized == other.Initialized &&
               Direction.Equals(other.Direction) &&
               Speed.Equals(other.Speed) &&
               Lifetime.Equals(other.Lifetime) &&
               Damage.Equals(other.Damage) &&
               SizeMultiplier.Equals(other.SizeMultiplier) &&
               TeamId == other.TeamId &&
               SourceClientId == other.SourceClientId &&
               SpawnServerTime.Equals(other.SpawnServerTime) && Element==other.Element;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class ArenaProjectile : NetworkBehaviour
{
    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField, Min(0.01f)] private float hitRadius = 0.15f;
    [SerializeField] private bool destroyOnEnvironment = true;
    [SerializeField] private bool friendlyFire;

    [Header("Presentation")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Disable this if a NetworkTransform drives projectile position.")]
    [SerializeField] private bool simulateOnClients = true;

    private readonly NetworkVariable<ArenaProjectileState> projectileState =
        new NetworkVariable<ArenaProjectileState>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly RaycastHit[] castHits = new RaycastHit[16];
    private ArenaProjectileState preparedState;
    private bool hasPreparedState;
    private bool consumed;
    private Vector3 baseVisualScale;

    public int TeamId => projectileState.Value.TeamId;
    public ulong SourceClientId => projectileState.Value.SourceClientId;
    public float Damage => projectileState.Value.Damage;

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        baseVisualScale = visualRoot.localScale;
    }

    /// <summary>
    /// Call on the server before NetworkObject.Spawn. OnNetworkSpawn publishes
    /// the prepared state as the projectile's initial authoritative state.
    /// </summary>
    public void PrepareServer(
        Vector3 position,
        Vector3 direction,
        float speed,
        float lifetime,
        float damage,
        float sizeMultiplier,
        int teamId,
        ulong sourceClientId,
        ElementType element=ElementType.None)
    {
        direction.y = 0f;
        if (!ArenaProtocol.IsFinite(direction) || direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.forward;
        }

        transform.position = ArenaProtocol.IsFinite(position) ? position : Vector3.zero;
        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        preparedState = new ArenaProjectileState
        {
            Initialized = true,
            Direction = direction.normalized,
            Speed = SanitizePositive(speed, 18f, 100f),
            Lifetime = SanitizePositive(lifetime, 3f, 30f),
            Damage = SanitizePositive(damage, 1f, 100000f),
            SizeMultiplier = SanitizePositive(sizeMultiplier, 1f, 20f),
            TeamId = teamId,
            SourceClientId = sourceClientId,
            SpawnServerTime = GetServerTime(),
            Element=element
        };

        hasPreparedState = true;
    }

    public override void OnNetworkSpawn()
    {
        projectileState.OnValueChanged += HandleStateChanged;

        if (IsServer)
        {
            if (!hasPreparedState)
            {
                preparedState = new ArenaProjectileState
                {
                    Initialized = true,
                    Direction = transform.forward.sqrMagnitude > 0.0001f
                        ? Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized
                        : Vector3.forward,
                    Speed = 18f,
                    Lifetime = 3f,
                    Damage = 1f,
                    SizeMultiplier = 1f,
                    TeamId = 0,
                    SourceClientId = NetworkManager.ServerClientId,
                    SpawnServerTime = GetServerTime()
                };
            }

            projectileState.Value = preparedState;
        }

        ApplyState(projectileState.Value);
    }

    public override void OnNetworkDespawn()
    {
        projectileState.OnValueChanged -= HandleStateChanged;
    }

    private void Update()
    {
        ArenaProjectileState state = projectileState.Value;
        if (!state.Initialized || consumed)
        {
            return;
        }

        if (IsServer)
        {
            if (GetServerTime() >= state.SpawnServerTime + state.Lifetime)
            {
                Consume();
                return;
            }

            StepServer(state, Time.deltaTime);
        }
        else if (simulateOnClients)
        {
            transform.position += state.Direction * state.Speed * Time.deltaTime;
        }
    }

    private void StepServer(ArenaProjectileState state, float deltaTime)
    {
        float distance = Mathf.Max(0f, state.Speed * Mathf.Max(0f, deltaTime));
        if (distance <= 0f)
        {
            return;
        }

        Vector3 start = transform.position;
        int hitCount = Physics.SphereCastNonAlloc(
            start,
            Mathf.Max(0.01f, hitRadius),
            state.Direction,
            castHits,
            distance,
            collisionMask,
            QueryTriggerInteraction.Collide);

        RaycastHit? nearest = null;
        IArenaDamageable nearestDamageable = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = castHits[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            IArenaDamageable damageable = FindDamageable(hitCollider);
            if (damageable != null)
            {
                if (!damageable.IsArenaAlive)
                {
                    continue;
                }

                if (!friendlyFire && damageable.ArenaTeamId == state.TeamId)
                {
                    continue;
                }
            }
            else if (!destroyOnEnvironment)
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearest = hit;
                nearestDamageable = damageable;
                nearestDistance = hit.distance;
            }
        }

        if (nearest.HasValue)
        {
            RaycastHit hit = nearest.Value;
            transform.position = hit.point;
            ApplyHit(nearestDamageable, hit.point, state);
            return;
        }

        transform.position = start + state.Direction * distance;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || consumed || other == null || other.transform.IsChildOf(transform))
        {
            return;
        }

        ArenaProjectileState state = projectileState.Value;
        if (!state.Initialized)
        {
            return;
        }

        IArenaDamageable damageable = FindDamageable(other);
        if (damageable != null)
        {
            if (!damageable.IsArenaAlive ||
                (!friendlyFire && damageable.ArenaTeamId == state.TeamId))
            {
                return;
            }

            ApplyHit(damageable, transform.position, state);
        }
        else if (destroyOnEnvironment)
        {
            Consume();
        }
    }

    private void ApplyHit(
        IArenaDamageable damageable,
        Vector3 hitPoint,
        ArenaProjectileState state)
    {
        if (damageable != null)
        {
            damageable.ApplyArenaDamage(new ArenaDamageInfo(
                state.Damage,
                state.TeamId,
                state.SourceClientId,
                hitPoint,
                state.Direction,
                state.Element));
        }

        Consume();
    }

    private void Consume()
    {
        if (consumed)
        {
            return;
        }

        consumed = true;
        if (IsServer && IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void HandleStateChanged(
        ArenaProjectileState previous,
        ArenaProjectileState current)
    {
        ApplyState(current);
    }

    private void ApplyState(ArenaProjectileState state)
    {
        if (!state.Initialized)
        {
            return;
        }

        Vector3 direction = state.Direction;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        if (visualRoot != null)
        {
            visualRoot.localScale = baseVisualScale * state.SizeMultiplier;
        }
    }

    private static IArenaDamageable FindDamageable(Collider hitCollider)
    {
        MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IArenaDamageable damageable)
            {
                return damageable;
            }
        }

        return null;
    }

    private static float SanitizePositive(float value, float fallback, float maximum)
    {
        if (!ArenaProtocol.IsFinite(value) || value <= 0f)
        {
            return fallback;
        }

        return Mathf.Min(value, maximum);
    }

    private double GetServerTime()
    {
        if (NetworkManager != null && NetworkManager.IsListening)
        {
            return NetworkManager.ServerTime.Time;
        }

        return Time.unscaledTimeAsDouble;
    }

    private void OnValidate()
    {
        hitRadius = Mathf.Max(0.01f, hitRadius);
    }
}
