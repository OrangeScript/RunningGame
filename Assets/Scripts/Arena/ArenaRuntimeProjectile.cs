using UnityEngine;

/// <summary>
/// Scene-local projectile used when no registered ArenaProjectile network
/// prefab is assigned. The server owns damage; owner and remote cosmetic
/// shots animate and collide with the shared boss surface without damage.
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
    private ElementType element;
    private int remainingBounces;
    private float explosionMultiplier;
    private float nextHitTime;
    private readonly RaycastHit[] hits = new RaycastHit[16];

    public static ArenaRuntimeProjectile SpawnServer(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float lifetime,
        float damage,
        float size,
        int teamId,
        ulong sourceClientId,
        ElementType element = ElementType.None,
        int pierce = 0,
        int bounce = 0,
        float explosionMultiplier = 0)
    {
        var projectile=Spawn(origin,direction,speed,lifetime,damage,size,teamId,sourceClientId,true);
        projectile.element=element;projectile.damage*=1+.1f*Mathf.Min(6,pierce);
        projectile.remainingBounces=Mathf.Min(4,bounce);projectile.explosionMultiplier=explosionMultiplier;
        RunSectionPresentation.Paint(projectile.GetComponent<Renderer>(),ElementTint(element));
        return projectile;
    }

    public static ArenaRuntimeProjectile SpawnVisual(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float lifetime,
        float size,
        int teamId,
        ElementType element=ElementType.None,
        int bounce=0)
    {
        var visual=Spawn(origin, direction, speed, lifetime, 0f, size, teamId, 0, false);
        RunSectionPresentation.Paint(visual.GetComponent<Renderer>(),ElementTint(element));
        visual.remainingBounces=Mathf.Min(4,bounce);
        return visual;
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
            RunSectionPresentation.Paint(renderer,shotTeam == 1 ? new Color(0.2f, 0.85f, 1f) : new Color(1f, 0.2f, 0.15f));

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

    private static Color ElementTint(ElementType element)=>element switch
    {
        ElementType.Fire=>new Color(1,.52f,.24f),ElementType.Water=>new Color(.2f,.64f,1),
        ElementType.Ice=>new Color(.45f,.92f,1),ElementType.Lightning=>new Color(.86f,.65f,1),
        ElementType.Wind=>new Color(.5f,1,.79f),ElementType.Nature=>new Color(.7f,.91f,.32f),
        ElementType.Earth=>new Color(1,.8f,.39f),_=>new Color(.9f,.97f,1)
    };

    private void Update()
    {
        if (Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        float distance = speed * Time.deltaTime;
        Vector3 start = transform.position;
        if(remainingBounces>0)
        {
            if(Mathf.Abs(start.x)>17.3f&&Mathf.Sign(start.x)==Mathf.Sign(direction.x)) { direction.x=-direction.x;remainingBounces--; }
            if(Mathf.Abs(start.z)>11.3f&&Mathf.Sign(start.z)==Mathf.Sign(direction.z)) { direction.z=-direction.z;remainingBounces--; }
        }

        if (TryHit(start, distance))
            return;

        transform.position = start + direction * distance;
    }

    private bool TryHit(Vector3 start, float distance)
    {
        if(Time.time<nextHitTime)return false;
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
        bool cosmeticHit=false;
        Vector3 point = start + direction * distance;

        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null || collider.transform.IsChildOf(transform))
                continue;

            IArenaDamageable damageable = ArenaDamageUtility.FindDamageable(collider);
            if(!authoritative&&collider.TryGetComponent<ArenaBossHitSurface>(out _)&&hits[i].distance<nearestDistance)
            {
                cosmeticHit=true;nearestDistance=hits[i].distance;point=hits[i].point;
            }
            if (damageable == null || !damageable.IsArenaAlive || damageable.ArenaTeamId == teamId)
                continue;

            if (hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
                nearest = damageable;
                point = hits[i].point;
            }
        }

        if (nearest == null&&!cosmeticHit)
            return false;

        if(authoritative&&nearest!=null)nearest.ApplyArenaDamage(new ArenaDamageInfo(
            damage*(1+explosionMultiplier),
            teamId,
            sourceClientId,
            point,
            direction,
            element));
        if(remainingBounces>0)
        {
            remainingBounces--;direction=-direction;nextHitTime=Time.time+.25f;
            transform.position=start+direction*distance;
        }
        else Destroy(gameObject);
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
