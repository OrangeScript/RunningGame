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
        ApplyVisualMode(owner.CurrentMode);
    }

    public override void OnRemove()
    {
        if (crowdVisualizer != null)
            crowdVisualizer.SetGiantMode(false);

        base.OnRemove();
    }

    public override void OnModeChanged(GameplayMode previous, GameplayMode current)
    {
        ApplyVisualMode(current);
    }


    public override void ModifyAttack(AttackPlan plan)
    {
        base.ModifyAttack(plan);
        // Runner represents firepower with a crowd, while Arena/FPS use one
        // avatar. Both keep the heavy-weapon identity without spawning clones.
        if (plan.gameplayMode == GameplayMode.Runner)
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

    private void ApplyVisualMode(GameplayMode mode)
    {
        if (crowdVisualizer != null)
            crowdVisualizer.SetGiantMode(mode == GameplayMode.Runner);
    }

}
