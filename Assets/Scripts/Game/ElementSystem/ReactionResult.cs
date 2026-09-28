using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ReactionResult
{
    public ElementReactionType reactionType;
    public float damageMultiplier;

    public bool Triggered => reactionType != ElementReactionType.None;

    public ReactionResult(ElementReactionType reactionType,
        float damageMultiplier)
    {
        this.reactionType = reactionType;
        this.damageMultiplier = damageMultiplier;
    }

    public static ReactionResult None => new ReactionResult(ElementReactionType.None,1f);
}
