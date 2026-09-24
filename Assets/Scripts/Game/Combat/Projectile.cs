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
        ApplyElementVisual(spec.element);
        this.destoryTime = lifeTime + Time.time;
        remainingBounces = spec.bounce;
        bounceRange = spec.bounceRange;
        explosionRadius =
            spec.explosionRadius;

        explosionDamageMultiplier =
            spec.explosionDamageMultiplier;
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
        remainingBounces --;
        target = nextTarget;
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
