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
        if (TryAttack())
        {
            remainingCooldown = cooldown;
        }
    }

    protected abstract bool TryAttack();
}
