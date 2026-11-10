using System.Collections.Generic;
using UnityEngine;

/// <summary>Deterministic rarity and passive selection. Inventory and reserved
/// offers are explicit inputs, so simultaneous kills do not duplicate loot.</summary>
public static class RunLootPolicy
{
    public static ItemRarity RollEnemyRarity(int seed,float difficulty,bool elite)
    {
        var random=new ProceduralRandom(seed);
        float roll=random.Range(0f,1f);
        if(elite)return roll<.25f?ItemRarity.Epic:roll<.85f?ItemRarity.Rare:ItemRarity.Uncommon;
        float rareChance=Mathf.Clamp(.06f+(difficulty-1)*.05f,.06f,.20f);
        return roll<rareChance?ItemRarity.Rare:roll<rareChance+.30f?ItemRarity.Uncommon:ItemRarity.Common;
    }

    public static UpgradeData Choose(IReadOnlyList<UpgradeData> catalog,ItemRarity desired,
        ItemAcquisitionSource source,HashSet<int> owned,HashSet<int> reserved,int seed)
    {
        var candidates=new List<UpgradeData>();
        // Prefer the rolled quality, then a lower one. Never silently upgrade
        // a common sentry drop to a treasure-only epic.
        for(int rarity=(int)desired;rarity>=0;rarity--)
        {
            candidates.Clear();
            if(catalog!=null)foreach(var item in catalog)
            {
                if(item==null||item.Category!=ItemCategory.Passive||item.Rarity!=(ItemRarity)rarity||
                    !item.SupportsMode(GameplayMode.Runner)||!ItemAcquisitionPolicy.CanAcquire(item,source))continue;
                if(reserved!=null&&reserved.Contains(item.NetworkId))continue;
                if(owned!=null&&owned.Contains(item.NetworkId))continue;
                candidates.Add(item);
            }
            if(candidates.Count>0)
            {
                candidates.Sort((a,b)=>a.NetworkId.CompareTo(b.NetworkId));
                var random=new ProceduralRandom(seed);return candidates[random.Range(0,candidates.Count)];
            }
        }
        return null;
    }

    public static Color Tint(ItemRarity rarity)=>rarity switch
    {
        ItemRarity.Epic=>new Color(.68f,.48f,.20f),
        ItemRarity.Legendary=>new Color(.76f,.39f,.18f),
        ItemRarity.Rare=>new Color(.25f,.48f,.66f),
        ItemRarity.Uncommon=>new Color(.29f,.49f,.37f),_=>new Color(.43f,.48f,.44f)
    };
}
