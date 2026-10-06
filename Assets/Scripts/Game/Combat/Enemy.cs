using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public class Enemy : NetworkBehaviour
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
   private Transform player;
   private CrowdManager crowd;
   private float nextAttackTime;
   [SerializeField] private Transform visualRoot;
   [SerializeField] private Animator visualAnimator;
   private static readonly int HitTrigger = Animator.StringToHash("Hit");
   private StatusController statusController;

   private NetworkVariable<float>
        currentHealth =
            new NetworkVariable<float>();

    private NetworkVariable<bool>
        dead =
            new NetworkVariable<bool>();


    public bool IsDead =>
    dead.Value;

   
   #region Damage
   public event Action<float,ElementType> Damaged;
   public event Action<Enemy> Died;
   

    #endregion



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

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            FindNearestPlayer();
        }
    }

    void Awake()
    {
        statusController = GetComponent<StatusController>();

        if (visualRoot == null)
        {
            visualRoot = transform.Find("Visual");
        }
    }


    public ReactionResult ApplyElement(ElementType element,float amount = 1f,float duration = -1f,float sourceDamage = 0f)
    {
        if(statusController == null) return ReactionResult.None;

        return statusController.ApplyElement(element,amount,duration,sourceDamage);
    }


    private void FindNearestPlayer()
    {
        Transform nearest =
            null;

        CrowdManager nearestCrowd =
            null;

        float nearestSqr =
            Mathf.Infinity;


        foreach (
            var pair in
            NetworkManager.Singleton
                .ConnectedClients
        )
        {
            NetworkObject playerObject =
                pair.Value.PlayerObject;


            if (playerObject == null)
                continue;


            float sqr =
                (
                    playerObject.transform.position -
                    transform.position
                ).sqrMagnitude;


            if (sqr >= nearestSqr)
                continue;


            nearestSqr =
                sqr;


            nearest =
                playerObject.transform;


            nearestCrowd =
                playerObject.GetComponent<
                    CrowdManager
                >();
        }


        player =
            nearest;

        crowd =
            nearestCrowd;
    }

    void Update()
    {
        if(!IsServer) return;
        if(dead.Value) return;
        if (
            statusController != null &&
            statusController.IsFrozen
        )
        {
            return;
        }

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
        // crowd.DamagePopulation(populationDamage);
        Debug.Log($"Enemy Attack!");
    }

    public void TakeDamage(float damage,ElementType element)
    {
        if(!IsServer) return;
        if (dead.Value)
        {
            return;
        }
        currentHealth.Value -= damage;
        DamageFeedbackClientRpc(damage,element);

        

        if(currentHealth.Value <= 0)
        {
            Die();
        }
    }


    [ClientRpc]
    private void DamageFeedbackClientRpc(float damage, ElementType element)
    {
        Damaged?.Invoke(damage,element);
        if(visualAnimator != null)
        {
            visualAnimator.SetTrigger("Hit");
        }
    }

    void Die()
    {
        if(!IsServer) return;
        if(dead.Value) return;
        dead.Value = true;
        Died?.Invoke(this);
        NetworkObject.Despawn(true);
    }

    void MoveTowardsPlayer(Vector3 direction)
    {   
        if (direction.sqrMagnitude < 0.001f)
            return;
        Vector3 moveDirection = direction.normalized;
        transform.position += moveDirection*moveSpeed*Time.deltaTime;

        // Keep gameplay/UI anchors stable by rotating only the model.
        // Rotating the root makes offset children such as CombatTextAnchor
        // orbit around the enemy as soon as Play mode starts.
        Transform facingTarget = visualRoot != null ? visualRoot : transform;
        facingTarget.rotation = Quaternion.LookRotation(moveDirection);
    }
}
