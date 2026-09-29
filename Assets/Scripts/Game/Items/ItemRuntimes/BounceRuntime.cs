using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BounceRuntime : ItemRuntime
{

    private readonly int extraBounces;
    private readonly float bounceRange;

    public BounceRuntime(int extraBounces,float bounceRange)
    {   
        this.extraBounces = extraBounces;
        this.bounceRange = bounceRange;
    }
    public override void ModifyProjectile(ProjectileSpec spec)
    {
        base.ModifyProjectile(spec);
        spec.bounce += extraBounces;
        spec.bounceRange += bounceRange;
    }
}
