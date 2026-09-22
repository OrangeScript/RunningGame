using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IceCoreRuntime : ItemRuntime
{
    public override void ModifyProjectile(ProjectileSpec spec)
    {
        base.ModifyProjectile(spec);
        spec.element = ElementType.Ice;
    }
}
