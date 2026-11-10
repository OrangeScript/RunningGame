using UnityEngine;

//  “如果玩家现在什么特殊道具都没有，这轮攻击应该是什么样？”
// 以及一些角色应该有的基础属性
public class PlayerStats : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 2f;

    [Header("Attack")]
    [SerializeField] float baseAttackInterval = 0.5f;
    [SerializeField] float baseDamage = 4f;
    [SerializeField] float baseAttackRange = 15f;



    [Header("Projectile")]
    [SerializeField] int basePopulationPerProjectile = 5; // one projectile need 5 people
    [SerializeField] int baseMaxProjectilesPerVolley = 12;
    [SerializeField] float baseShotSpacing = .04f;

    float attackSpeedMultiplier = 1f;
    float attackRadiusBonus = 0f;
    int  projectilePopulationBonus = 0;
    int extraProjectiles = 0;


    public float MoveSpeed => moveSpeed;
    public float Damage => baseDamage;
    public float AttackInterval => baseAttackInterval / attackSpeedMultiplier;
    public float AttackRange => baseAttackRange + attackRadiusBonus;
    public int PopulationPerProjectile => Mathf.Max(1,basePopulationPerProjectile - projectilePopulationBonus);

    public int MaxProjectilesPerVolley => baseMaxProjectilesPerVolley + extraProjectiles;

    public float ShotSpacing => baseShotSpacing;


    public AttackPlan CreateAttackPlan(int population,ProjectileArchetype archetype)
    {
        
        // Gates increase one character's weapon damage, not the number of
        // bodies or shooters. Multishot is reserved for item modifiers.
        int projectileCount = 1;

        projectileCount =
            Mathf.Clamp(
                projectileCount,
                1,
                MaxProjectilesPerVolley
            );


        return new AttackPlan
        {
            // Preserve gate growth without letting two early multipliers
            // erase every sentry. Below the baseline, penalties stay linear.
            damageMultiplier = population<=20?Mathf.Clamp(population/20f,.1f,1):
                Mathf.Min(8,Mathf.Sqrt(population/20f)),

            attackInterval =
                AttackInterval,

            attackRange =
                AttackRange,

            projectileCount =
                projectileCount,

            shotSpacing =
                baseShotSpacing,

            projectileArchetype = archetype
        };
    }

    public void AddAttackSpeedMultiplier(float amount)
    {
        attackSpeedMultiplier = Mathf.Max(0.05f, attackSpeedMultiplier + amount);
    }

    public void AddAttackRange(float amount)
    {
        attackRadiusBonus += amount;
    }

    public void AddProjectileEfficiency(int populationReduction)
    {
        projectilePopulationBonus += populationReduction;
    }

    public void AddMaxProjectiles(int amount)
    {
        extraProjectiles = Mathf.Max(-baseMaxProjectilesPerVolley + 1, extraProjectiles + amount);
    }



}
