using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeavyBulletItem : ItemRuntime
{
    public override void ModifyProjectile(ProjectileSpec spec)
    {
        base.ModifyProjectile(spec);
        spec.damage*=3f;
        spec.sizeMultiplier*=3f;
        spec.element = ElementType.Ice;
    }
}
