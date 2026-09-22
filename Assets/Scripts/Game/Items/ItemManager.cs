using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemManager : MonoBehaviour
{

    private readonly List<ItemRuntime> items =
        new List<ItemRuntime>();


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
}
