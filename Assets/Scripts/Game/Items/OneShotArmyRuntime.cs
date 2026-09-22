using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OneShotArmyRuntime : ItemRuntime
{
    public override void ModifyAttack(AttackPlan plan)
    {
        base.ModifyAttack(plan);
        plan.projectileCount = 1;
        plan.attackInterval /= 2f;
    }
}
