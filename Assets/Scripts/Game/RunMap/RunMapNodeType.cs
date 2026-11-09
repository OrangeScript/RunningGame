namespace Running.Game.RunMap
{
    /// <summary>
    /// Stable node identifiers used by generation, save snapshots and networking.
    /// Never reorder existing values; append new node types instead.
    /// </summary>
    public enum RunMapNodeType : byte
    {
        Combat = 0,
        Elite = 1,
        Event = 2,
        Reward = 3,
        Treasure = 4,
        Boss = 5
    }
}
