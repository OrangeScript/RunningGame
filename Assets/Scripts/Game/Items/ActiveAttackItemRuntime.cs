using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ActiveAttackItemRuntime : ItemRuntime
{
    readonly float cooldown;
    protected ItemManager owner;
    float remainingCooldown;

    protected ActiveAttackItemRuntime(
        float cooldown
    )
    {
        this.cooldown = cooldown;
    }

    public override void OnAcquire(ItemManager owner)
    {
        base.OnAcquire(owner);
        this.owner = owner;
        remainingCooldown = 0f;
    }

    public override void Tick(float deltaTime)
    {
        base.Tick(deltaTime);
        remainingCooldown -= deltaTime;

        if(remainingCooldown > 0f) return;
    }


    public bool IsReady => remainingCooldown <= 0f;
    public bool TryActivate()
    {
        if(!IsReady) return false;
        if(!TryAttack()) return false;

        remainingCooldown = cooldown;
        return true;
    }

    protected abstract bool TryAttack();
}
