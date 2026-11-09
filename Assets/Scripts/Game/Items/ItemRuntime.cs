using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ItemRuntime
{
    protected ItemManager Owner { get; private set; }

    public virtual bool CanActivate => false;
    public virtual float CooldownDuration => 0;
    public virtual float CooldownRemaining => 0;

    public virtual void OnAcquire(ItemManager owner)
    {
        Owner = owner;
    }

    public virtual void OnRemove()
    {
        Owner = null;
    }

    public virtual void OnModeChanged(GameplayMode previous, GameplayMode current)
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

    public virtual bool TryActivate()
    {
        return false;
    }
    public virtual void ModifyArenaStats(ArenaBuildStats stats) { }

}
