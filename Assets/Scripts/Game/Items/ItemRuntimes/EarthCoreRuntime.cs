using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EarthCoreRuntime : ItemRuntime
{
    public override void ModifyProjectile(ProjectileSpec spec)
    {
        base.ModifyProjectile(spec);
        spec.element = ElementType.Earth;
    }
}
