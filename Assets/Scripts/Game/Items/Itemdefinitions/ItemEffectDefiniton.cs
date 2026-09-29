using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ItemEffectDefiniton : ScriptableObject
{
    public abstract ItemRuntime CreateRuntime();
}
