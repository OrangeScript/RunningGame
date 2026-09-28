using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ItemRuntime
{

    public virtual void OnAcquire(ItemManager owner)
    {
        
    }

    public virtual void Tick(float deltaTime)
    {
        
    }
    public virtual void ModifyProjectile(ProjectileSpec spec)
    {
        
    }

    public virtual void ModifyAttack(AttackPlan plan)
    {
        
    }

}
