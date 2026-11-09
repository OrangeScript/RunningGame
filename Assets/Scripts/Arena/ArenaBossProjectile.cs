using UnityEngine;

/// <summary>Server-authoritative Boss bullet with deterministic client visuals.</summary>
public sealed class ArenaBossProjectile : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float damage;
    private float radius;
    private float expiresAt;
    private bool authoritative;
    private readonly RaycastHit[] hits = new RaycastHit[16];

    public static ArenaBossProjectile Spawn(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float lifetime,
        float damage,
        float radius,
        bool authoritative)
    {
        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = authoritative ? "BossBullet_Server" : "BossBullet_Visual";
        bullet.transform.position = origin;
        bullet.transform.localScale = Vector3.one * Mathf.Clamp(radius * 2f, 0.15f, 1.5f);

        Collider collider = bullet.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = bullet.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(1f, 0.12f, 0.28f);

        ArenaBossProjectile projectile = bullet.AddComponent<ArenaBossProjectile>();
        direction.y = 0f;
        projectile.direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        projectile.speed = Mathf.Clamp(speed, 0.1f, 50f);
        projectile.damage = Mathf.Max(0f, damage);
        projectile.radius = Mathf.Clamp(radius, 0.05f, 0.75f);
        projectile.expiresAt = Time.time + Mathf.Clamp(lifetime, 0.1f, 20f);
        projectile.authoritative = authoritative;
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
        if (authoritative && TryDamagePlayer(start, distance))
            return;

        transform.position = start + direction * distance;
    }

    private bool TryDamagePlayer(Vector3 start, float distance)
    {
        int count = Physics.SphereCastNonAlloc(
            start,
            radius,
            direction,
            hits,
            distance,
            ~0,
            QueryTriggerInteraction.Collide);

        IArenaDamageable nearest = null;
        float nearestDistance = float.PositiveInfinity;
        Vector3 hitPoint = start + direction * distance;

        for (int i = 0; i < count; i++)
        {
            IArenaDamageable damageable = ArenaDamageUtility.FindDamageable(hits[i].collider);
            if (damageable == null || !damageable.IsArenaAlive || damageable.ArenaTeamId == 2)
                continue;

            if (hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
                nearest = damageable;
                hitPoint = hits[i].point;
            }
        }

        if (nearest == null)
            return false;

        nearest.ApplyArenaDamage(new ArenaDamageInfo(damage, 2, 0, hitPoint, direction));
        Destroy(gameObject);
        return true;
    }
}
