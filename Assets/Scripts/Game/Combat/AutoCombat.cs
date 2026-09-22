using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class AutoCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField]CrowdManager crowd;
    [SerializeField] Transform firePoint;
    [SerializeField] private PlayerStats stats;
    [SerializeField] private ItemManager itemManager;
    [SerializeField] private ProjectileArchetype archetype;

    [Header("Target")]
    [SerializeField] private LayerMask enemyLayer;
    private bool isFiring;
    float nextFireTime;
    void Update()
    {
        if(crowd == null) return;
        if(crowd.Population <= 0) return;
        if(stats == null) return;
        if(itemManager == null) return;
        if(isFiring) return;
        if(Time.time < nextFireTime) return;

        AttackPlan plan = CreateAttackPlan();

        Enemy target = FindNearestEnemy(plan.attackRange);
        if(target == null)  return;
        nextFireTime = Time.time + stats.AttackInterval;
        Fire(target,plan);
    }


    private AttackPlan CreateAttackPlan()
    {
        AttackPlan plan = stats.CreateAttackPlan(crowd.Population,archetype);
        itemManager.ModifyAttack(plan);
        return plan;
    }

    void Fire(Enemy target,AttackPlan plan)
    {

        if(isFiring) return;

        StartCoroutine(FireVolley(target,plan));

    }

    IEnumerator FireVolley(Enemy target,AttackPlan plan)
    {
        isFiring = true;
        // int projectileCount = Mathf.CeilToInt(crowd.Population / (float)stats.PopulationPerProjectile);

        // projectileCount = Mathf.Clamp(projectileCount,1,stats.MaxProjectilesPerVolley);

        for(int i = 0; i< plan.projectileCount; i++)
        {
            Transform shooter = GetShooter(i,plan.projectileCount);

            Vector3 spawnPosition = shooter.position+Vector3.up*0.5f+transform.forward*0.3f;
            float finalDamage = stats.Damage * plan.damageMultiplier;
            ProjectileSpec spec =  plan.projectileArchetype.CreateSpec(finalDamage);
            itemManager.ModifyProjectile(spec);


            Projectile projectile = Instantiate(plan.projectileArchetype.ProjectilePrefab,spawnPosition,Quaternion.identity);
            projectile.Initialize(spec,target);
            yield return new WaitForSeconds(plan.shotSpacing);
        }
        isFiring = false;

    }



    Transform GetShooter(int shootIndex,
                        int shootCount)
    {
        if(crowd.UnitCount <= 0)
            return firePoint;
        
        if(shootCount <= 1)
        {
            int middle = crowd.UnitCount / 2;
            return crowd.GetUnitTransform(middle);
        }

        float t = shootIndex / (float)(shootCount - 1);
        int unitIndex = Mathf.RoundToInt(t * (crowd.UnitCount - 1));

        return crowd.GetUnitTransform(unitIndex);

    }


    Enemy FindNearestEnemy(float attackRange)
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            attackRange,
            enemyLayer,
            QueryTriggerInteraction.Collide
        );

        Enemy nearestEnemy = null;
        float nearestEnemyDistance = float.MaxValue;

        foreach(Collider hit in hits)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if(enemy == null) continue;
            if(enemy.IsDead) continue;
            float distanceSqr = (enemy.transform.position - transform.position).sqrMagnitude;
            if(distanceSqr < nearestEnemyDistance)
            {
                nearestEnemyDistance = distanceSqr;
                nearestEnemy = enemy;
            }
        }
        return nearestEnemy;
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position,stats.AttackRange);
    }

}
