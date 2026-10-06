using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ItemManager : NetworkBehaviour
{

    private readonly List<ItemRuntime> items =
        new List<ItemRuntime>();

    private readonly List<ActiveAttackItemRuntime>
    activeItems =
        new List<ActiveAttackItemRuntime>();

    public int itemCount => items.Count;
    public void AddItem(ItemRuntime item)
    {
        items.Add(item);
    }

    void Update()
    {
        if(!IsServer) return;
            for (
                int i = 0;
                i < items.Count;
                i++
            )
            {
                items[i].Tick(
                    Time.deltaTime
                );
            }
    }
    public void ModifyProjectile(ProjectileSpec spec)
    {
        foreach(ItemRuntime item in items)
        {
            item.ModifyProjectile(spec);
        }
    }
    public void ModifyAttack(AttackPlan plan)
    {
        foreach(ItemRuntime item in items)
        {
            item.ModifyAttack(plan);
        }
    }

    public void AcquireServer(
        UpgradeData upgrade
    )
    {
        if(!IsServer) return;
        if(upgrade == null) return;
        ItemRuntime runtime = upgrade.CreateRuntime();
        if(runtime == null) return;
        runtime.OnAcquire(this);
        items.Add(runtime);
        if (
            runtime is
            ActiveAttackItemRuntime activeItem
        )
        {
            activeItems.Add(
                activeItem
            );
        }

        ItemVisualAcquiredClientRpc(upgrade.NetworkId);
    }

    public void Acquire(UpgradeData upgrade)
    {
        if(upgrade == null) return;
        ItemRuntime runtime = upgrade.CreateRuntime();
        if(runtime == null) return;
        runtime.OnAcquire(this);
        items.Add(runtime);
        if (
            runtime is ActiveAttackItemRuntime activeItem
        )
        {
            activeItems.Add(
                activeItem
            );
        }

        Debug.Log( $"获得道具：{upgrade.UpgradeName}，当前道具数：{items.Count}");
    }

    public bool TryActivateActiveItem(
        int slotIndex
    )
    {
        if (
            slotIndex < 0 ||
            slotIndex >= activeItems.Count
        )
        {
            return false;
        }


        return activeItems[
            slotIndex
        ].TryActivate();
    }

    public void RequestActivateActiveItem(
        int slotIndex
    )
    {
        if (!IsOwner)
            return;


        ActivateActiveItemServerRpc(
            slotIndex
        );
    }


    [ServerRpc]
    private void ActivateActiveItemServerRpc(
        int slotIndex
    )
    {
        if (
            slotIndex < 0 ||
            slotIndex >= activeItems.Count
        )
        {
            return;
        }


        activeItems[
            slotIndex
        ].TryActivate();
    }
}
