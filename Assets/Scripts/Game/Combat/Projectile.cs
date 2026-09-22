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
    private Vector3 scale;
    [SerializeField] private Renderer projectileRenderer;


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

    void HitTarget()
    {
        if (target != null)
        {
            target.TakeDamage(damage);
        }
        Destroy(gameObject);
    }
}
