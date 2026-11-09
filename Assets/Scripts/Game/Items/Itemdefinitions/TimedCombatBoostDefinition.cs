using UnityEngine;

[CreateAssetMenu(
    fileName = "TimedCombatBoost",
    menuName = "Roguelike/Effects/Timed Combat Boost")]
public sealed class TimedCombatBoostDefinition : ItemEffectDefiniton
{
    [SerializeField, Min(0.05f)] private float damageMultiplier = 1f;
    [SerializeField, Min(0.05f)] private float attackIntervalMultiplier = 1f;
    [SerializeField] private int extraProjectiles;
    [SerializeField, Min(0.05f)] private float projectileSpeedMultiplier = 1f;
    [SerializeField, Min(0.05f)] private float projectileSizeMultiplier = 1f;

    public override ItemRuntime CreateRuntime()
    {
        return new TimedCombatBoostRuntime(
            damageMultiplier,
            attackIntervalMultiplier,
            extraProjectiles,
            projectileSpeedMultiplier,
            projectileSizeMultiplier);
    }
}
