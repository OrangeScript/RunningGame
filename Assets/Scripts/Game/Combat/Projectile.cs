using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{

    [SerializeField] private float speed = 18f;
    [SerializeField] private float hitDistance = 0.2f;
    [SerializeField] private float lifeTime = 3f;
    private Enemy target;
    private float damage;
    private float destoryTime;

    private int remainingBounces;
    private float bounceRange;
    private float explosionRadius;

    private float explosionDamageMultiplier;
    private int remainingPierces;
    private float pierceRange;

    private ElementType element;

    [SerializeField] private ExplosionVFX explosionVFX;
    private Vector3 scale;
    readonly HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
    [SerializeField] private Renderer projectileRenderer;
    [SerializeField] LayerMask enemyLayer;

    void Awake()
    {
        scale = transform.localScale;
    }

    public void Initialize(ProjectileSpec spec,Enemy target)
    {
        this.damage = spec.damage;
        this.speed = spec.speed;
        transform.localScale *= spec.sizeMultiplier;
        this.target = target;
        element = spec.element;
        ApplyElementVisual(spec.element);
        this.destoryTime = lifeTime + Time.time;
        remainingBounces = spec.bounce;
        bounceRange = spec.bounceRange;
        explosionRadius =
            spec.explosionRadius;

        explosionDamageMultiplier =
            spec.explosionDamageMultiplier;

        remainingPierces = spec.pierce;
        pierceRange = spec.pierceRange;
        hitEnemies.Clear();
    }

    private void ApplyElementVisual(ElementType element)
    {
        if (projectileRenderer == null)
            return;


        switch (element)
        {
            case ElementType.Ice:

                projectileRenderer.material.color =
                    Color.cyan;

                break;


            case ElementType.Fire:

                projectileRenderer.material.color =
                    Color.red;

                break;


            case ElementType.Lightning:

                projectileRenderer.material.color =
                    Color.yellow;

                break;


            default:

                projectileRenderer.material.color =
                    Color.white;

                break;
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(target == null || target.IsDead)
        {
            Destroy(gameObject);
            return;
        }
        if(Time.time > destoryTime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition = target.AimPosition;
        Vector3 direction = targetPosition - transform.position;

        if(direction.sqrMagnitude <= hitDistance * hitDistance)
        {
            HitTarget();
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position,targetPosition,speed*Time.deltaTime);

        if(direction.sqrMagnitude > 0.01f)
        {
            transform.forward = direction.normalized;
        }
    }


    void Explode(Vector3 position)
    {
        if(explosionRadius <= 0f) return;

        if(explosionDamageMultiplier <= 0f) return;

        ExplosionVFX vfx = Instantiate(explosionVFX,position,Quaternion.identity);

        vfx.Initialize(explosionRadius);

        Collider[] hits = Physics.OverlapSphere(
            position,
            explosionRadius,
            enemyLayer,
            QueryTriggerInteraction.Collide
        );

        HashSet<Enemy> enemies = new HashSet<Enemy>();

        foreach(Collider hit in hits)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();

            if(enemy == null) continue;

            if(enemy.IsDead) continue;

            if(!enemies.Add(enemy)) continue;

            float explosionDamage = damage*explosionDamageMultiplier;

            enemy.TakeDamage(explosionDamage);
        }
    }



    void HitTarget()
    {
        if(target == null)
        {
            Destroy(gameObject);
            return;
        }
        Enemy hitTarget = target;
        hitEnemies.Add(hitTarget);

        ReactionResult reactionResult = ReactionResult.None;
        if (element != ElementType.None)
            {
                reactionResult = hitTarget.ApplyElement(
                    element
                );
            }
        //元素反应可能影响damage

        damage *= reactionResult.damageMultiplier;
        hitTarget.TakeDamage(damage);

        Explode(hitTarget.AimPosition);

        if(remainingBounces <= 0)
        {
            Destroy(gameObject);
            return;
        }

        Enemy nextTarget = FindBounceTarget();
        if(nextTarget == null)
        {
            Destroy(gameObject);
            return;
        }
            
        if (remainingPierces > 0)
        {
            Enemy pierceTarget =
                FindPierceTarget();


            if (pierceTarget != null)
            {
                remainingPierces--;

                target =
                    pierceTarget;

                return;
            }
        }


        // -------------------------
        // 4. Bounce
        // -------------------------

        if (remainingBounces > 0)
        {
            Enemy bounceTarget =
                FindBounceTarget();


            if (bounceTarget != null)
            {
                remainingBounces--;

                target =
                    bounceTarget;

                return;
            }
        }


        // -------------------------
        // 没有任何后续行为
        // -------------------------

        Destroy(gameObject);
    }

    private Enemy FindPierceTarget()
    {
        Collider[] hits = 
            Physics.OverlapSphere(
                transform.position,
                pierceRange,
                enemyLayer,
                QueryTriggerInteraction.Collide
            );
        Enemy bestTarget = null;
        float bestDistanceSqr = Mathf.Infinity;

        foreach(Collider hit in hits)
        {
              Enemy enemy =
            hit.GetComponentInParent<Enemy>();


            if (enemy == null)
                continue;

            if (enemy.IsDead)
                continue;

            if (hitEnemies.Contains(enemy))
                continue;


            Vector3 direction =
                enemy.AimPosition -
                transform.position;


            if (direction.sqrMagnitude < 0.001f)
                continue;


            Vector3 normalizedDirection =
                direction.normalized;


            float forwardDot =
                Vector3.Dot(
                    transform.forward,
                    normalizedDirection
                );


            // 小于0说明敌人在身后。
            // 0.5大约限制在前方120°范围内。
            if (forwardDot < 0.5f)
                continue;


            float distanceSqr =
                direction.sqrMagnitude;


            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr =
                    distanceSqr;

                bestTarget =
                    enemy;
            }
            
        }
        return bestTarget;
    }

    private Enemy FindBounceTarget()
    {
        Collider[] hits =
            Physics.OverlapSphere(
                transform.position,
                bounceRange,
                enemyLayer,
                QueryTriggerInteraction.Collide
            );


        Enemy nearestEnemy =
            null;


        float nearestDistanceSqr =
            Mathf.Infinity;


        foreach (Collider hit in hits)
        {
            Enemy enemy =
                hit.GetComponentInParent<Enemy>();


            if (enemy == null)
                continue;


            if (enemy.IsDead)
                continue;


            // 已经命中过，不再选择
            if (hitEnemies.Contains(enemy))
                continue;


            Vector3 difference =
                enemy.AimPosition -
                transform.position;


            float distanceSqr =
                difference.sqrMagnitude;


            if (
                distanceSqr <
                nearestDistanceSqr
            )
            {
                nearestDistanceSqr =
                    distanceSqr;

                nearestEnemy =
                    enemy;
            }
        }


        return nearestEnemy;
    }

}
