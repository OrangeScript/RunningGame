using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-side build entry used while replacing a pawn during a mode/scene
/// transition. Temporary items retain their remaining gameplay time instead
/// of being refreshed by the transition.
/// </summary>
public readonly struct PlayerItemSnapshot
{
    public PlayerItemSnapshot(UpgradeData definition, float remainingTime)
    {
        Definition = definition;
        RemainingTime = remainingTime;
    }

    public UpgradeData Definition { get; }
    public float RemainingTime { get; }
}

/// <summary>
/// Server-authoritative inventory. Definitions are stable across modes while
/// runtimes interpret the current gameplay context.
/// </summary>
public class ItemManager : NetworkBehaviour
{
    private sealed class OwnedItem
    {
        public UpgradeData Definition;
        public ItemRuntime Runtime;
        public float RemainingTime;
    }

    [SerializeField] private GameplayMode startingMode = GameplayMode.Runner;

    private readonly List<OwnedItem> items = new List<OwnedItem>();
    private readonly List<ItemRuntime> activeItems = new List<ItemRuntime>();
    private readonly Dictionary<int,double> activeReadyAt = new Dictionary<int,double>();
    public int ActiveItemCount=>activeItems.Count;
    public UpgradeData GetActiveDefinition(int slot)
    {
        if(slot<0||slot>=activeItems.Count)return null;
        foreach(var entry in items)if(entry.Runtime==activeItems[slot])return entry.Definition;
        return null;
    }
    public float GetActiveCooldown(int slot)
    {
        if(slot<0||slot>=activeItems.Count)return 0;
        if(IsServer)return activeItems[slot].CooldownRemaining;
        return activeReadyAt.TryGetValue(slot,out var ready)?Mathf.Max(0,(float)(ready-NetworkManager.ServerTime.Time)):0;
    }

    private readonly NetworkVariable<GameplayMode> gameplayMode =
        new NetworkVariable<GameplayMode>(
            GameplayMode.Runner,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private readonly NetworkList<int> acquiredItemIds = new NetworkList<int>();

    public int itemCount => items.Count;
    public GameplayMode CurrentMode => gameplayMode.Value;
    public int SyncedItemCount => acquiredItemIds.Count;
    public float ArenaMoveSpeedMultiplier=>GetArenaStats().MoveSpeed;
    private ArenaBuildStats cachedStats;
    public ArenaBuildStats GetArenaStats()
    {
        if(cachedStats!=null)return cachedStats;
        cachedStats=new ArenaBuildStats();
        foreach(var item in items)item.Runtime.ModifyArenaStats(cachedStats);
        cachedStats.MoveSpeed=Mathf.Clamp(cachedStats.MoveSpeed,.7f,1.6f);
        return cachedStats;
    }
    public bool Owns(int networkId)=>Contains(networkId);

    public event Action<GameplayMode> ModeChanged;
    public event Action InventoryChanged;

    public override void OnNetworkSpawn()
    {
        gameplayMode.OnValueChanged += HandleModeChanged;
        acquiredItemIds.OnListChanged += HandleItemListChanged;

        if (IsServer)
            gameplayMode.Value = startingMode;
        else
            RebuildMirroredInventory();

        ModeChanged?.Invoke(gameplayMode.Value);
        InventoryChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        gameplayMode.OnValueChanged -= HandleModeChanged;
        acquiredItemIds.OnListChanged -= HandleItemListChanged;

        for (int i = items.Count - 1; i >= 0; i--)
            items[i].Runtime?.OnRemove();

        items.Clear();
        activeItems.Clear();
        cachedStats=null;
        activeReadyAt.Clear();
    }

    public int GetSyncedItemId(int index)
    {
        if (index < 0 || index >= acquiredItemIds.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        return acquiredItemIds[index];
    }

    /// <summary>Legacy/debug hook for an already-created runtime.</summary>
    public void AddItem(ItemRuntime item)
    {
        if (item == null)
            return;

        RegisterRuntime(null, item, 0f, false);
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (!ShouldAdvanceGameplayTime())
            return;

        float deltaTime = Time.deltaTime;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            OwnedItem entry = items[i];
            entry.Runtime.Tick(deltaTime);

            if (entry.Definition == null || entry.Definition.Category != ItemCategory.Temporary)
                continue;

            entry.RemainingTime -= deltaTime;
            if (entry.RemainingTime <= 0f)
                RemoveAt(i);
        }
    }

    public void ModifyProjectile(ProjectileSpec spec)
    {
        if (spec == null)
            return;

        spec.gameplayMode = CurrentMode;
        for (int i = 0; i < items.Count; i++)
            items[i].Runtime.ModifyProjectile(spec);
    }

    public void ModifyAttack(AttackPlan plan)
    {
        if (plan == null)
            return;

        plan.gameplayMode = CurrentMode;
        for (int i = 0; i < items.Count; i++)
            items[i].Runtime.ModifyAttack(plan);
    }

    public void AcquireServer(UpgradeData upgrade)
    {
        TryAcquireServer(upgrade, ItemAcquisitionSource.LevelReward);
    }

    public bool TryAcquireServer(UpgradeData upgrade, ItemAcquisitionSource source)
    {
        return TryRegisterDefinition(upgrade, source, null);
    }

    public void SetGameplayModeServer(GameplayMode mode)
    {
        if (!IsServer || gameplayMode.Value == mode)
            return;

        gameplayMode.Value = mode;
    }

    public int[] CaptureItemIds()
    {
        int[] snapshot = new int[items.Count];
        for (int i = 0; i < items.Count; i++)
            snapshot[i] = items[i].Definition != null ? items[i].Definition.NetworkId : 0;

        return snapshot;
    }

    public UpgradeData[] CaptureDefinitions()
    {
        List<UpgradeData> snapshot = new List<UpgradeData>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Definition != null)
                snapshot.Add(items[i].Definition);
        }

        return snapshot.ToArray();
    }

    public PlayerItemSnapshot[] CaptureBuildSnapshot()
    {
        var snapshot = new List<PlayerItemSnapshot>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            OwnedItem entry = items[i];
            if (entry.Definition == null)
                continue;

            snapshot.Add(new PlayerItemSnapshot(
                entry.Definition,
                entry.Definition.Category == ItemCategory.Temporary
                    ? Mathf.Max(0f, entry.RemainingTime)
                    : 0f));
        }

        return snapshot.ToArray();
    }

    public bool TryRestoreServer(PlayerItemSnapshot snapshot)
    {
        if (snapshot.Definition == null)
            return false;
        if (snapshot.Definition.Category == ItemCategory.Temporary &&
            snapshot.RemainingTime <= 0f)
        {
            return false;
        }

        float? remainingTime = snapshot.Definition.Category == ItemCategory.Temporary
            ? Mathf.Min(snapshot.RemainingTime, snapshot.Definition.TemporaryDuration)
            : null;
        return TryRegisterDefinition(
            snapshot.Definition,
            ItemAcquisitionSource.Debug,
            remainingTime);
    }

    public void RequestActivateActiveItem(int slotIndex)
    {
        if (!IsOwner)
            return;

        ActivateActiveItemServerRpc(slotIndex);
    }

    [ServerRpc]
    private void ActivateActiveItemServerRpc(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= activeItems.Count)
            return;

        bool success = activeItems[slotIndex].TryActivate();
        double readyAt=NetworkManager.ServerTime.Time+activeItems[slotIndex].CooldownRemaining;
        NotifyActivationResultClientRpc(success,slotIndex,readyAt);
    }

    [ClientRpc]
    private void NotifyActivationResultClientRpc(bool success,int slot,double readyAt)
    {
        if (!IsOwner)
            return;
        activeReadyAt[slot]=readyAt;
    }

    private void RegisterRuntime(
        UpgradeData definition,
        ItemRuntime runtime,
        float remainingTime,
        bool synchronize)
    {
        runtime.OnAcquire(this);
        cachedStats=null;
        items.Add(new OwnedItem
        {
            Definition = definition,
            Runtime = runtime,
            RemainingTime = remainingTime
        });

        if (runtime.CanActivate)
            activeItems.Add(runtime);

        if (synchronize && IsServer && definition != null)
            acquiredItemIds.Add(definition.NetworkId);

        InventoryChanged?.Invoke();
    }

    private bool TryRegisterDefinition(
        UpgradeData upgrade,
        ItemAcquisitionSource source,
        float? temporaryRemainingOverride)
    {
        if (!IsServer || upgrade == null)
            return false;

        if (!ItemAcquisitionPolicy.CanAcquire(upgrade, source))
        {
            Debug.LogWarning($"Item acquisition rejected: {upgrade.name}, source={source}");
            return false;
        }

        if (!upgrade.SupportsMode(CurrentMode))
            return false;

        if (upgrade.StackPolicy == ItemStackPolicy.Unique && Contains(upgrade.NetworkId))
            return false;

        ItemRuntime runtime = upgrade.CreateRuntime();
        if (runtime == null)
            return false;

        float duration = upgrade.Category == ItemCategory.Temporary
            ? Mathf.Max(0.01f, temporaryRemainingOverride ?? upgrade.TemporaryDuration)
            : 0f;
        RegisterRuntime(upgrade, runtime, duration, true);
        return true;
    }

    private bool ShouldAdvanceGameplayTime()
    {
        if (CurrentMode != GameplayMode.Runner)
            return true;

        RunnerController runner = GetComponent<RunnerController>();
        return runner == null ||
               runner.Phase == PlayerRunPhase.Running ||
               runner.Phase == PlayerRunPhase.Combat;
    }

    private void RemoveAt(int index)
    {
        cachedStats=null;
        OwnedItem entry = items[index];
        entry.Runtime.OnRemove();
        activeItems.Remove(entry.Runtime);
        items.RemoveAt(index);

        if (entry.Definition != null)
        {
            int syncedIndex = IndexOfSynced(entry.Definition.NetworkId);
            if (syncedIndex >= 0)
                acquiredItemIds.RemoveAt(syncedIndex);
        }

        InventoryChanged?.Invoke();
    }

    private bool Contains(int networkId)
    {
        for (int i = 0; i < items.Count; i++)
        {
            UpgradeData definition = items[i].Definition;
            if (definition != null && definition.NetworkId == networkId)
                return true;
        }

        return false;
    }

    private int IndexOfSynced(int networkId)
    {
        for (int i = 0; i < acquiredItemIds.Count; i++)
        {
            if (acquiredItemIds[i] == networkId)
                return i;
        }

        return -1;
    }

    private void HandleModeChanged(GameplayMode previous, GameplayMode current)
    {
        cachedStats=null;
        for (int i = 0; i < items.Count; i++)
            items[i].Runtime.OnModeChanged(previous, current);

        RebuildActiveItems();
        ModeChanged?.Invoke(current);
    }

    private void RebuildActiveItems()
    {
        activeItems.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Runtime.CanActivate)
                activeItems.Add(items[i].Runtime);
        }
    }

    private void HandleItemListChanged(NetworkListEvent<int> changeEvent)
    {
        cachedStats=null;
        if(!IsServer) RebuildMirroredInventory();
        InventoryChanged?.Invoke();
    }

    private void RebuildMirroredInventory()
    {
        if(RuntimeItemCatalog.Default!=null)
        {
            foreach(var entry in items)entry.Runtime.OnRemove();
            items.Clear();activeItems.Clear();
            foreach(int id in acquiredItemIds)
            {
                var definition=RuntimeItemCatalog.Default.Find(id);
                if(definition==null)continue;
                var runtime=definition.CreateRuntime();
                if(runtime!=null)RegisterRuntime(definition,runtime,0,false);
            }
        }
    }
}
