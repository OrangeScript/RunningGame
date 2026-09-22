using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemManager : MonoBehaviour
{

    private readonly List<ItemRuntime> items =
        new List<ItemRuntime>();

    public int itemCount => items.Count;
    public void AddItem(ItemRuntime item)
    {
        items.Add(item);
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
    public void Acquire(UpgradeData upgrade)
    {
        if(upgrade == null) return;
        ItemRuntime runtime = upgrade.CreateRuntime();
        if(runtime == null) return;
        items.Add(runtime);

        Debug.Log( $"获得道具：{upgrade.UpgradeName}，当前道具数：{items.Count}");
    }
}
