using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatusController : MonoBehaviour
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

    private readonly Dictionary<ElementType,ElementAura> auras = new();

    private readonly List<ElementType> expiredElements = new();

    private float frozenRemainingTime;
    public bool IsFrozen => frozenRemainingTime > 0f;




    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        UpdateAuras();
        UpdateFrozen();
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


        if (frozenRemainingTime < 0f)
        {
            frozenRemainingTime = 0f;
            FrozenChanged?.Invoke(false);
        }
    }

    public ReactionResult ApplyElement(
        ElementType incomingElement,
        float amount,
        float duration
    )
    {
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
            ExecuteReaction(reaction,reactionAmount);
            ReactionTriggered?.Invoke(reaction.reactionType);
        }

        if(amount >= 0.001f)
        {
            AddAura(incomingElement,amount,duration);
        }

        return reaction;
    }

    private void ExecuteReaction(ReactionResult reaction,float strength)
    {
        switch (reaction.reactionType)
        {
            case ElementReactionType.Frozen:
                ApplyFrozen(strength);
                break;
        }
    }

    private void ApplyFrozen(float strength)
    {
        bool wasFrozen = IsFrozen;
        float duration = baseFrozenDuration * strength;

        frozenRemainingTime = Mathf.Max(frozenRemainingTime,duration);

        if (!wasFrozen)
        {
            FrozenChanged?.Invoke(true);
        }
        Debug.Log("Frozen!");
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
