using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[CreateAssetMenu(fileName ="BounceEffect",menuName ="Roguelike/Effects/Bounce")]
public class BounceDefinition : ItemEffectDefiniton
{

    [SerializeField] private int extraBounces = 2;
    [SerializeField] private float bounceRange = 6f;
    public override ItemRuntime CreateRuntime()
    {
        return new BounceRuntime(extraBounces,bounceRange);
    }
}
