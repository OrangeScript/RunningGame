using Unity.Netcode;
using UnityEngine;

public enum ProceduralObstacleKind : byte
{
    Block, MovingBlock, Spinner, Crate, ExplosiveBarrel, PulseLaser, SpikePad
}

/// <summary>Same plans and collision shapes on all peers. Server owns damage
/// and durability; no solid obstacle can permanently stop the forward runner.</summary>
public static class ProceduralObstacleField
{
    private const string ContainerName="__ProceduralObstacles";

    public static void Build(Transform sectionRoot,int seed,int laneId,float difficulty,bool serverPhysics,
        int minimumCount=2,int maximumCount=4)
    {
        if(sectionRoot==null)return;
        Clear(sectionRoot);
        var section=sectionRoot.GetComponent<LevelSection>();
        bool gate=section!=null&&section.Type==SectionType.Gate;
        var plans=RunnerObstaclePlanner.Generate(seed,difficulty,gate,minimumCount,maximumCount);
        var container=new GameObject(ContainerName).transform;container.SetParent(sectionRoot,false);
        var networkObject=sectionRoot.GetComponent<NetworkObject>();
        ulong sectionId=networkObject!=null&&networkObject.IsSpawned?networkObject.NetworkObjectId:0;
        RunnerObstacleReplication.Ensure();
        for(int i=0;i<plans.Length;i++)
            RunnerObstacleRuntime.Create(container,plans[i],laneId,serverPhysics,sectionId,seed,i);
    }

    public static void Clear(Transform sectionRoot)
    {
        if(sectionRoot==null)return;
        var existing=sectionRoot.Find(ContainerName);if(existing==null)return;
        // Disable immediately: deferred destruction must not leave duplicate
        // collision or two hazards registered during a field rebuild.
        existing.gameObject.SetActive(false);
        if(Application.isPlaying)Object.Destroy(existing.gameObject);else Object.DestroyImmediate(existing.gameObject);
    }
}
