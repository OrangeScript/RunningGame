using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>NetworkObject ids distinguish consecutive roads; seed also protects
/// rebuilds of one section. No prefab NetworkBehaviour layout is changed.</summary>
public readonly struct RunnerObstacleIdentity : IEquatable<RunnerObstacleIdentity>
{
    public readonly ulong Section;
    public readonly int Seed,Index;
    public RunnerObstacleIdentity(ulong section,int seed,int index){Section=section;Seed=seed;Index=index;}
    public bool Equals(RunnerObstacleIdentity other)=>Section==other.Section&&Seed==other.Seed&&Index==other.Index;
    public override bool Equals(object obj)=>obj is RunnerObstacleIdentity other&&Equals(other);
    public override int GetHashCode()=>unchecked(((Section.GetHashCode()*397)^Seed)*397^Index);
}

/// <summary>Durability/destruction replication for scene-local seeded cover.
/// Early messages are cached until the matching client's geometry is built.</summary>
public sealed class RunnerObstacleReplication : MonoBehaviour
{
    private const string Message="Running.Obstacle.State.v1";
    public static RunnerObstacleReplication Instance { get; private set; }
    private readonly Dictionary<RunnerObstacleIdentity,RunnerObstacleRuntime> instances=new Dictionary<RunnerObstacleIdentity,RunnerObstacleRuntime>();
    private readonly Dictionary<RunnerObstacleIdentity,State> states=new Dictionary<RunnerObstacleIdentity,State>();
    private readonly Queue<RunnerObstacleIdentity> stateOrder=new Queue<RunnerObstacleIdentity>();
    private NetworkManager manager;
    private bool subscribed;
    private readonly struct State
    {
        public readonly float Health;public readonly bool Destroyed;
        public State(float health,bool destroyed){Health=health;Destroyed=destroyed;}
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install(){Ensure();}
    public static RunnerObstacleReplication Ensure()
    {
        if(!Application.isPlaying)return null;
        if(Instance==null)
        {
            var root=new GameObject("RunnerObstacleReplication");DontDestroyOnLoad(root);
            Instance=root.AddComponent<RunnerObstacleReplication>();
        }
        Instance.Attach();return Instance;
    }
    private void Update()=>Attach();
    private void Attach()
    {
        var current=NetworkManager.Singleton;
        if(current==null||!current.IsListening||current.CustomMessagingManager==null)
        {
            if(subscribed)Detach();
            return;
        }
        if(subscribed&&manager==current)return;
        if(subscribed)Detach();
        manager=current;manager.CustomMessagingManager.RegisterNamedMessageHandler(Message,Receive);subscribed=true;
    }
    private void Detach()
    {
        if(manager!=null&&manager.CustomMessagingManager!=null)
            manager.CustomMessagingManager.UnregisterNamedMessageHandler(Message);
        manager=null;subscribed=false;states.Clear();stateOrder.Clear();instances.Clear();
    }
    public void Register(RunnerObstacleRuntime obstacle)
    {
        Attach();if(obstacle==null)return;
        instances[obstacle.Identity]=obstacle;
        if(states.TryGetValue(obstacle.Identity,out var state))obstacle.ApplyState(state.Health,state.Destroyed);
    }
    public void Unregister(RunnerObstacleRuntime obstacle)
    {
        if(obstacle==null)return;
        if(instances.TryGetValue(obstacle.Identity,out var current)&&current==obstacle)instances.Remove(obstacle.Identity);
    }
    public void Publish(RunnerObstacleRuntime obstacle)
    {
        Attach();
        if(manager==null||!manager.IsServer||obstacle==null)return;
        var key=obstacle.Identity;var state=new State(obstacle.HealthFraction,obstacle.IsDestroyed);
        Remember(key,state);
        using(var writer=new FastBufferWriter(32,Allocator.Temp))
        {
            writer.WriteValueSafe(key.Section);writer.WriteValueSafe(key.Seed);writer.WriteValueSafe(key.Index);
            writer.WriteValueSafe(state.Health);writer.WriteValueSafe(state.Destroyed);
            manager.CustomMessagingManager.SendNamedMessageToAll(Message,writer,NetworkDelivery.ReliableSequenced);
        }
    }
    private void Receive(ulong sender,FastBufferReader reader)
    {
        if(manager==null||manager.IsServer||sender!=NetworkManager.ServerClientId)return;
        reader.ReadValueSafe(out ulong section);reader.ReadValueSafe(out int seed);reader.ReadValueSafe(out int index);
        reader.ReadValueSafe(out float health);reader.ReadValueSafe(out bool destroyed);
        if(index<0||index>16||!ArenaProtocol.IsFinite(health))return;
        var key=new RunnerObstacleIdentity(section,seed,index);var state=new State(Mathf.Clamp01(health),destroyed);
        Remember(key,state);
        if(instances.TryGetValue(key,out var obstacle)&&obstacle!=null&&states.TryGetValue(key,out var applied))
            obstacle.ApplyState(applied.Health,applied.Destroyed);
    }
    private void Remember(RunnerObstacleIdentity key,State state)
    {
        if(states.TryGetValue(key,out var previous))
        {
            // A duplicate/late durability packet can never resurrect cover.
            if(previous.Destroyed||previous.Health<state.Health)return;
        }
        else stateOrder.Enqueue(key);
        states[key]=state;
        // Keep enough history for several entire multiplayer roads, then bound
        // memory even if the run is replayed for hours in the same session.
        while(stateOrder.Count>2048)states.Remove(stateOrder.Dequeue());
    }
    private void OnDestroy()
    {
        Detach();if(Instance==this)Instance=null;
    }
}
