using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "IceCoreEffect",
    menuName = "Roguelike/Effects/Ice Core"
)]
public class IceCoreDefinition
    : ItemEffectDefiniton
{
    public override ItemRuntime CreateRuntime()
    {
        return new IceCoreRuntime();
    }
}