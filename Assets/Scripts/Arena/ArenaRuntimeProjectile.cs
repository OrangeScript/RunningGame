using UnityEngine;

/// <summary>
/// Scene-local projectile used when no registered ArenaProjectile network
/// prefab is assigned. The server owns collision; clients receive a visual
/// spawn RPC from ArenaPlayerNetwork and only animate the projectile.
/// </summary>
public sealed class ArenaRuntimeProjectile : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float damage;
    private float expiresAt;
    private float radius;
    private int teamId;
    private ulong sourceClientId;
    private bool authoritative;
    private readonly RaycastHit[] hits = new RaycastHit[16];

    public static ArenaRuntimeProjectile SpawnServer(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float lifetime,
        float damage,
        float size,
        int teamId,
        ulong sourceClientId)
    {
        return Spawn(origin, direction, speed, lifetime, damage, size, teamId, sourceClientId, true);
    }

    public static ArenaRuntimeProjectile SpawnVisual(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float lifetime,
        float size,
        int teamId)
    {
        return Spawn(origin, direction, speed, lifetime, 0f, size, teamId, 0, false);
    }

    private static ArenaRuntimeProjectile Spawn(
        Vector3 origin,
        Vector3 shotDirection,
        float shotSpeed,
        float lifetime,
        float shotDamage,
        float size,
        int shotTeam,
        ulong source,
        bool isAuthoritative)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = isAuthoritative ? "ArenaPlayerShot_Server" : "ArenaPlayerShot_Visual";
        visual.transform.position = origin;
        visual.transform.localScale = Vector3.one * Mathf.Clamp(size * 0.28f, 0.08f, 1.2f);

        Collider primitiveCollider = visual.GetComponent<Collider>();
        if (primitiveCollider != null)
            Destroy(primitiveCollider);

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = shotTeam == 1 ? new Color(0.2f, 0.85f, 1f) : new Color(1f, 0.2f, 0.15f);

        ArenaRuntimeProjectile projectile = visual.AddComponent<ArenaRuntimeProjectile>();
        shotDirection.y = 0f;
        projectile.direction = shotDirection.sqrMagnitude > 0.001f ? shotDirection.normalized : Vector3.forward;
        projectile.speed = Mathf.Clamp(shotSpeed, 0.1f, 100f);
        projectile.damage = Mathf.Max(0f, shotDamage);
        projectile.expiresAt = Time.time + Mathf.Clamp(lifetime, 0.05f, 30f);
        projectile.radius = Mathf.Clamp(size * 0.15f, 0.06f, 0.8f);
        projectile.teamId = shotTeam;
        projectile.sourceClientId = source;
        projectile.authoritative = isAuthoritative;
        return projectile;
    }

    private void Update()
    {
        if (Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        float distance = speed * Time.deltaTime;
        Vector3 start = transform.position;

        if (authoritative && TryHit(start, distance))
            return;

        transform.position = start + direction * distance;
    }

    private bool TryHit(Vector3 start, float distance)
    {
        int count = Physics.SphereCastNonAlloc(
            start,
            radius,
            direction,
            hits,
            distance,
            ~0,
            QueryTriggerInteraction.Collide);

        float nearestDistance = float.PositiveInfinity;
        IArenaDamageable nearest = null;
        Vector3 point = start + direction * distance;

        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || collider.transform.IsChildOf(transform))
                continue;

            IArenaDamageable damageable = ArenaDamageUtility.FindDamageable(collider);
            if (damageable == null || !damageable.IsArenaAlive || damageable.ArenaTeamId == teamId)
                continue;

            if (hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
                nearest = damageable;
                point = hits[i].point;
            }
        }

        if (nearest == null)
            return false;

        nearest.ApplyArenaDamage(new ArenaDamageInfo(
            damage,
            teamId,
            sourceClientId,
            point,
            direction));
        Destroy(gameObject);
        return true;
    }
}

public static class ArenaDamageUtility
{
    public static IArenaDamageable FindDamageable(Collider collider)
    {
        if (collider == null)
            return null;

        MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IArenaDamageable damageable)
                return damageable;
        }

        return null;
    }
}
