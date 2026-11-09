using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName ="UpgradeData", menuName ="Roguelike/Upgrade Data")]
public class UpgradeData : ScriptableObject
{
    [SerializeField] private int networkId;
    public int NetworkId => networkId != 0 ? networkId : ComputeStableId(name);
    [SerializeField]private string upgradeName;
    [TextArea][SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [Header("RuntimeEffect")]
    [SerializeField] private ItemEffectDefiniton effect;

    [Header("Item Rules")]
    [SerializeField] private ItemCategory category = ItemCategory.Passive;
    [SerializeField] private ItemRarity rarity = ItemRarity.Common;
    [SerializeField] private ItemStackPolicy stackPolicy = ItemStackPolicy.Stack;
    [SerializeField] private ItemModeMask supportedModes = ItemModeMask.All;
    [SerializeField] private ItemAcquisitionMask acquisitionSources = ItemAcquisitionMask.AllRewards;
    [SerializeField, Min(0.1f)] private float temporaryDuration = 30f;

    [Header("Optional mode-specific effects")]
    [SerializeField] private ModeItemEffectBinding[] modeEffects;

    public string UpgradeName
        => upgradeName;

    public string Description
        => description;

    public Sprite Icon
        => icon;

    public ItemCategory Category => category;
    public ItemRarity Rarity => rarity;
    public ItemStackPolicy StackPolicy => stackPolicy;
    public float TemporaryDuration => Mathf.Max(0.1f, temporaryDuration);

    public bool SupportsMode(GameplayMode mode)
    {
        ItemModeMask mask = supportedModes == ItemModeMask.None
            ? ItemModeMask.All
            : supportedModes;

        ItemModeMask requested = mode switch
        {
            GameplayMode.Runner => ItemModeMask.Runner,
            GameplayMode.BossArena => ItemModeMask.BossArena,
            GameplayMode.FirstPerson => ItemModeMask.FirstPerson,
            _ => ItemModeMask.None
        };

        return (mask & requested) != 0;
    }

    public bool IsSourceAllowed(ItemAcquisitionSource source)
    {
        ItemAcquisitionMask mask = acquisitionSources == ItemAcquisitionMask.None
            ? ItemAcquisitionMask.AllRewards
            : acquisitionSources;

        ItemAcquisitionMask requested = source switch
        {
            ItemAcquisitionSource.LevelReward => ItemAcquisitionMask.LevelReward,
            ItemAcquisitionSource.TreasureChest => ItemAcquisitionMask.TreasureChest,
            ItemAcquisitionSource.Event => ItemAcquisitionMask.Event,
            ItemAcquisitionSource.Debug => ItemAcquisitionMask.AllRewards,
            _ => ItemAcquisitionMask.None
        };

        return (mask & requested) != 0;
    }

    public ItemRuntime CreateRuntime()
    {
        ItemRuntime fallback = effect != null ? effect.CreateRuntime() : null;
        Dictionary<GameplayMode, ItemRuntime> runtimes =
            new Dictionary<GameplayMode, ItemRuntime>();

        if (modeEffects != null)
        {
            for (int i = 0; i < modeEffects.Length; i++)
            {
                ModeItemEffectBinding binding = modeEffects[i];
                if (binding == null || binding.effect == null)
                    continue;

                runtimes[binding.mode] = binding.effect.CreateRuntime();
            }
        }

        if (runtimes.Count > 0)
            return new ContextualItemRuntime(fallback, runtimes);

        if(fallback == null)
        {
            Debug.LogError( $"{upgradeName} 没有配置 ItemEffectDefinition");

            return null;
        }

        return fallback;
    }

    private static int ComputeStableId(string value)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            uint hash = offset;
            string text = string.IsNullOrEmpty(value) ? "UnnamedUpgrade" : value;

            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= prime;
            }

            int result = (int)(hash & 0x7fffffff);
            return result == 0 ? 1 : result;
        }
    }

}
