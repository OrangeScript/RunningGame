using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackPlan
{
    public float damageMultiplier;
    public float attackInterval;
    public float attackRange;
    public int projectileCount;
    public float shotSpacing;
    public ProjectileArchetype projectileArchetype;

    // public AttackPlan Clone()
    // {
    //     return new AttackPlan
    //     {
    //         damage = damage,
    //         attackInterval = attackInterval,
    //         attackRange = attackRange,
    //         projectileCount = projectileCount,
    //         shotSpacing = shotSpacing,
    //         projectileArchetype = projectileArchetype
    //     };
    // }
}
