using UnityEngine;


[CreateAssetMenu(
    fileName = "GiantEffect",
    menuName = "Roguelike/Effects/Giant"
)]
public class GiantDefinition
    : ItemEffectDefiniton
{
    [Header("Attack")]

    [SerializeField]
    private float
        attackIntervalMultiplier = 3f;

    [SerializeField]
    private float
        damageMultiplier = 10f;


    [Header("Projectile")]

    [SerializeField]
    private float
        projectileSizeMultiplier = 5f;

    [SerializeField]
    private float
        projectileSpeedMultiplier = 0.6f;


    public override ItemRuntime CreateRuntime()
    {
        return new GiantRuntime(
            attackIntervalMultiplier,
            damageMultiplier,
            projectileSizeMultiplier,
            projectileSpeedMultiplier
        );
    }
}