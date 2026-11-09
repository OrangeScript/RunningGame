using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RainCoreRuntime : ItemRuntime
{
    // Start is called before the first frame update
    readonly float interval;
    readonly float radius;
    float auraAmount;
    float auraDuration;
    LayerMask enemyLayer;
    Transform ownerTransform;
    float timer;

    public RainCoreRuntime(
        float interval,
        float radius,
        float auraAmount,
        float auraDuration,
        LayerMask enemyLayer
    )
    {
        this.interval =
            Mathf.Max(
                0.1f,
                interval
            );

        this.radius =
            radius;

        this.auraAmount =
            auraAmount;

        this.auraDuration =
            auraDuration;

        this.enemyLayer =
            enemyLayer;
    }

    public override void OnAcquire(ItemManager owner)
    {
        base.OnAcquire(owner);
        ownerTransform = owner.transform;
        timer = 0f;
    }

    public override void Tick(float deltaTime)
    {
        base.Tick(deltaTime);
        timer -= deltaTime;

        if(timer > 0f)
        {
            return;
        }
        timer = Owner!=null&&Owner.CurrentMode==GameplayMode.BossArena?Mathf.Min(interval,3):interval;
        if(Owner!=null&&Owner.CurrentMode==GameplayMode.BossArena)
        {
            var boss=Object.FindFirstObjectByType<ArenaBossController>();
            if(boss!=null&&Vector3.Distance(ownerTransform.position,boss.transform.position)<=radius)
                boss.ApplyArenaDamage(new ArenaDamageInfo(.25f,1,Owner.OwnerClientId,boss.transform.position,Vector3.forward,ElementType.Water));
            return;
        }
        ApplyWaterAura();
    }

    private void ApplyWaterAura()
    {
        Collider[] hits = Physics.OverlapSphere(ownerTransform.position,radius,enemyLayer,QueryTriggerInteraction.Collide);
        HashSet<Enemy> affectedEnemies =
            new HashSet<Enemy>();


        foreach (Collider hit in hits)
        {
            Enemy enemy =
                hit.GetComponentInParent<Enemy>();


            if (enemy == null)
                continue;

            if (enemy.IsDead)
                continue;


            if (
                !affectedEnemies.Add(
                    enemy
                )
            )
            {
                continue;
            }


            enemy.ApplyElement(
                ElementType.Water,
                auraAmount,
                auraDuration
            );
        }
        
    }
}
