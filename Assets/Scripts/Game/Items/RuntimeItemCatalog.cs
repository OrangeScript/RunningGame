using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Roguelike/Item Catalog")]
public sealed class RuntimeItemCatalog : ScriptableObject
{
    [SerializeField] private UpgradeData[] items;
    private Dictionary<int,UpgradeData> lookup;
    private static RuntimeItemCatalog instance;
    public static RuntimeItemCatalog Default=>instance!=null?instance:(instance=Resources.Load<RuntimeItemCatalog>("ItemCatalog"));
    public IReadOnlyList<UpgradeData> Items=>items;
    public UpgradeData Find(int id)
    {
        if(lookup==null)
        {
            lookup=new Dictionary<int,UpgradeData>();
            if(items!=null)foreach(var item in items)if(item!=null)lookup[item.NetworkId]=item;
        }
        return lookup.TryGetValue(id,out var value)?value:null;
    }
}
