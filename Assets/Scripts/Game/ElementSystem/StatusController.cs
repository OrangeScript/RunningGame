using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class StatusController : NetworkBehaviour
{

    private class ElementAura
    {
        public float amount;
        public float remainingTime;
        public ElementAura(
            float amount,
            float remainingTime
        )
        {
            this.amount = amount;
            this.remainingTime = remainingTime;
        }
    }


    public event Action<ElementType,float> AuraChanged;
    public event Action<bool> FrozenChanged;
    public event Action<ElementReactionType> ReactionTriggered;

    [Header("Aura")]

    [SerializeField] float defaultAuraDuration = 4f;
    [SerializeField] float maxAuraAmount = 2f;

    [Header("Frozen")]
    [SerializeField] private float baseFrozenDuration = 2f;

    [Header("Overload")]
    [SerializeField] float overloadRadius = 3f;
    [SerializeField] float overloadDamageMultiplier = 0.5f;
    [SerializeField] LayerMask enemyLayer;


    [Header("Electro Charged")]

    [SerializeField]
    private float electroDuration = 2f;

    [SerializeField]
    private float electroTickInterval = 0.5f;

    [SerializeField]
    private float electroDamageMultiplier = 0.25f;

    [SerializeField]
    private float electroChainRange = 5f;


    private float electroRemainingTime;

    private float electroTickTimer;

    private float electroTickDamage;


    private readonly Dictionary<ElementType,ElementAura> auras = new();

    private readonly List<ElementType> expiredElements = new();

    private float frozenRemainingTime;

    private NetworkVariable<bool>
        frozenNetwork =
            new NetworkVariable<bool>();


    public bool IsFrozen =>
        frozenNetwork.Value;



    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(!IsServer) return;
        UpdateAuras();
        UpdateFrozen();
        UpdateElectroCharged();
    }

    private void UpdateElectroCharged()
    {
        if (
            electroRemainingTime <= 0f
        )
        {
            return;
        }


        electroRemainingTime -=
            Time.deltaTime;

        electroTickTimer -=
            Time.deltaTime;


        if (electroTickTimer > 0f)
            return;


        electroTickTimer =
            electroTickInterval;


        Enemy self =
            GetComponent<Enemy>();


        if (
            self != null &&
            !self.IsDead
        )
        {
            self.TakeDamage(
                electroTickDamage,
                ElementType.Lightning
            );
        }


        ChainElectroDamage();
    }

    private void ChainElectroDamage()
    {
        Collider[] hits =
            Physics.OverlapSphere(
                transform.position,
                electroChainRange,
                enemyLayer,
                QueryTriggerInteraction.Collide
            );


        Enemy self =
            GetComponent<Enemy>();


        Enemy nearest =
            null;

        float nearestSqr =
            Mathf.Infinity;


        foreach (Collider hit in hits)
        {
            Enemy enemy =
                hit.GetComponentInParent<Enemy>();


            if (enemy == null)
                continue;

            if (enemy == self)
                continue;

            if (enemy.IsDead)
                continue;


            Vector3 difference =
                enemy.transform.position -
                transform.position;


            float distanceSqr =
                difference.sqrMagnitude;


            if (distanceSqr < nearestSqr)
            {
                nearestSqr =
                    distanceSqr;

                nearest =
                    enemy;
            }
        }


        if (nearest != null)
        {
            nearest.TakeDamage(
                electroTickDamage,
                ElementType.Lightning
            );
        }
    }

    private void UpdateAuras()
    {
        if (auras.Count == 0)
            return;


        expiredElements.Clear();


        foreach (
            KeyValuePair<
                ElementType,
                ElementAura
            > pair in auras
        )
        {
            pair.Value.remainingTime -=
                Time.deltaTime;


            if (
                pair.Value.remainingTime <=
                0f
            )
            {
                expiredElements.Add(
                    pair.Key
                );
            }
        }


        foreach (
            ElementType element
            in expiredElements
        )
        {
            auras.Remove(
                element
            );
            AuraChanged?.Invoke(element,0f);
        }
    }


    private void UpdateFrozen()
    {
        if (frozenRemainingTime <= 0f)
            return;


        frozenRemainingTime -=
            Time.deltaTime;


        if (frozenRemainingTime < 0f && frozenNetwork.Value)
        {
            frozenRemainingTime = 0f;
            frozenNetwork.Value = false;
        }
    }

    public ReactionResult ApplyElement(
        ElementType incomingElement,
        float amount,
        float duration,
        float sourceDamage = 0f
    )
    {
        if(!IsServer) return ReactionResult.None;
        if(incomingElement == ElementType.None) return ReactionResult.None;
        if(amount <= 0f) return ReactionResult.None;
        if(duration <= 0f) duration = defaultAuraDuration;
        ElementType reactedElement = ElementType.None;
        ReactionResult reaction = ReactionResult.None;
        foreach(KeyValuePair<ElementType,ElementAura> pair in auras)
        {
            if (pair.Key == incomingElement) continue;
            if(pair.Value.amount <= 0f) continue;
            ReactionResult result = ElementReactionSystem.Resolve(pair.Key,incomingElement);

            if(result.Triggered)
            {
                reactedElement = pair.Key;
                reaction = result;
                break;
            }

        }

        if(reaction.Triggered)
        {
            ElementAura existingAura = auras[reactedElement];

            float reactionAmount = Mathf.Min(existingAura.amount,amount);
            
            existingAura.amount -= reactionAmount;
            amount -= reactionAmount;

            if(existingAura.amount <= 0.001f)
            { 
                auras.Remove(reactedElement);
                AuraChanged?.Invoke(reactedElement,0f);
            }
            else
            {
                AuraChanged?.Invoke(reactedElement,existingAura.amount);
            }
            ExecuteReaction(reaction,reactionAmount,sourceDamage);
            ReactionTriggered?.Invoke(reaction.reactionType);
        }

        if(amount >= 0.001f)
        {
            AddAura(incomingElement,amount,duration);
        }

        return reaction;
    }

    private void ExecuteReaction(ReactionResult reaction,float strength,float sourceDamage)
    {
        switch (reaction.reactionType)
        {
            case ElementReactionType.Frozen:
                ApplyFrozen(strength);
                break;
            case ElementReactionType.Melt:

                break;


            case ElementReactionType.Vaporize:

                break;


            case ElementReactionType.Overload:

                ApplyOverload(
                    sourceDamage
                );

                break;


            case ElementReactionType.ElectroCharged:

                ApplyElectroCharged(
                    sourceDamage
                );

                break;
            case ElementReactionType.SuperConduct:

                break;
        }
    }
    

    private void ApplyElectroCharged(
        float sourceDamage
    )
    {
        if (sourceDamage <= 0f)
            return;


        electroRemainingTime =
            electroDuration;


        electroTickTimer =
            0f;


        electroTickDamage =
            sourceDamage *
            electroDamageMultiplier;
    }



    private void ApplyOverload(
        float sourceDamage
    )
    {
        if (sourceDamage <= 0f)
            return;


        float overloadDamage =
            sourceDamage *
            overloadDamageMultiplier;


        Collider[] hits =
            Physics.OverlapSphere(
                transform.position,
                overloadRadius,
                enemyLayer,
                QueryTriggerInteraction.Collide
            );


        HashSet<Enemy> damaged =
            new HashSet<Enemy>();


        foreach (Collider hit in hits)
        {
            Enemy enemy =
                hit.GetComponentInParent<Enemy>();


            if (enemy == null)
                continue;

            if (enemy.IsDead)
                continue;

            if (!damaged.Add(enemy))
                continue;


            enemy.TakeDamage(
                overloadDamage,
                ElementType.Fire
            );
        }
    }

    private void ApplyFrozen(float strength)
    {
        if(!IsServer) return;
        float duration = baseFrozenDuration * strength;

        frozenRemainingTime = Mathf.Max(frozenRemainingTime,duration);

        frozenNetwork.Value = true;
    }

    //TODO: 万一和剩下的元素还能反应呢
    private void AddAura(ElementType element,float amount,float duration)
    {
        if(auras.TryGetValue(element,out ElementAura aura))
        {
            aura.amount =
                Mathf.Min(
                    maxAuraAmount,
                    aura.amount + amount
                );


            aura.remainingTime =
                Mathf.Max(
                    aura.remainingTime,
                    duration
                );
        }
        else
        {
            auras.Add(element,new ElementAura(Mathf.Min(amount,maxAuraAmount),duration));
        }

        AuraChanged?.Invoke(element,auras[element].amount);
    }
}
