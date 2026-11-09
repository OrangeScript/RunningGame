using UnityEngine;

public sealed class TimedCombatBoostRuntime : ItemRuntime
{
    private readonly float damageMultiplier;
    private readonly float attackIntervalMultiplier;
    private readonly int extraProjectiles;
    private readonly float projectileSpeedMultiplier;
    private readonly float projectileSizeMultiplier;

    public TimedCombatBoostRuntime(
        float damageMultiplier,
        float attackIntervalMultiplier,
        int extraProjectiles,
        float projectileSpeedMultiplier,
        float projectileSizeMultiplier)
    {
        this.damageMultiplier = Mathf.Max(0.05f, damageMultiplier);
        this.attackIntervalMultiplier = Mathf.Max(0.05f, attackIntervalMultiplier);
        this.extraProjectiles = extraProjectiles;
        this.projectileSpeedMultiplier = Mathf.Max(0.05f, projectileSpeedMultiplier);
        this.projectileSizeMultiplier = Mathf.Max(0.05f, projectileSizeMultiplier);
    }

    public override void ModifyAttack(AttackPlan plan)
    {
        plan.damageMultiplier *= damageMultiplier;
        plan.attackInterval *= attackIntervalMultiplier;
        plan.projectileCount = Mathf.Max(1, plan.projectileCount + extraProjectiles);
    }

    public override void ModifyProjectile(ProjectileSpec spec)
    {
        spec.speed *= projectileSpeedMultiplier;
        spec.sizeMultiplier *= projectileSizeMultiplier;
    }
}
