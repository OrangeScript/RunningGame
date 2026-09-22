using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName ="UpgradeData", menuName ="Roguelike/Upgrade Data")]
public class UpgradeData : ScriptableObject
{
    [SerializeField]private string upgradeName;
    [TextArea][SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [Header("RuntimeEffect")]
    [SerializeField] private ItemEffectDefiniton effect;

    public string UpgradeName
        => upgradeName;

    public string Description
        => description;

    public Sprite Icon
        => icon;

    public ItemRuntime CreateRuntime()
    {
        if(effect == null)
        {
            Debug.LogError( $"{upgradeName} 没有配置 ItemEffectDefinition");

            return null;
        }

        return effect.CreateRuntime();
    }

}
