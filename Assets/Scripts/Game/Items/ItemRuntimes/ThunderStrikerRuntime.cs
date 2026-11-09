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
        if (owner != null && owner.CurrentMode == GameplayMode.BossArena)
            return TryAttackArenaBoss();

        Enemy[] targets = FindEnemies();
        if (targets.Length == 0)
            return false;

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

    private bool TryAttackArenaBoss()
    {
        ArenaBossController boss = Object.FindFirstObjectByType<ArenaBossController>();
        if (boss == null || !boss.IsArenaAlive)
            return false;

        Vector3 origin = owner != null ? owner.transform.position : Vector3.zero;
        Vector3 direction = boss.transform.position - origin;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.forward;

        boss.ApplyArenaDamage(new ArenaDamageInfo(
            damage,
            1,
            owner.OwnerClientId,
            boss.transform.position,
            direction.normalized,
            ElementType.Lightning));
        return true;
    }

    Enemy[] FindEnemies()
    {
        RunnerController runner = owner != null
            ? owner.GetComponent<RunnerController>()
            : null;
        Collider[] colliders = Physics.OverlapSphere(
            owner.transform.position,
                range,
                enemyLayer,
                QueryTriggerInteraction.Collide
        );
        List<Enemy> enemies = new();
        HashSet<Enemy> uniqueEnemies = new HashSet<Enemy>();
        foreach(Collider hit in colliders)
        {
            Enemy enemy =
                hit.GetComponentInParent<Enemy>();


            if (enemy == null)
                continue;

            if (enemy.IsDead)
                continue;
            if (runner != null && enemy.LaneId >= 0 && enemy.LaneId != runner.LaneId)
                continue;
            if (!uniqueEnemies.Add(enemy))
                continue;
            enemies.Add(enemy);
        }
        return enemies.ToArray();
    }
}
