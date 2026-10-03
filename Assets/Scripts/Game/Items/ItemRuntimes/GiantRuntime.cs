using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GiantRuntime : ItemRuntime
{
    private readonly float attackIntervalMultiplier;

    private CrowdVisualizer crowdVisualizer;
    private readonly float
        damageMultiplier;

    private readonly float
        projectileSizeMultiplier;

    private readonly float
        projectileSpeedMultiplier;

    public GiantRuntime(
        float attackIntervalMultiplier,
        float damageMultiplier,
        float projectileSizeMultiplier,
        float projectileSpeedMultiplier
    )
    {
        this.attackIntervalMultiplier =
            attackIntervalMultiplier;

        this.damageMultiplier =
            damageMultiplier;

        this.projectileSizeMultiplier =
            projectileSizeMultiplier;

        this.projectileSpeedMultiplier =
            projectileSpeedMultiplier;
    }

    public override void OnAcquire(ItemManager owner)
    {
        base.OnAcquire(owner);

        crowdVisualizer = owner.GetComponent<CrowdVisualizer>();
        crowdVisualizer.SetGiantMode(true);


    }


    public override void ModifyAttack(AttackPlan plan)
    {
        base.ModifyAttack(plan);
        plan.projectileCount = 1;
        plan.attackInterval *= attackIntervalMultiplier;
        plan.damageMultiplier *= damageMultiplier;
    }

    public override void ModifyProjectile(ProjectileSpec spec)
    {
        base.ModifyProjectile(spec);
        spec.sizeMultiplier *=
            projectileSizeMultiplier;


        // 更慢
        spec.speed *=
            projectileSpeedMultiplier;
    }

}
