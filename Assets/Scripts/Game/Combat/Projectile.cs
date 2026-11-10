using System.Collections.Generic;
using UnityEngine;

/// <summary>Road-space projectile. Straight flight is the default; every hit
/// comes from a swept collider query, never from distance to a chosen target.</summary>
public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 22f;
    [SerializeField] private float hitDistance = .2f;
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private ExplosionVFX explosionVFX;
    [SerializeField] private Renderer projectileRenderer;
    [SerializeField] private LayerMask enemyLayer;

    private static readonly HashSet<Projectile> active = new HashSet<Projectile>();
    public static IReadOnlyCollection<Projectile> ActiveProjectiles => active;
    private readonly HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
    private readonly HashSet<IRunnerProjectileObstacle> explodedObstacles = new HashSet<IRunnerProjectileObstacle>();
    private readonly List<Enemy> explosionTargets = new List<Enemy>();
    private readonly RaycastHit[] sweepHits = new RaycastHit[64];
    private readonly Collider[] overlaps = new Collider[32];
    private readonly Collider[] explosionHits = new Collider[64];
    private ProjectileSpec specification;
    private RunnerController sourceRunner;
    private Enemy homingTarget;
    private Vector3 direction = Vector3.forward;
    private Vector3 baseScale;
    private float remainingDistance, radius, expiresAt, nextTargetSearch;
    private int remainingPierces, remainingBounces;
    private int laneId = -1;
    private ulong sourceClientId;
    private bool authoritative, initialized, finished;

    public Vector3 Direction => direction;
    public bool IsHoming => specification != null && specification.homingTurnRate > 0;
    public bool IsAuthoritative => authoritative;
    public int LaneId => laneId;
    public ulong SourceClientId => sourceClientId;

    private struct Collision
    {
        public Enemy Enemy;
        public IRunnerProjectileObstacle Obstacle;
        public float Distance;
    }

    private void Awake()
    {
        baseScale = transform.localScale;
        if (projectileRenderer == null) projectileRenderer = GetComponentInChildren<Renderer>();
    }

    private void OnEnable() { active.Add(this); }
    private void OnDestroy() { active.Remove(this); }

    /// <summary>Legacy entry point retained for tools. Passing an enemy no
    /// longer makes an ordinary bullet home toward that enemy.</summary>
    public void Initialize(ProjectileSpec spec, Enemy target)
    {
        InitializeForward(spec, Vector3.forward, target != null ? target.LaneId : -1,
            0, 36, true);
    }

    public void InitializeVisual(float configuredSpeed, float sizeMultiplier,
        ElementType configuredElement, Enemy configuredTarget)
    {
        InitializeForward(new ProjectileSpec { speed = configuredSpeed, sizeMultiplier = sizeMultiplier,
            element = configuredElement }, Vector3.forward, configuredTarget != null ? configuredTarget.LaneId : -1,
            0, 36, false);
    }

    public void InitializeForward(ProjectileSpec spec, Vector3 shotDirection, int configuredLaneId,
        ulong ownerClientId, float range = 36, bool isAuthoritative = true, RunnerController owner = null)
    {
        specification = (spec ?? new ProjectileSpec()).Clone();
        specification.damage = Mathf.Clamp(specification.damage, 0, 100000);
        specification.speed = Mathf.Clamp(specification.speed, 2, 90);
        specification.homingTurnRate = Mathf.Clamp(specification.homingTurnRate, 0, 90);
        specification.homingMaxAngle = Mathf.Clamp(specification.homingMaxAngle, 0, 20);
        specification.homingAcquireRange = Mathf.Clamp(specification.homingAcquireRange, 1, 40);
        specification.obstacleDamageMultiplier = Mathf.Clamp(specification.obstacleDamageMultiplier, 0, 10);
        specification.explosionRadius = Mathf.Clamp(specification.explosionRadius, 0, 8);
        float size = Mathf.Clamp(specification.sizeMultiplier, .2f, 5);
        transform.localScale = baseScale * size;
        shotDirection.y = 0;
        direction = shotDirection.sqrMagnitude > .001f ? shotDirection.normalized : Vector3.forward;
        // Even explicit spawn recipes cannot create a backward road bullet.
        if (direction.z <= .05f) direction = Vector3.forward;
        speed = specification.speed;
        laneId = configuredLaneId; sourceClientId = ownerClientId;
        authoritative = isAuthoritative; sourceRunner = owner;
        remainingDistance = Mathf.Clamp(range, 1, 90);
        radius = Mathf.Clamp(hitDistance * size, .08f, .65f);
        remainingPierces = Mathf.Clamp(specification.pierce, 0, 12);
        remainingBounces = Mathf.Clamp(specification.bounce, 0, 6);
        expiresAt = Time.time + Mathf.Max(lifeTime, remainingDistance / speed + .2f);
        hitEnemies.Clear(); homingTarget = null; nextTargetSearch = 0;
        finished = false; initialized = true;
        transform.forward = direction;
        ApplyElementVisual(specification.element);
    }

    private void ApplyElementVisual(ElementType element)
    {
        Color tint = element switch
        {
            ElementType.Ice => new Color(.48f, .9f, 1),
            ElementType.Fire => new Color(1, .53f, .25f),
            ElementType.Water => new Color(.27f, .66f, 1),
            ElementType.Lightning => new Color(.86f, .67f, 1),
            ElementType.Wind => new Color(.56f, .95f, .76f),
            ElementType.Earth => new Color(.94f, .77f, .37f),
            ElementType.Nature => new Color(.65f, .88f, .35f),
            _ => new Color(.96f, .98f, .93f)
        };
        RunSectionPresentation.Paint(projectileRenderer, tint);
        if (projectileRenderer != null)
        {
            projectileRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            projectileRenderer.receiveShadows = false;
        }
    }

    private void Update()
    {
        if (!initialized || finished) return;
        if (Time.time >= expiresAt || remainingDistance <= .001f ||
            (sourceRunner != null && sourceRunner.Phase != PlayerRunPhase.Running &&
             sourceRunner.Phase != PlayerRunPhase.Combat))
        {
            Finish();
            return;
        }
        Simulate(Time.deltaTime);
    }

    /// <summary>Public fixed-duration simulation hook for behavioural validation.
    /// Cosmetic instances use the exact same geometry but cannot cause damage.</summary>
    public void Simulate(float deltaTime)
    {
        if (!initialized || finished || deltaTime <= 0) return;
        UpdateHoming(deltaTime);
        float distance = Mathf.Min(speed * deltaTime, remainingDistance);

        // Resolve several swept impacts in one frame for pierce/ricochet.
        for (int impact = 0; impact < 16 && distance > .0001f && !finished; impact++)
        {
            Vector3 start = transform.position;
            if (!TryFindCollision(start, distance, out Collision collision))
            {
                transform.position = start + direction * distance;
                remainingDistance -= distance;
                break;
            }

            float advanced = Mathf.Clamp(collision.Distance, 0, distance);
            transform.position = start + direction * advanced;
            distance -= advanced; remainingDistance -= advanced;
            if (collision.Obstacle != null)
            {
                if (authoritative)
                    collision.Obstacle.HitByProjectile(specification.damage * specification.obstacleDamageMultiplier,
                        specification.element);
                Explode(transform.position, null, collision.Obstacle);
                Finish();
                return;
            }

            Enemy enemy = collision.Enemy;
            hitEnemies.Add(enemy);
            if (authoritative) ApplyEnemyHit(enemy, specification.damage);
            Explode(transform.position, enemy, null);

            if (remainingPierces > 0) remainingPierces--;
            else if (remainingBounces > 0 && TryRicochet(out Vector3 ricochet))
            {
                remainingBounces--;
                direction = ricochet;
                homingTarget = null;
            }
            else
            {
                Finish();
                return;
            }

            float inset = Mathf.Min(.025f, distance);
            transform.position += direction * inset;
            distance -= inset; remainingDistance -= inset;
        }
        if (direction.sqrMagnitude > .001f) transform.forward = direction;
    }

    private bool TryFindCollision(Vector3 start, float distance, out Collision collision)
    {
        collision = new Collision { Distance = float.PositiveInfinity };
        int count = Physics.OverlapSphereNonAlloc(start, radius, overlaps, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++) Consider(overlaps[i], 0, ref collision);
        count = Physics.SphereCastNonAlloc(start, radius, direction, sweepHits, distance, ~0,
            QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++) Consider(sweepHits[i].collider, sweepHits[i].distance, ref collision);
        return collision.Enemy != null || collision.Obstacle != null;
    }

    private void Consider(Collider collider, float distance, ref Collision best)
    {
        if (collider == null || distance >= best.Distance || collider.transform.IsChildOf(transform)) return;
        var obstacle = RunnerObstacleDamageUtility.Find(collider);
        if (obstacle != null)
        {
            if (!obstacle.BlocksProjectiles || (laneId >= 0 && obstacle.LaneId != laneId)) return;
            best = new Collision { Obstacle = obstacle, Distance = distance };
            return;
        }
        Enemy enemy = collider.GetComponentInParent<Enemy>();
        if (!IsValidEnemy(enemy) || hitEnemies.Contains(enemy)) return;
        best = new Collision { Enemy = enemy, Distance = distance };
    }

    private bool IsValidEnemy(Enemy enemy) => enemy != null && enemy.IsSpawned && !enemy.IsDead &&
        (laneId < 0 || enemy.LaneId == laneId);

    private void UpdateHoming(float dt)
    {
        if (!IsHoming) return;
        bool retained=IsValidEnemy(homingTarget)&&!hitEnemies.Contains(homingTarget);
        if(retained)
        {
            Vector3 offset=homingTarget.AimPosition-transform.position;offset.y=0;
            retained=offset.z>.05f&&offset.sqrMagnitude<=specification.homingAcquireRange*specification.homingAcquireRange;
        }
        // The acquisition cone chooses a forward target. Once acquired, its
        // bearing naturally grows as the projectile closes in; dropping it at
        // that point made mild guidance stop just before the hit. Steering
        // itself remains clamped to the world-forward maximum angle.
        if(!retained&&Time.time>=nextTargetSearch)
        {
            nextTargetSearch = Time.time + .1f;
            homingTarget = null;
            float nearest = float.PositiveInfinity;
            foreach (Enemy candidate in Enemy.ActiveEnemies)
            {
                if (!CanGuideToward(candidate)) continue;
                float sqr = (candidate.AimPosition - transform.position).sqrMagnitude;
                if (sqr >= nearest) continue;
                nearest = sqr; homingTarget = candidate;
            }
        }
        else if(!retained)homingTarget=null;
        if (homingTarget == null) return;
        direction = RunnerProjectileSteering.Steer(direction, homingTarget.AimPosition - transform.position,
            specification.homingTurnRate, specification.homingMaxAngle, dt);
    }

    private bool CanGuideToward(Enemy enemy) => IsValidEnemy(enemy) && !hitEnemies.Contains(enemy) &&
        RunnerProjectileSteering.CanAcquire(transform.position, enemy.AimPosition, laneId, enemy.LaneId,
            specification.homingAcquireRange, specification.homingMaxAngle);

    private bool TryRicochet(out Vector3 nextDirection)
    {
        nextDirection = direction;
        Enemy nearest = null;
        float nearestSqr = float.PositiveInfinity;
        float range = Mathf.Clamp(specification.bounceRange, 1, 14);
        foreach (Enemy enemy in Enemy.ActiveEnemies)
        {
            if (!IsValidEnemy(enemy) || hitEnemies.Contains(enemy) ||
                !RunnerProjectileSteering.CanAcquire(transform.position, enemy.AimPosition, laneId,
                    enemy.LaneId, range, 45)) continue;
            float sqr = (enemy.AimPosition - transform.position).sqrMagnitude;
            if (sqr >= nearestSqr) continue;
            nearest = enemy; nearestSqr = sqr;
        }
        if (nearest == null) return false;
        nextDirection = nearest.AimPosition - transform.position;
        nextDirection.y = 0; nextDirection.Normalize();
        return true;
    }

    private void ApplyEnemyHit(Enemy enemy, float baseDamage)
    {
        if (!IsValidEnemy(enemy)) return;
        ReactionResult reaction = specification.element != ElementType.None ?
            enemy.ApplyElement(specification.element, sourceDamage: baseDamage) : ReactionResult.None;
        enemy.TakeDamage(baseDamage * reaction.damageMultiplier, specification.element);
    }

    private void Explode(Vector3 position, Enemy primaryEnemy, IRunnerProjectileObstacle primaryObstacle)
    {
        float range = specification.explosionRadius;
        if (range <= 0) return;
        if (explosionVFX != null)
            Instantiate(explosionVFX, position, Quaternion.identity).Initialize(range);
        if (!authoritative || specification.explosionDamageMultiplier <= 0) return;
        float blastDamage = specification.damage * specification.explosionDamageMultiplier;
        explosionTargets.Clear();
        explosionTargets.AddRange(Enemy.ActiveEnemies);
        foreach (Enemy enemy in explosionTargets)
        {
            if (!IsValidEnemy(enemy) || enemy == primaryEnemy) continue;
            Vector3 delta = enemy.AimPosition - position; delta.y = 0;
            if (delta.sqrMagnitude <= range * range) ApplyEnemyHit(enemy, blastDamage);
        }

        explodedObstacles.Clear();
        int count = Physics.OverlapSphereNonAlloc(position, range, explosionHits, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            var obstacle = RunnerObstacleDamageUtility.Find(explosionHits[i]);
            if (obstacle == null || obstacle == primaryObstacle || !obstacle.BlocksProjectiles ||
                (laneId >= 0 && obstacle.LaneId != laneId) || !explodedObstacles.Add(obstacle)) continue;
            obstacle.HitByProjectile(blastDamage * specification.obstacleDamageMultiplier, specification.element);
        }
    }

    private void Finish()
    {
        if (finished) return;
        finished = true; active.Remove(this);
        Destroy(gameObject);
    }

    public static void ClearLane(int lane)
    {
        foreach (Projectile projectile in new List<Projectile>(active))
            if (projectile != null && projectile.laneId == lane) projectile.Finish();
    }
    public static void ClearAll()
    {
        foreach(Projectile projectile in new List<Projectile>(active))if(projectile!=null)projectile.Finish();
    }
}
