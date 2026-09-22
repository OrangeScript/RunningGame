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
    [SerializeField] float baseDamage = 1f;
    [SerializeField] float baseAttackRange = .05f;



    [Header("Projectile")]
    [SerializeField] int basePopulationPerProjectile = 5; // one projectile need 5 people
    [SerializeField] int baseMaxProjectilesPerVolley = 12;
    [SerializeField] float baseShotSpacing = .04f;

    float attackSpeedMultiplier = 1f;
    float attackRadiusBonus = 0f;
    int  projectilePopulationBonus = 0;
    int extraProjectiles = 0;


    public float Damage => baseDamage;
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
            damageMultiplier = 1f,

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



}
