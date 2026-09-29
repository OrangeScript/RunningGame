using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PierceRuntime : ItemRuntime
{
    // Start is called before the first frame update
    private readonly int extraPierces;
    private readonly float pierceRange;

    public PierceRuntime(
        int extraPierces,
        float pierceRange
    )
    {
        this.extraPierces = extraPierces;
        this.pierceRange = pierceRange;
    }


    public override void ModifyProjectile(ProjectileSpec spec)
    {
        base.ModifyProjectile(spec);
        spec.pierce += extraPierces;
        spec.pierceRange = Mathf.Max(spec.pierceRange,pierceRange);
    }
}
