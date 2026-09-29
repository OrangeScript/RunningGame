using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElementReactionSystem : MonoBehaviour
{
    public static ReactionResult Resolve(
        ElementType existingElement,
        ElementType incomingElement
    )
    {
        if(existingElement == ElementType.None || incomingElement == ElementType.None)
        {
            return ReactionResult.None;
        }

        if(existingElement == incomingElement)
        {
            return ReactionResult.None;
        }

        //Frozen
        if(
            IsPair(
                existingElement,
                incomingElement,
                ElementType.Water,
                ElementType.Ice
            )
        )
        {
            return new ReactionResult(ElementReactionType.Frozen,1f);
        }


        //Melt
        if(existingElement == ElementType.Ice && incomingElement == ElementType.Fire)
        {
            return new ReactionResult(ElementReactionType.Melt,2f);
        }

        if(existingElement == ElementType.Fire && incomingElement == ElementType.Ice)
        {
            return new ReactionResult(ElementReactionType.Melt,1.5f);
        }
        // Fire aura + Water hit
        if (
            existingElement == ElementType.Fire &&
            incomingElement == ElementType.Water
        )
        {
            return new ReactionResult(
                ElementReactionType.Vaporize,
                2f
            );
        }


        // Water aura + Fire hit
        if (
            existingElement == ElementType.Water &&
            incomingElement == ElementType.Fire
        )
        {
            return new ReactionResult(
                ElementReactionType.Vaporize,
                1.5f
            );
        }

        if (
            IsPair(
                existingElement,
                incomingElement,
                ElementType.Fire,
                ElementType.Lightning
            )
        )
        {
            return new ReactionResult(
                ElementReactionType.Overload,
                1f
            );
        }

        if (
            IsPair(
                existingElement,
                incomingElement,
                ElementType.Water,
                ElementType.Lightning
            )
        )
        {
            return new ReactionResult(
                ElementReactionType.ElectroCharged,
                1f
            );
        }


        return ReactionResult.None;


    }

    public static bool IsPair(
        ElementType a,
        ElementType b,
        ElementType first,
        ElementType second
    )
    {
        return
            (a == first && b == second) ||
            (a == second && b == first);
    }
}



    public enum ElementReactionType
    {
        None,
        Frozen,
        Melt,
        Vaporize,
        ElectroCharged,
        Overload,
        SuperConduct
    }