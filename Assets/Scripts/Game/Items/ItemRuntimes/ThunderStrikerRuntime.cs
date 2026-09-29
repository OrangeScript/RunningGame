using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThunderStrikerRuntime : ActiveAttackItemRuntime
{
    private readonly float range;

    private readonly float damage;

    private readonly float auraAmount;

    private readonly LayerMask enemyLayer;

    public ThunderStrikerRuntime(
        float cooldown,
        float range,
        float damage,
        float auraAmount,
        LayerMask layer
    ) : base(cooldown)
    {
        this.range =
            range;

        this.damage =
            damage;

        this.auraAmount =
            auraAmount;

        this.enemyLayer =
            layer;
    }


    protected override bool TryAttack()
    {
        Enemy[] targets = FindEnemies();
        foreach(Enemy target in targets)
        {
            ReactionResult reaction = target.ApplyElement(ElementType.Lightning,
                                                            auraAmount,
                                                            -1f,damage);
            float finalDamage = damage * reaction.damageMultiplier;
            target.TakeDamage(finalDamage,ElementType.Lightning);
        }

        return true;
    }

    Enemy[] FindEnemies()
    {
        
        return null;
    }
}
