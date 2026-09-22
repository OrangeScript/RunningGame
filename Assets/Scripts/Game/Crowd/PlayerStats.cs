using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//  “如果玩家现在什么特殊道具都没有，这轮攻击应该是什么样？”
// 以及一些角色应该有的基础属性
public class PlayerStats : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 2f;

    [Header("Attack")]
    [SerializeField] float baseAttackInterval = 0.5f;
    [SerializeField] int baseDamage = 1;
    [SerializeField] float baseAttackRange = .05f;



    [Header("Projectile")]
    [SerializeField] int basePopulationPerProjectile = 5; // one projectile need 5 people
    [SerializeField] int baseMaxProjectilesPerVolley = 12;
    [SerializeField] float baseShotSpacing = .04f;

    float damageMultiplier = 1f;
    float attackSpeedMultiplier = 1f;
    float attackRadiusBonus = 0f;
    int  projectilePopulationBonus = 0;
    int extraProjectiles = 0;


    public int Damage => Mathf.Max(1,Mathf.RoundToInt(baseDamage * damageMultiplier));
    public float AttackInterval => baseAttackInterval / attackSpeedMultiplier;
    public float AttackRange => baseAttackRange + attackRadiusBonus;
    public int PopulationPerProjectile => Mathf.Max(1,basePopulationPerProjectile - projectilePopulationBonus);

    public int MaxProjectilesPerVolley => baseMaxProjectilesPerVolley + extraProjectiles;

    public float ShotSpacing => baseShotSpacing;


    public AttackPlan CreateAttackPlan(int population,ProjectileArchetype archetype)
    {
        
        int projectileCount =
            Mathf.CeilToInt(
                population /
                (float)basePopulationPerProjectile
            );

        projectileCount =
            Mathf.Clamp(
                projectileCount,
                1,
                baseMaxProjectilesPerVolley
            );


        return new AttackPlan
        {
            damage = baseDamage,

            attackInterval =
                baseAttackInterval,

            attackRange =
                baseAttackRange,

            projectileCount =
                projectileCount,

            shotSpacing =
                baseShotSpacing,

            projectileArchetype = archetype
        };
    }

    public void AddDamagePercent(float percent)
    {
        damageMultiplier *=
            1f + percent;
    }


    public void AddAttackSpeedPercent(float percent)
    {
        attackSpeedMultiplier *=
            1f + percent;
    }


    public void AddAttackRange(float amount)
    {
        attackRadiusBonus += amount;
    }


    public void ImprovePopulationPerProjectile(int amount)
    {
        projectilePopulationBonus += amount;
    }


    public void AddMaxProjectiles(int amount)
    {
        extraProjectiles += amount;
    }
}
