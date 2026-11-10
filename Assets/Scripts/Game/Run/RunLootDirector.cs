using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>Scene-local loot replication. Clients render cards; only the server
/// selects rewards, checks a lane's pickup distance and mutates inventory.</summary>
public sealed class RunLootDirector : MonoBehaviour
{
    private const string SpawnMessage="Running.Loot.Spawn.v1",RemoveMessage="Running.Loot.Remove.v1",
        ClearMessage="Running.Loot.Clear.v1",NoticeMessage="Running.Loot.Notice.v1";
    private sealed class Pickup
    {
        public uint Id;public int Lane,ItemId;public ItemRarity Rarity;public Vector3 Position;public GameObject Visual;
    }
    private readonly Dictionary<uint,Pickup> pickups=new Dictionary<uint,Pickup>();
    private readonly Dictionary<int,HashSet<int>> reserved=new Dictionary<int,HashSet<int>>();
    private readonly HashSet<Enemy> watched=new HashSet<Enemy>();
    private readonly HashSet<int> gateOfferLanes=new HashSet<int>();
    private readonly List<uint> removeBuffer=new List<uint>();
    private NetworkManager manager;
    private GameManager game;
    private bool registered;
    private uint nextId;
    public int ActivePickupCount=>pickups.Count;
    public uint LastSpawnedPickupId=>nextId;
    public bool HasGateOffer(int lane)=>gateOfferLanes.Contains(lane);
    public int LaneFirepower(int lane)=>FindRunner(lane)?.GetComponent<CrowdManager>()?.Firepower??20;
    public bool TryGetPickup(uint id,out Vector3 position,out int lane,out int itemId)
    {
        if(pickups.TryGetValue(id,out var pickup)){position=pickup.Position;lane=pickup.Lane;itemId=pickup.ItemId;return true;}
        position=Vector3.zero;lane=-1;itemId=0;return false;
    }
    public static event Action<UpgradeData,int,ItemRarity> LocalReward;
    public static RunLootDirector Ensure(GameObject owner)=>owner.GetComponent<RunLootDirector>()??owner.AddComponent<RunLootDirector>();
    private bool IsServer=>game!=null&&game.IsSpawned&&game.IsServer;
    private ulong Epoch=>game!=null&&game.IsSpawned?game.NetworkObjectId:ulong.MaxValue;
    private void Awake(){game=GetComponent<GameManager>();manager=NetworkManager.Singleton;Enemy.ServerSpawned+=Watch;}
    private void OnDestroy()
    {
        Enemy.ServerSpawned-=Watch;
        foreach(var enemy in watched)if(enemy!=null)enemy.Died-=OnEnemyDied;
        if(registered&&manager!=null&&manager.CustomMessagingManager!=null)
        {
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(SpawnMessage);
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(RemoveMessage);
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(ClearMessage);
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(NoticeMessage);
        }
        ClearLocal();
    }
    private void Update()
    {
        manager=NetworkManager.Singleton;
        if(manager==null||!manager.IsListening)return;
        if(!registered)
        {
            var messages=manager.CustomMessagingManager;
            messages.RegisterNamedMessageHandler(SpawnMessage,ReceiveSpawn);
            messages.RegisterNamedMessageHandler(RemoveMessage,ReceiveRemove);
            messages.RegisterNamedMessageHandler(ClearMessage,ReceiveClear);
            messages.RegisterNamedMessageHandler(NoticeMessage,ReceiveNotice);registered=true;
        }
        if(!IsServer)return;
        removeBuffer.Clear();
        foreach(var pickup in pickups.Values)
        {
            RunnerController runner=FindRunner(pickup.Lane);
            if(runner==null||!runner.IsAdvancing)continue;
            var items=runner.GetComponent<ItemManager>();
            float radius=items!=null?items.GetRunnerStats().PickupRadius:1.1f;
            Vector3 delta=runner.transform.position-pickup.Position;delta.y=0;
            if(delta.sqrMagnitude<=radius*radius)
            {
                Grant(runner,pickup.ItemId,pickup.Rarity,ItemAcquisitionSource.InLevelDrop);
                removeBuffer.Add(pickup.Id);
            }
            else if(runner.transform.position.z>pickup.Position.z+7)removeBuffer.Add(pickup.Id);
        }
        foreach(uint id in removeBuffer)RemoveServer(id);
    }
    private static RunnerController FindRunner(int lane)
    {
        foreach(var runner in RunnerController.ActiveRunners)if(runner!=null&&runner.IsSpawned&&runner.LaneId==lane)return runner;
        return null;
    }
    private HashSet<int> Reservations(int lane)
    {
        if(!reserved.TryGetValue(lane,out var ids))reserved[lane]=ids=new HashSet<int>();return ids;
    }
    private static HashSet<int> Owned(RunnerController runner)
    {
        var result=new HashSet<int>();var items=runner!=null?runner.GetComponent<ItemManager>():null;
        if(items!=null)for(int i=0;i<items.SyncedItemCount;i++)result.Add(items.GetSyncedItemId(i));return result;
    }
    public UpgradeData ChooseGateItem(int lane,int seed,ItemRarity quality=ItemRarity.Uncommon)
    {
        var item=RunLootPolicy.Choose(RuntimeItemCatalog.Default?.Items,quality,ItemAcquisitionSource.RoadGate,
            Owned(FindRunner(lane)),Reservations(lane),seed);
        if(item!=null){Reservations(lane).Add(item.NetworkId);gateOfferLanes.Add(lane);}return item;
    }
    private void Watch(Enemy enemy)
    {
        if(!IsServer||enemy==null||!watched.Add(enemy))return;
        enemy.Died+=OnEnemyDied;
    }
    private void OnEnemyDied(Enemy enemy)
    {
        if(enemy==null)return;enemy.Died-=OnEnemyDied;watched.Remove(enemy);
        if(!IsServer)return;
        var runner=FindRunner(enemy.LaneId);if(runner==null||!runner.IsAdvancing)return;
        int healing=runner.GetComponent<ItemManager>()?.GetRunnerStats().HealthOnKill??0;
        if(healing>0)runner.GetComponent<CrowdManager>()?.HealHealth(healing);
        bool elite=game.IsEliteRoad&&(enemy.Archetype==EnemyArchetype.Charger||enemy.Archetype==EnemyArchetype.Ranged);
        int seed=ProceduralRandom.Derive(enemy.ContentSeed,8801,enemy.LaneId);
        var quality=RunLootPolicy.RollEnemyRarity(seed,enemy.Difficulty,elite);
        var item=RunLootPolicy.Choose(RuntimeItemCatalog.Default?.Items,quality,ItemAcquisitionSource.InLevelDrop,
            Owned(runner),Reservations(enemy.LaneId),seed);
        SpawnServer(enemy.transform.position,enemy.LaneId,item,quality);
    }
    public uint SpawnServer(Vector3 position,int lane,UpgradeData item,ItemRarity quality)
    {
        if(!IsServer)return 0;
        if(!ArenaProtocol.IsFinite(position))return 0;
        var owner=FindRunner(lane);
        if(owner==null||!owner.IsAdvancing)return 0;
        if(item!=null&&!ItemAcquisitionPolicy.CanAcquire(item,ItemAcquisitionSource.InLevelDrop))return 0;
        if(owner!=null)position.x=Mathf.Clamp(position.x,owner.LaneCenterX-owner.MovementHalfWidth,owner.LaneCenterX+owner.MovementHalfWidth);
        position.y=.04f;
        var pickup=new Pickup{Id=++nextId,Lane=lane,ItemId=item!=null?item.NetworkId:0,Rarity=item!=null?item.Rarity:quality,Position=position};
        AddLocal(pickup);if(item!=null)Reservations(lane).Add(item.NetworkId);
        using(var writer=new FastBufferWriter(64,Allocator.Temp))
        {
            writer.WriteValueSafe(Epoch);writer.WriteValueSafe(pickup.Id);writer.WriteValueSafe(lane);
            writer.WriteValueSafe(pickup.ItemId);writer.WriteValueSafe((byte)pickup.Rarity);writer.WriteValueSafe(position);
            manager.CustomMessagingManager.SendNamedMessageToAll(SpawnMessage,writer,NetworkDelivery.ReliableSequenced);
        }
        return pickup.Id;
    }
    public void GrantGate(RunnerController runner,int itemId)
    {
        if(!IsServer||runner==null||!runner.IsAdvancing)return;
        var item=RuntimeItemCatalog.Default?.Find(itemId);
        Grant(runner,itemId,item!=null?item.Rarity:ItemRarity.Common,ItemAcquisitionSource.RoadGate);
    }
    private void Grant(RunnerController runner,int itemId,ItemRarity rarity,ItemAcquisitionSource source)
    {
        var definition=RuntimeItemCatalog.Default?.Find(itemId);
        bool acquired=definition!=null&&runner.GetComponent<ItemManager>()?.TryAcquireServer(definition,source)==true;
        int salvage=0;
        if(!acquired)
        {
            // Exhausted/duplicate passives become an explicit amplification,
            // never a silent failed pickup or an endlessly rerolled card.
            salvage=3+(int)rarity*3;runner.GetComponent<CrowdManager>()?.AddShield(salvage);
        }
        Reservations(runner.LaneId).Remove(itemId);
        ShowNotice(runner.OwnerClientId,acquired?itemId:0,salvage,rarity);
        using(var writer=new FastBufferWriter(40,Allocator.Temp))
        {
            writer.WriteValueSafe(Epoch);writer.WriteValueSafe(runner.OwnerClientId);
            writer.WriteValueSafe(acquired?itemId:0);writer.WriteValueSafe(salvage);writer.WriteValueSafe((byte)rarity);
            manager.CustomMessagingManager.SendNamedMessageToAll(NoticeMessage,writer,NetworkDelivery.ReliableSequenced);
        }
    }
    public void ClearRoadServer()
    {
        if(!IsServer)return;
        foreach(var enemy in watched)if(enemy!=null)enemy.Died-=OnEnemyDied;
        watched.Clear();
        ClearLocal();reserved.Clear();gateOfferLanes.Clear();
        if(manager==null)manager=NetworkManager.Singleton;
        using(var writer=new FastBufferWriter(8,Allocator.Temp))
        {writer.WriteValueSafe(Epoch);manager.CustomMessagingManager.SendNamedMessageToAll(ClearMessage,writer,NetworkDelivery.ReliableSequenced);}
    }
    private void RemoveServer(uint id)
    {
        RemoveLocal(id);
        using(var writer=new FastBufferWriter(16,Allocator.Temp))
        {writer.WriteValueSafe(Epoch);writer.WriteValueSafe(id);manager.CustomMessagingManager.SendNamedMessageToAll(RemoveMessage,writer,NetworkDelivery.ReliableSequenced);}
    }
    private void AddLocal(Pickup pickup)
    {
        if(pickups.ContainsKey(pickup.Id))return;
        pickup.Visual=RunItemPickupVisual.Create(pickup.Position,RuntimeItemCatalog.Default?.Find(pickup.ItemId),pickup.Rarity,pickup.Id);
        pickups.Add(pickup.Id,pickup);
    }
    private void RemoveLocal(uint id)
    {
        if(!pickups.TryGetValue(id,out var pickup))return;
        if(pickup.Visual!=null)Destroy(pickup.Visual);
        if(reserved.TryGetValue(pickup.Lane,out var ids))ids.Remove(pickup.ItemId);pickups.Remove(id);
    }
    private void ClearLocal()
    {foreach(var pickup in pickups.Values)if(pickup.Visual!=null)Destroy(pickup.Visual);pickups.Clear();}
    private bool Accept(ulong sender,ref FastBufferReader reader)
    {reader.ReadValueSafe(out ulong epoch);return !IsServer&&sender==NetworkManager.ServerClientId&&epoch==Epoch;}
    private void ReceiveSpawn(ulong sender,FastBufferReader reader)
    {
        if(!Accept(sender,ref reader))return;
        reader.ReadValueSafe(out uint id);reader.ReadValueSafe(out int lane);reader.ReadValueSafe(out int itemId);
        reader.ReadValueSafe(out byte rarity);reader.ReadValueSafe(out Vector3 position);
        AddLocal(new Pickup{Id=id,Lane=lane,ItemId=itemId,Rarity=(ItemRarity)rarity,Position=position});
    }
    private void ReceiveRemove(ulong sender,FastBufferReader reader)
    {if(!Accept(sender,ref reader))return;reader.ReadValueSafe(out uint id);RemoveLocal(id);}
    private void ReceiveClear(ulong sender,FastBufferReader reader)
    {if(Accept(sender,ref reader)){ClearLocal();Projectile.ClearAll();}}
    private void ReceiveNotice(ulong sender,FastBufferReader reader)
    {
        if(!Accept(sender,ref reader))return;
        reader.ReadValueSafe(out ulong owner);reader.ReadValueSafe(out int itemId);reader.ReadValueSafe(out int salvage);reader.ReadValueSafe(out byte rarity);
        ShowNotice(owner,itemId,salvage,(ItemRarity)rarity);
    }
    private void ShowNotice(ulong owner,int itemId,int salvage,ItemRarity rarity)
    {if(manager!=null&&manager.LocalClientId==owner)LocalReward?.Invoke(RuntimeItemCatalog.Default?.Find(itemId),salvage,rarity);}
}
