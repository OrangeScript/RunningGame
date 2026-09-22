using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{

    [SerializeField] private float speed = 18f;
    [SerializeField] private float hitDistance = 0.2f;
    [SerializeField] private float lifeTime = 3f;
    private Enemy target;
    private int damage;
    private float destoryTime;

    public void Initialize(int damage,Enemy target)
    {
        this.damage = damage;
        this.target = target;
        this.destoryTime = lifeTime + Time.time;
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
