using Unity.Netcode;
using UnityEngine;

/// <summary>A running gun fires along world +Z, even when no target is present.
/// The owner predicts cosmetic shots. Damage is produced only by server shots.</summary>
public class AutoCombat : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private CrowdVisualizer crowdVisualizer;
    [SerializeField] private CrowdManager crowd;
    [SerializeField] private Transform firePoint;
    [SerializeField] private PlayerStats stats;
    [SerializeField] private ItemManager itemManager;
    [SerializeField] private ProjectileArchetype archetype;
    [SerializeField] private LayerMask enemyLayer;

    private RunnerController runner;
    private AttackPlan volley;
    private int volleyIndex;
    private int volleyCount;
    private int elementSequence;
    private float nextVolleyTime;
    private float nextShotTime;

    public int FiredShotCount { get; private set; }
    public Vector3 LastShotDirection { get; private set; } = Vector3.forward;
    public Vector3 LastMuzzlePosition { get; private set; }

    private void Awake()
    {
        runner = GetComponent<RunnerController>();
        if (crowd == null) crowd = GetComponent<CrowdManager>();
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (itemManager == null) itemManager = GetComponent<ItemManager>();
    }

    private bool CanFire => IsSpawned && (IsServer || IsOwner) && crowd != null &&
        crowd.Health > 0 && stats != null && itemManager != null && archetype != null &&
        archetype.ProjectilePrefab != null && (runner == null ||
        runner.Phase == PlayerRunPhase.Running || runner.Phase == PlayerRunPhase.Combat);

    private void Update()
    {
        if (!CanFire)
        {
            volleyCount = 0;
            nextVolleyTime = 0;
            return;
        }

        float now = Time.time;
        if (volleyCount == 0)
        {
            if (now < nextVolleyTime) return;
            volley = CreateAttackPlan();
            volleyIndex = 0;
            volleyCount = Mathf.Clamp(volley.projectileCount, 1, 12);
            nextShotTime = now;
            nextVolleyTime = now + Mathf.Clamp(volley.attackInterval, .1f, 3f);
        }

        // Bounded catch-up preserves burst spacing after a slow frame without
        // starting coroutines that could outlive a map or a dead player.
        while (volleyIndex < volleyCount && now >= nextShotTime)
        {
            FireShot(volleyIndex, volleyCount, volley);
            volleyIndex++;
            nextShotTime += Mathf.Clamp(volley.shotSpacing, 0, .15f);
        }
        if (volleyIndex >= volleyCount) volleyCount = 0;
    }

    private AttackPlan CreateAttackPlan()
    {
        var plan = stats.CreateAttackPlan(crowd.Firepower, archetype);
        // The old 15m targeting range left very little time to engage a
        // stationary sentry. Keep range upgrades meaningful above this base.
        plan.attackRange = Mathf.Clamp(plan.attackRange + 18, 18, 60);
        itemManager.ModifyAttack(plan);
        return plan;
    }

    private void FireShot(int index, int count, AttackPlan plan)
    {
        Vector3 origin = firePoint != null ? firePoint.position :
            transform.position + new Vector3(0, .9f, .8f);
        origin.x += (index - (count - 1) * .5f) * .32f;
        origin.y = Mathf.Clamp(origin.y, transform.position.y + .75f, transform.position.y + 1.15f);
        origin.z = Mathf.Max(origin.z, transform.position.z + .75f);

        float damage = Mathf.Max(0, stats.Damage * plan.damageMultiplier);
        ProjectileSpec spec = plan.projectileArchetype.CreateSpec(damage);
        spec.speed = Mathf.Max(22, spec.speed);
        itemManager.ModifyProjectile(spec);
        spec.gameplayMode = GameplayMode.Runner;
        if (spec.secondaryElement != ElementType.None && (elementSequence++ & 1) == 1)
            spec.element = spec.secondaryElement;

        LastShotDirection = Vector3.forward;
        LastMuzzlePosition = origin;
        FiredShotCount++;
        int lane = runner != null ? runner.LaneId : -1;
        Projectile projectile = Instantiate(plan.projectileArchetype.ProjectilePrefab,
            origin, Quaternion.identity);
        projectile.InitializeForward(spec, Vector3.forward, lane, OwnerClientId,
            plan.attackRange, IsServer, runner);

        if (IsServer)
            SpawnShotClientRpc(RunnerShotVisual.From(origin, lane, spec, plan.attackRange));
    }

    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    private void SpawnShotClientRpc(RunnerShotVisual shot)
    {
        // Host owns its real shot; owner already predicted its cosmetic shot.
        if (IsServer || IsOwner || archetype == null || archetype.ProjectilePrefab == null) return;
        Projectile visual = Instantiate(archetype.ProjectilePrefab, shot.Position, Quaternion.identity);
        visual.InitializeForward(shot.ToSpec(), Vector3.forward, shot.Lane, OwnerClientId,
            shot.Range, false, runner);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(.5f, .7f, .55f, .7f);
        Vector3 center = transform.position + Vector3.forward * 16.5f + Vector3.up;
        Gizmos.DrawWireCube(center, new Vector3(.5f, .3f, 33));
    }
}

/// <summary>Visual recipe only; no damage values are accepted from clients.</summary>
public struct RunnerShotVisual : INetworkSerializable
{
    public Vector3 Position;
    public int Lane, Pierce, Bounce;
    public float Speed, Size, Range, TurnRate, MaxAngle, AcquireRange, BounceRange, ExplosionRadius;
    public ElementType Element;

    public static RunnerShotVisual From(Vector3 origin, int lane, ProjectileSpec spec, float range) =>
        new RunnerShotVisual
        {
            Position = origin, Lane = lane, Speed = spec.speed, Size = spec.sizeMultiplier,
            Range = range, TurnRate = spec.homingTurnRate, MaxAngle = spec.homingMaxAngle,
            AcquireRange = spec.homingAcquireRange, Element = spec.element,
            Pierce = spec.pierce, Bounce = spec.bounce, BounceRange = spec.bounceRange,
            ExplosionRadius = spec.explosionRadius
        };

    public ProjectileSpec ToSpec() => new ProjectileSpec
    {
        damage = 0, speed = Speed, sizeMultiplier = Size, element = Element,
        homingTurnRate = TurnRate, homingMaxAngle = MaxAngle, homingAcquireRange = AcquireRange,
        pierce = Pierce, bounce = Bounce, bounceRange = BounceRange, explosionRadius = ExplosionRadius
    };

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Lane);
        serializer.SerializeValue(ref Speed);
        serializer.SerializeValue(ref Size);
        serializer.SerializeValue(ref Range);
        serializer.SerializeValue(ref TurnRate);
        serializer.SerializeValue(ref MaxAngle);
        serializer.SerializeValue(ref AcquireRange);
        serializer.SerializeValue(ref Pierce);
        serializer.SerializeValue(ref Bounce);
        serializer.SerializeValue(ref BounceRange);
        serializer.SerializeValue(ref ExplosionRadius);
        serializer.SerializeValue(ref Element);
    }
}
