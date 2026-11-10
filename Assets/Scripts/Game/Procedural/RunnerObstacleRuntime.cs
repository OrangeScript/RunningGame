using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>One physical obstacle. Every collider is a trigger: forward motion
/// is never held hostage by a wall, while real projectile sweeps can hit cover.</summary>
public sealed class RunnerObstacleRuntime : MonoBehaviour,IRunnerProjectileObstacle
{
    private static readonly HashSet<RunnerObstacleRuntime> active=new HashSet<RunnerObstacleRuntime>();
    private readonly HashSet<ulong> hitRunners=new HashSet<ulong>();
    private RunnerObstaclePlan plan;
    private Collider shape;
    private Renderer[] renderers;
    private Color[] originalColors;
    private MaterialPropertyBlock properties;
    private float health,flashUntil,destroyedAt;
    private bool authority,destroyed;
    private int lane;
    private Transform spikes;
    private RunnerHazardCycle lastCycle=(RunnerHazardCycle)byte.MaxValue;
    private bool paintedFlash;
    private static Mesh pyramid;
    public int LaneId=>lane;
    public static IReadOnlyCollection<RunnerObstacleRuntime> ActiveObstacles=>active;
    public RunnerObstaclePlan Plan=>plan;
    public ProceduralObstacleKind Kind=>plan.Kind;
    public bool IsDestroyed=>destroyed;
    public bool BlocksProjectiles=>!destroyed&&plan.Kind!=ProceduralObstacleKind.PulseLaser&&plan.Kind!=ProceduralObstacleKind.SpikePad;
    public float HealthFraction=>plan.Health>0?Mathf.Clamp01(health/plan.Health):1;
    public RunnerObstacleIdentity Identity { get; private set; }
    private static double Clock=>NetworkManager.Singleton!=null&&NetworkManager.Singleton.IsListening?
        NetworkManager.Singleton.ServerTime.Time:Time.timeAsDouble;

    public static RunnerObstacleRuntime Create(Transform parent,RunnerObstaclePlan plan,int laneId,bool authority,
        ulong sectionId,int seed,int index)
    {
        var root=new GameObject($"Obstacle_{index:00}_{plan.Kind}");root.transform.SetParent(parent,false);
        root.transform.localPosition=plan.Position;
        var runtime=root.AddComponent<RunnerObstacleRuntime>();
        runtime.plan=plan;runtime.lane=laneId;runtime.authority=authority;runtime.health=plan.Health;
        runtime.Identity=new RunnerObstacleIdentity(sectionId,seed,index);
        var collider=root.AddComponent<BoxCollider>();collider.isTrigger=true;collider.size=plan.Size;
        runtime.shape=collider;
        runtime.BuildGeometry();
        runtime.renderers=root.GetComponentsInChildren<Renderer>();runtime.originalColors=new Color[runtime.renderers.Length];
        runtime.properties=new MaterialPropertyBlock();
        for(int i=0;i<runtime.renderers.Length;i++)
        {
            runtime.renderers[i].GetPropertyBlock(runtime.properties);
            runtime.originalColors[i]=runtime.properties.GetColor("_BaseColor");
        }
        active.Add(runtime);
        if(Application.isPlaying)RunnerObstacleReplication.Ensure()?.Register(runtime);
        if(!runtime.destroyed)runtime.RefreshHazardVisuals(true);
        return runtime;
    }

    private void BuildGeometry()
    {
        Color wood=new Color(.57f,.48f,.34f),stone=new Color(.48f,.54f,.50f),orange=new Color(.72f,.40f,.27f);
        Vector3 size=plan.Size;
        switch(plan.Kind)
        {
            case ProceduralObstacleKind.ExplosiveBarrel:
                Piece("PressureTank",PrimitiveType.Cylinder,Vector3.zero,new Vector3(size.x,size.y*.5f,size.z),orange);
                for(int i=-1;i<=1;i+=2)Piece("Band",PrimitiveType.Cylinder,new Vector3(0,i*size.y*.3f,0),
                    new Vector3(size.x*1.035f,.045f,size.z*1.035f),new Color(.31f,.35f,.31f));
                Piece("Valve",PrimitiveType.Cube,new Vector3(0,size.y*.55f,0),new Vector3(.35f,.12f,.12f),stone);
                break;
            case ProceduralObstacleKind.Crate:
                Piece("Crate",PrimitiveType.Cube,Vector3.zero,size,wood);
                for(int i=-1;i<=1;i+=2)
                    Piece("Rib",PrimitiveType.Cube,new Vector3(i*size.x*.34f,0,-size.z*.51f),
                        new Vector3(.12f,size.y*.95f,.06f),new Color(.73f,.67f,.52f));
                Piece("Brace",PrimitiveType.Cube,new Vector3(0,0,-size.z*.52f),new Vector3(size.x*.9f,.12f,.06f),new Color(.73f,.67f,.52f));
                break;
            case ProceduralObstacleKind.PulseLaser:
                for(int i=-1;i<=1;i+=2)
                    Piece("Emitter",PrimitiveType.Cylinder,new Vector3(i*size.x*.5f,-.1f,0),new Vector3(.20f,size.y*.5f,.20f),stone);
                Piece("Beam",PrimitiveType.Cube,Vector3.zero,new Vector3(size.x,.14f,.14f),orange);
                Piece("WarningTrack",PrimitiveType.Cube,new Vector3(0,-plan.Position.y+.025f,0),new Vector3(size.x,.035f,.40f),orange);
                break;
            case ProceduralObstacleKind.SpikePad:
                Piece("PressurePlate",PrimitiveType.Cube,new Vector3(0,-plan.Position.y+.08f,0),new Vector3(size.x,.14f,size.z),stone);
                spikes=new GameObject("Spikes").transform;spikes.SetParent(transform,false);
                spikes.localPosition=new Vector3(0,-plan.Position.y+.15f,0);
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z+=2)
                    Spike(new Vector3(x*.65f,0,z*.43f),orange);
                break;
            case ProceduralObstacleKind.Spinner:
                Piece("Sweeper",PrimitiveType.Cube,Vector3.zero,size,orange);
                Piece("Hub",PrimitiveType.Cylinder,new Vector3(0,-.2f,0),new Vector3(.4f,.7f,.4f),stone);
                for(int i=-1;i<=1;i+=2)
                    Piece("ArmEnd",PrimitiveType.Cube,new Vector3(i*(size.x*.5f-.13f),0,0),new Vector3(.22f,size.y+.1f,.5f),new Color(.82f,.67f,.43f));
                break;
            case ProceduralObstacleKind.MovingBlock:
                Piece("SlidingCover",PrimitiveType.Cube,Vector3.zero,size,new Color(.40f,.57f,.53f));
                Piece("Guide",PrimitiveType.Cube,new Vector3(0,-plan.Position.y+.025f,0),
                    new Vector3(size.x+plan.Amplitude*2,.025f,.10f),new Color(.77f,.81f,.73f));
                break;
            default:
                Piece("ReinforcedCover",PrimitiveType.Cube,Vector3.zero,size,stone);
                Piece("Cap",PrimitiveType.Cube,new Vector3(0,size.y*.43f,0),new Vector3(size.x+.1f,.22f,size.z+.1f),new Color(.66f,.68f,.59f));
                break;
        }
    }

    private void Piece(string name,PrimitiveType primitive,Vector3 position,Vector3 scale,Color color)
    {
        var part=GameObject.CreatePrimitive(primitive);part.name=name;part.transform.SetParent(transform,false);
        part.transform.localPosition=position;part.transform.localScale=scale;part.GetComponent<Collider>().enabled=false;
        RunSectionPresentation.Paint(part.GetComponent<Renderer>(),color);
    }
    private void Spike(Vector3 position,Color color)
    {
        if(pyramid==null)
        {
            pyramid=new Mesh{name="Runner spike"};
            pyramid.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(.5f,0,.5f),new Vector3(-.5f,0,.5f),Vector3.up};
            pyramid.triangles=new[]{0,4,1,1,4,2,2,4,3,3,4,0,0,1,2,0,2,3};pyramid.RecalculateNormals();
        }
        var part=new GameObject("Spike",typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(spikes,false);
        part.transform.localPosition=position;part.transform.localScale=new Vector3(.43f,.85f,.43f);
        part.GetComponent<MeshFilter>().sharedMesh=pyramid;
        var renderer=part.GetComponent<MeshRenderer>();
        // Reuse the primitive material, never allocate a new material per spike.
        renderer.sharedMaterial=transform.Find("PressurePlate").GetComponent<Renderer>().sharedMaterial;
        RunSectionPresentation.Paint(renderer,color);
    }

    private void Update()
    {
        if(destroyed)
        {
            float amount=Mathf.Clamp01((Time.unscaledTime-destroyedAt)/.32f);
            transform.localScale=Vector3.one*(1-amount*.8f);
            if(amount>=1)gameObject.SetActive(false);
            return;
        }
        double time=Clock;
        transform.localPosition=plan.PositionAt(time);
        if(plan.Kind==ProceduralObstacleKind.MovingBlock)
        {
            Transform guide=transform.Find("Guide");
            if(guide!=null){var position=guide.localPosition;position.x=plan.Position.x-transform.localPosition.x;guide.localPosition=position;}
        }
        if(plan.Kind==ProceduralObstacleKind.Spinner)transform.localRotation=Quaternion.Euler(0,(float)time*plan.AngularSpeed+plan.Phase*60,0);
        RefreshHazardVisuals(false);
    }

    private void RefreshHazardVisuals(bool force)
    {
        RunnerHazardCycle cycle=plan.CycleAt(Clock);
        bool flash=Time.unscaledTime<flashUntil;
        if(!force&&cycle==lastCycle&&flash==paintedFlash)return;
        lastCycle=cycle;paintedFlash=flash;
        bool timed=plan.Kind==ProceduralObstacleKind.PulseLaser||plan.Kind==ProceduralObstacleKind.SpikePad;
        for(int i=0;i<renderers.Length;i++)
        {
            Color color=originalColors[i]*Mathf.Lerp(.58f,1,HealthFraction);color.a=1;
            if(timed)color=cycle==RunnerHazardCycle.Warning?new Color(.83f,.65f,.29f):
                cycle==RunnerHazardCycle.Active?new Color(.85f,.32f,.24f):new Color(.39f,.46f,.42f);
            if(flash)color=Color.Lerp(color,Color.white,.7f);
            properties.Clear();properties.SetColor("_BaseColor",color);properties.SetColor("_Color",color);
            renderers[i].SetPropertyBlock(properties);
        }
        if(spikes!=null)spikes.localScale=new Vector3(1,cycle==RunnerHazardCycle.Active?1:.12f,1);
        Transform beam=transform.Find("Beam");if(beam!=null)
            beam.localScale=new Vector3(plan.Size.x,cycle==RunnerHazardCycle.Active?.18f:.045f,cycle==RunnerHazardCycle.Active?.18f:.045f);
    }

    private void OnTriggerEnter(Collider other)=>TryHurtRunner(other);
    private void OnTriggerStay(Collider other)=>TryHurtRunner(other);
    private void TryHurtRunner(Collider other)
    {
        if(!authority||destroyed||NetworkManager.Singleton==null||!NetworkManager.Singleton.IsServer||plan.CycleAt(Clock)!=RunnerHazardCycle.Active)return;
        var runner=other.GetComponentInParent<RunnerController>();
        if(runner==null||runner.LaneId!=lane||(runner.Phase!=PlayerRunPhase.Running&&runner.Phase!=PlayerRunPhase.Combat))return;
        var crowd=runner.GetComponent<CrowdManager>();
        if(crowd!=null&&crowd.Health>0&&hitRunners.Add(runner.OwnerClientId))crowd.DamageHealth(plan.Damage);
    }

    public void HitByProjectile(float damage,ElementType element)
    {
        if(!authority||destroyed||plan.Health<=0||damage<=0||float.IsNaN(damage)||float.IsInfinity(damage)||
            NetworkManager.Singleton==null||!NetworkManager.Singleton.IsServer)return;
        health=Mathf.Max(0,health-damage);
        ApplyState(HealthFraction,health<=0);
        RunnerObstacleReplication.Ensure()?.Publish(this);
        if(destroyed&&plan.Kind==ProceduralObstacleKind.ExplosiveBarrel)Detonate();
    }

    public void ApplyState(float healthFraction,bool broken)
    {
        if(destroyed)return;
        health=plan.Health*Mathf.Clamp01(healthFraction);flashUntil=Time.unscaledTime+.14f;
        RefreshHazardVisuals(true);
        if(!broken)return;
        destroyed=true;destroyedAt=Time.unscaledTime;shape.enabled=false;active.Remove(this);
        foreach(var renderer in renderers)RunSectionPresentation.Paint(renderer,new Color(.91f,.84f,.64f));
        if(Application.isPlaying&&plan.Kind==ProceduralObstacleKind.ExplosiveBarrel)RunnerObstacleBlastVisual.Spawn(transform.position,4.5f);
    }

    private void Detonate()
    {
        const float radius=4.5f;
        foreach(var enemy in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if(enemy.IsDead||enemy.LaneId!=lane||(enemy.transform.position-transform.position).sqrMagnitude>radius*radius)continue;
            var reaction=enemy.ApplyElement(ElementType.Fire,1,3,9);
            enemy.TakeDamage(9*Mathf.Clamp(reaction.damageMultiplier,1,3),ElementType.Fire);
        }
        // Snapshot because a chain reaction removes members immediately.
        foreach(var obstacle in new List<RunnerObstacleRuntime>(active))
            if(obstacle!=null&&obstacle.lane==lane&&(obstacle.transform.position-transform.position).sqrMagnitude<=radius*radius)
                obstacle.HitByProjectile(7,ElementType.Fire);
    }
    private void OnDisable()
    {
        active.Remove(this);
        if(Application.isPlaying)RunnerObstacleReplication.Instance?.Unregister(this);
    }
    private void OnDestroy()=>active.Remove(this);
}

public static class RunnerObstacleDamageUtility
{
    private static readonly List<MonoBehaviour> candidates=new List<MonoBehaviour>(8);
    public static IRunnerProjectileObstacle Find(Collider collider)
    {
        if(collider==null)return null;
        var roadObstacle=collider.GetComponentInParent<RunnerObstacleRuntime>();
        if(roadObstacle!=null)return roadObstacle;
        // Custom projectile obstacles can opt in without a per-cast array.
        candidates.Clear();collider.GetComponentsInParent(true,candidates);
        foreach(var component in candidates)
            if(component is IRunnerProjectileObstacle obstacle)return obstacle;
        return null;
    }
}

/// <summary>Non-damaging client/server presentation of a barrel blast.</summary>
public sealed class RunnerObstacleBlastVisual : MonoBehaviour
{
    private float started,radius;
    private LineRenderer ring;
    private static Material material;
    public static void Spawn(Vector3 position,float radius)
    {
        var root=new GameObject("BarrelBlast");root.transform.position=new Vector3(position.x,.12f,position.z);
        var effect=root.AddComponent<RunnerObstacleBlastVisual>();effect.radius=radius;effect.started=Time.unscaledTime;
        effect.ring=root.AddComponent<LineRenderer>();effect.ring.useWorldSpace=false;effect.ring.loop=true;effect.ring.positionCount=40;
        if(material==null)material=new Material(Shader.Find("Sprites/Default"));
        effect.ring.sharedMaterial=material;effect.ring.startWidth=.16f;effect.ring.endWidth=.16f;
        effect.ring.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    private void Update()
    {
        float t=(Time.unscaledTime-started)/.4f;if(t>=1){Destroy(gameObject);return;}
        float distance=Mathf.Lerp(.2f,radius,t);
        for(int i=0;i<ring.positionCount;i++)
        {float angle=i*Mathf.PI*2/ring.positionCount;ring.SetPosition(i,new Vector3(Mathf.Cos(angle)*distance,0,Mathf.Sin(angle)*distance));}
        ring.startColor=ring.endColor=new Color(.86f,.57f,.26f,1-t);
    }
}
