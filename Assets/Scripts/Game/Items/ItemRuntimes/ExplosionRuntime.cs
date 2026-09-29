using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplosionRuntime : ItemRuntime
{
    private readonly float radius;
    private readonly float damageMultiplier;

    public ExplosionRuntime(float radius, float damageMultiplier)
    {
        this.radius = radius;
        this.damageMultiplier = damageMultiplier;
    }

    public override void ModifyProjectile(ProjectileSpec spec)
    {
        base.ModifyProjectile(spec);
        spec.explosionRadius = UnityEngine.Mathf.Max(
                spec.explosionRadius,
                radius
            );
        spec.explosionDamageMultiplier *= damageMultiplier;
    }
}
