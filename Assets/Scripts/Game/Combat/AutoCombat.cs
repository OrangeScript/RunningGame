using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class AutoCombat : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] CrowdVisualizer crowdVisualizer;
    [SerializeField]CrowdManager crowd;
    [SerializeField] Transform firePoint;
    [SerializeField] private PlayerStats stats;
    [SerializeField] private ItemManager itemManager;
    [SerializeField] private ProjectileArchetype archetype;
    private RunnerController runner;

    [Header("Target")]
    [SerializeField] private LayerMask enemyLayer;
    private bool isFiring;
    float nextFireTime;

    private void Awake()
    {
        runner = GetComponent<RunnerController>();
    }

    void Update()
    {
        if(!IsServer) return;
        if (runner != null && runner.Phase != PlayerRunPhase.Running && runner.Phase != PlayerRunPhase.Combat) return;
        if(crowd == null) return;
        if(crowd.Population <= 0) return;
        if(stats == null) return;
        if(itemManager == null) return;
        if(isFiring) return;
        if(Time.time < nextFireTime) return;

        AttackPlan plan = CreateAttackPlan();

        Enemy target = FindNearestEnemy(plan.attackRange);
        if(target == null)  return;
        nextFireTime = Time.time + plan.attackInterval;
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
            if (target == null || target.IsDead || !target.IsSpawned) break;
            Transform shooter = firePoint != null ? firePoint : transform;

            Vector3 spawnPosition = shooter.position + transform.right * ((i - (plan.projectileCount-1)*.5f) * .12f);
            float finalDamage = stats.Damage * plan.damageMultiplier;
            ProjectileSpec spec =  plan.projectileArchetype.CreateSpec(finalDamage);
            itemManager.ModifyProjectile(spec);


            Projectile projectile = Instantiate(plan.projectileArchetype.ProjectilePrefab,spawnPosition,Quaternion.identity);
            projectile.Initialize(spec,target);
            SpawnProjectileVisualClientRpc(
                spawnPosition,
                new NetworkObjectReference(target.NetworkObject),
                spec.speed,
                spec.sizeMultiplier,
                (int)spec.element);
            yield return new WaitForSeconds(plan.shotSpacing);
        }
        isFiring = false;

    }

    [ClientRpc]
    private void SpawnProjectileVisualClientRpc(
        Vector3 spawnPosition,
        NetworkObjectReference targetReference,
        float projectileSpeed,
        float sizeMultiplier,
        int elementValue)
    {
        // The authoritative host already instantiated the gameplay projectile.
        if (IsServer || archetype == null || archetype.ProjectilePrefab == null)
            return;

        if (!targetReference.TryGet(out NetworkObject targetObject))
            return;

        Enemy target = targetObject.GetComponent<Enemy>();
        if (target == null || !System.Enum.IsDefined(typeof(ElementType), elementValue))
            return;

        Projectile visual = Instantiate(
            archetype.ProjectilePrefab,
            spawnPosition,
            Quaternion.identity);
        visual.InitializeVisual(
            projectileSpeed,
            sizeMultiplier,
            (ElementType)elementValue,
            target);
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
            if (runner != null && enemy.LaneId >= 0 && enemy.LaneId != runner.LaneId)
                continue;
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
