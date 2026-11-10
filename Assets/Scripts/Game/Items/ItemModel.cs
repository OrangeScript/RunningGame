using System;

public enum ItemCategory : byte
{
    Passive = 0,
    Active = 1,
    Temporary = 2
}

public enum ItemRarity : byte
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4
}

public enum GameplayMode : byte
{
    Runner = 0,
    BossArena = 1,
    FirstPerson = 2
}

[Flags]
public enum ItemModeMask : byte
{
    None = 0,
    Runner = 1 << 0,
    BossArena = 1 << 1,
    FirstPerson = 1 << 2,
    All = Runner | BossArena | FirstPerson
}

public enum ItemAcquisitionSource : byte
{
    LevelReward = 0,
    TreasureChest = 1,
    Event = 2,
    InLevelDrop = 3,
    Debug = 4,
    RoadGate = 5
}

[Flags]
public enum ItemAcquisitionMask : byte
{
    None = 0,
    LevelReward = 1 << 0,
    TreasureChest = 1 << 1,
    Event = 1 << 2,
    InLevelDrop = 1 << 3,
    RoadGate = 1 << 4,
    AllRewards = LevelReward | TreasureChest | Event | InLevelDrop | RoadGate
}

public enum ItemStackPolicy : byte
{
    Stack = 0,
    Unique = 1
}

/// <summary>
/// Explicit sources remain authoritative. Road gates grant ordinary passives;
/// sentry/elite drops may grant the rarity rolled by the loot director.
/// </summary>
public static class ItemAcquisitionPolicy
{
    public static bool CanAcquire(UpgradeData item, ItemAcquisitionSource source)
    {
        if (item == null)
            return false;

        if (source == ItemAcquisitionSource.Debug)
            return true;

        bool road=source==ItemAcquisitionSource.InLevelDrop||source==ItemAcquisitionSource.RoadGate;
        if(road&&(item.Category!=ItemCategory.Passive||!item.SupportsMode(GameplayMode.Runner)))return false;

        if (item.Rarity >= ItemRarity.Epic && source != ItemAcquisitionSource.TreasureChest&&source!=ItemAcquisitionSource.InLevelDrop)
            return false;

        return item.IsSourceAllowed(source);
    }
}

[Serializable]
public sealed class ModeItemEffectBinding
{
    public GameplayMode mode;
    public ItemEffectDefiniton effect;
}
