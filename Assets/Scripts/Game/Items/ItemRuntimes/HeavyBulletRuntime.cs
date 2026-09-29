using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeavyBulletRuntime : ItemRuntime
{

    private readonly float damageMultiplier;
    private readonly float speedMultiplier;
    private readonly float sizeMultiplier;

    // Start is called before the first frame update
    public HeavyBulletRuntime(float damageMultiplier,
                                float speedMultiplier,
                                float sizeMultiplier)
    {
        this.damageMultiplier = damageMultiplier;
        this.speedMultiplier = speedMultiplier;
        this.sizeMultiplier = sizeMultiplier;    
    }

    public override void ModifyProjectile(ProjectileSpec spec)
    {
        base.ModifyProjectile(spec);
        spec.damage *= damageMultiplier;
        spec.speed *= speedMultiplier;
        spec.sizeMultiplier *= sizeMultiplier;
    }
}
