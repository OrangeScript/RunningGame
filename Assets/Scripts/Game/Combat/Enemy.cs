using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public class Enemy : MonoBehaviour
{
    
   [Header("Health")]
   [SerializeField] private float maxHealth = 8;
   [Header("Movement")]
   [SerializeField] private float moveSpeed = 2.5f;
   [SerializeField] private float attackDistance = 1.5f;

   [Header("Attack")]
   [SerializeField] private int populationDamage = 1;
   [SerializeField] private float attackInterval = .8f;

   [Header("Target")]
   [SerializeField] private Transform aimPoint;
   private float currentHealth;
   private Transform player;
   private CrowdManager crowd;
   private float nextAttackTime;
   private bool isDead;
   public bool IsDead => isDead;

    public Vector3 AimPosition
    {
        get
        {
            if(aimPoint!= null)
            {
                return aimPoint.position;
            }
            else
            {
                return transform.position + Vector3.up*0.8f;
            }
        }
    }

    void Awake()
    {
        currentHealth = maxHealth;
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        player = playerObj.transform;
        crowd = playerObj.GetComponent<CrowdManager>();
    }


    void Update()
    {
        if(isDead) return;
        if(player==null || crowd == null) return;
        
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        float distanceSquared = direction.sqrMagnitude;

        float attackDistanceSquared = attackDistance*attackDistance;

        if(distanceSquared > attackDistanceSquared)
        {
            MoveTowardsPlayer(direction);
        }
        else
        {
            TryAttack();
        }
    }

    void TryAttack()
    {
        if(Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + attackInterval;
        crowd.DamagePopulation(populationDamage);
        Debug.Log($"Enemy Attack!");
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
        {
            return;
        }
        currentHealth -= damage;
        if(currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        Destroy(gameObject);
    }

    void MoveTowardsPlayer(Vector3 direction)
    {   
        if (direction.sqrMagnitude < 0.001f)
            return;
        Vector3 moveDirection = direction.normalized;
        transform.position += moveDirection*moveSpeed*Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(moveDirection);
    }
}
