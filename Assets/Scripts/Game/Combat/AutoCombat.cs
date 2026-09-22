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
    [SerializeField] Projectile projectilePrefab;
    [SerializeField] private PlayerStats stats;
    [SerializeField] private ItemManager itemManager;
    [SerializeField] private ProjectileSpec baseSpec;

    [Header("Target")]
    [SerializeField] private LayerMask enemyLayer;
    private bool isFiring;
    



    float nextFireTime;
    void Update()
    {
        if(crowd == null) return;
        if(crowd.Population <= 0) return;
        if(Time.time < nextFireTime) return;
        Enemy target = FindNearestEnemy();
        if(target == null)  return;
        nextFireTime = Time.time + stats.AttackInterval;
        Fire(target);
    }

    void Fire(Enemy target)
    {

        if(isFiring) return;

        StartCoroutine(FireVolley(target));

    }

    IEnumerator FireVolley(Enemy target)
    {
        isFiring = true;
        int projectileCount = Mathf.CeilToInt(crowd.Population / (float)stats.PopulationPerProjectile);

        projectileCount = Mathf.Clamp(projectileCount,1,stats.MaxProjectilesPerVolley);

        for(int i = 0; i< projectileCount; i++)
        {
            Transform shooter = GetShooter(i,projectileCount);

            Vector3 spawnPosition = shooter.position+Vector3.up*0.5f+transform.forward*0.3f;

            ProjectileSpec spec =  baseSpec.Clone();
            spec.damage = stats.Damage;

            itemManager.ModifyProjectile(spec);


            Projectile projectile = Instantiate(projectilePrefab,spawnPosition,Quaternion.identity);
            projectile.Initialize(spec,target);
            yield return new WaitForSeconds(stats.ShotSpacing);
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


    Enemy FindNearestEnemy()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            stats.AttackRange,
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
