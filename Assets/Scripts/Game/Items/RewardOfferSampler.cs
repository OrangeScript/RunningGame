using System.Collections.Generic;
using Running.Game.Run.Generation;

/// <summary>Prefer unseen, unowned items; deterministically degrade to stacked
/// choices only when the source pool is genuinely too small.</summary>
public static class RewardOfferSampler
{
    public static int[] Draw(List<UpgradeData> source,HashSet<int> owned,HashSet<int> recent,int seed,int count=3)
    {
        var fresh=source.FindAll(item=>!owned.Contains(item.NetworkId)&&!recent.Contains(item.NetworkId));
        var pool=new List<UpgradeData>(fresh.Count>=count?fresh:source);
        pool.Sort((a,b)=>a.NetworkId.CompareTo(b.NetworkId));
        var random=new DeterministicRandom(seed);
        var result=new List<int>();
        while(result.Count<count&&pool.Count>0)
        {
            double total=0;foreach(var item in pool)total+=Weight(item,owned,recent);
            double roll=random.NextUnitDouble()*total;int index=pool.Count-1;
            for(int i=0;i<pool.Count;i++) { roll-=Weight(pool[i],owned,recent);if(roll<=0){index=i;break;} }
            result.Add(pool[index].NetworkId);pool.RemoveAt(index);
        }
        return result.ToArray();
    }
    private static double Weight(UpgradeData item,HashSet<int> owned,HashSet<int> recent)
        =>(owned.Contains(item.NetworkId)?.15:1)*(recent.Contains(item.NetworkId)?.08:1);
}
