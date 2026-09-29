using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName ="EarthBullet",menuName ="Roguelike/Effects/EArth")]
public class EarthCoreDefinition : ItemEffectDefiniton
{
    public override ItemRuntime CreateRuntime()
    {
        return new EarthCoreRuntime();
    }
}
