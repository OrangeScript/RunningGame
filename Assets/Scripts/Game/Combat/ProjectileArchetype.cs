using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "ProjectileArchetype",menuName ="Combat/Projectile Archetype")]
public class ProjectileArchetype : ScriptableObject
{
    
    [SerializeField] private Projectile projectilePrefab;

    [SerializeField] private float baseSpeed = 18f;
    [SerializeField] private float baseSize = 1f;

    [SerializeField] private ElementType baseElement = ElementType.None;
    public Projectile ProjectilePrefab => projectilePrefab;
    public ProjectileSpec CreateSpec(float damage)
    {
        return new ProjectileSpec
        {

            damage =
                damage,

            speed =
                baseSpeed,

            sizeMultiplier =
                baseSize,

            element =
                baseElement
        };
    }


}
