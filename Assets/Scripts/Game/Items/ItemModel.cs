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
    Debug = 4
}

[Flags]
public enum ItemAcquisitionMask : byte
{
    None = 0,
    LevelReward = 1 << 0,
    TreasureChest = 1 << 1,
    Event = 1 << 2,
    AllRewards = LevelReward | TreasureChest | Event
}

public enum ItemStackPolicy : byte
{
    Stack = 0,
    Unique = 1
}

/// <summary>
/// Central acquisition rules. Permanent items never drop directly inside a
/// lane, and Epic/Legendary items are reserved for explicit treasure rewards.
/// </summary>
public static class ItemAcquisitionPolicy
{
    public static bool CanAcquire(UpgradeData item, ItemAcquisitionSource source)
    {
        if (item == null)
            return false;

        if (source == ItemAcquisitionSource.Debug)
            return true;

        if (source == ItemAcquisitionSource.InLevelDrop)
            return false;

        if (item.Rarity >= ItemRarity.Epic && source != ItemAcquisitionSource.TreasureChest)
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
