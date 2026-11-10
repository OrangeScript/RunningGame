using Unity.Netcode;
using UnityEngine;

/// <summary>Server swept collision and timestamped client visuals for lane bullets.</summary>
public sealed class RunnerEnemyProjectile : MonoBehaviour
{
    private const float DissolveTime = .16f;
    private readonly RaycastHit[] hits = new RaycastHit[24];
    private readonly Collider[] overlaps = new Collider[16];
    private RunnerEnemyProjectileSystem system;
    private RunnerEnemyShot shot;
    private Enemy source;
    private bool authoritative, stopped, dissolving;
    private double stopTime = double.PositiveInfinity;
    private Vector3 stopPosition;
    private float dissolveAt;
    private Renderer visual;
    public int LaneId => shot.LaneId;
    public bool IsAuthoritative => authoritative;
    public bool IsDissolving => dissolving;
    public uint ShotId => shot.Id;
    public Vector3 Direction => shot.Direction;
    public double BornTime => shot.Born;
    private static double Now => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening ?
        NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;

    public static RunnerEnemyProjectile Spawn(RunnerEnemyProjectileSystem owner, RunnerEnemyShot data, bool server, Enemy source)
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = server ? "RunnerEnemyShot_Server" : "RunnerEnemyShot_Visual";
        root.transform.SetParent(owner.transform, false); root.GetComponent<Collider>().enabled = false;
        var bullet = root.AddComponent<RunnerEnemyProjectile>();
        bullet.system = owner; bullet.shot = data; bullet.source = source; bullet.authoritative = server;
        bullet.visual = root.GetComponent<Renderer>();
        bullet.visual.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; bullet.visual.receiveShadows = false;
        RunSectionPresentation.Paint(bullet.visual, new Color(.94f, .32f, .17f));
        root.transform.position = PositionAt(data, Now); root.transform.localScale = Vector3.one * data.Radius * 2;
        return bullet;
    }

    public static Vector3 PositionAt(RunnerEnemyShot data, double time)
        => data.Origin + data.Direction * Mathf.Max(0, (float)(time - data.Born)) * data.Speed;

    public void StopAt(Vector3 position, double time)
    {
        if (stopped) return;
        stopped = true; stopPosition = position; stopTime = time;
    }

    private void Update()
    {
        if (dissolving)
        {
            float amount = (Time.unscaledTime - dissolveAt) / DissolveTime;
            if (amount >= 1) { Destroy(gameObject); return; }
            transform.localScale = Vector3.one * shot.Radius * 2 * (1 - amount);
            RunSectionPresentation.Paint(visual, Color.Lerp(new Color(.94f, .32f, .17f), Color.white, amount));
            return;
        }
        double now = Now;
        if (stopped && now >= stopTime) { transform.position = stopPosition; BeginDissolve(); return; }
        if (now < shot.Born)
        {
            // Killing or freezing a burst gunner interrupts rounds that have
            // not yet left its muzzle. Existing rounds remain independent.
            if (authoritative && (source == null || source.IsDead || source.IsFrozen))
                system.ImpactServer(shot.Id, shot.Origin, now);
            return;
        }
        Vector3 start = transform.position;
        Vector3 next = PositionAt(shot, now);
        if (now - shot.Born > shot.Lifetime || Mathf.Abs(next.x - shot.LaneCenter) > 8.2f)
        { BeginDissolve(); return; }
        if (authoritative && !stopped && TryHit(start, next, out Vector3 impact))
        {
            system.ImpactServer(shot.Id, impact, now); transform.position = impact; BeginDissolve(); return;
        }
        transform.position = next;
    }

    private bool TryHit(Vector3 start, Vector3 end, out Vector3 point)
    {
        point = end;
        int overlapCount = Physics.OverlapSphereNonAlloc(start, shot.Radius, overlaps, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < overlapCount; i++)
            if (AcceptHit(overlaps[i])) { point = start; return true; }
        Vector3 movement = end - start; float distance = movement.magnitude;
        if (distance < .00001f) return false;
        int count = Physics.SphereCastNonAlloc(start, shot.Radius, movement / distance, hits, distance, ~0, QueryTriggerInteraction.Collide);
        // Inspect by distance: a player behind cover must not be hit first
        // simply because PhysX returned its collider earlier in the array.
        for (int step = 0; step < count; step++)
        {
            int nearest = step;
            for (int j = step + 1; j < count; j++) if (hits[j].distance < hits[nearest].distance) nearest = j;
            var swap = hits[step]; hits[step] = hits[nearest]; hits[nearest] = swap;
            if (AcceptHit(hits[step].collider)) { point = start + movement.normalized * hits[step].distance; return true; }
        }
        return false;
    }

    private bool AcceptHit(Collider collider)
    {
        if (collider == null || collider.transform.IsChildOf(transform)) return false;
        var cover = RunnerObstacleDamageUtility.Find(collider);
        if (cover != null) return cover.LaneId == shot.LaneId && cover.BlocksProjectiles;
        var runner = collider.GetComponentInParent<RunnerController>();
        if (runner == null || runner.LaneId != shot.LaneId ||
            (runner.Phase != PlayerRunPhase.Running && runner.Phase != PlayerRunPhase.Combat)) return false;
        var stats = runner.GetComponent<CrowdManager>();
        if (stats == null || stats.Health <= 0) return false;
        stats.DamageHealth(shot.Damage);
        return true;
    }

    private void BeginDissolve()
    {
        if (dissolving) return;
        dissolving = true; dissolveAt = Time.unscaledTime;
    }
    private void OnDestroy() { if (system != null) system.Forget(shot.Id); }
}
