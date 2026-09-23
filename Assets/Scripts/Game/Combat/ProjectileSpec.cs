using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;


[System.Serializable]
public class ProjectileSpec
{

    public float damage = 1f;
    public float speed = 10f;
    public float sizeMultiplier = 1f;
    public ElementType element = ElementType.None;

    public int bounce = 0;
    public float bounceRange = 6f;
    public float explosionRadius = 0f;
    public float explosionDamageMultiplier = 1f;
    public int pierce = 0;


    public ProjectileSpec Clone()
    {
        return new ProjectileSpec{
            damage = damage,
            speed = speed,
            sizeMultiplier = sizeMultiplier,
            element = element,
            bounce = bounce,
            bounceRange = bounceRange,
            pierce = pierce,
            explosionRadius = explosionRadius,
            explosionDamageMultiplier = explosionDamageMultiplier
        };
    }

}

public enum ElementType
{
    None,
    Ice,
    Fire,
    Lightning
}
