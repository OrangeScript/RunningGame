using System;
using UnityEngine;

public enum RunnerHazardCycle : byte { Safe, Warning, Active }

/// <summary>Seed-owned obstacle parameters, shared by geometry, collision and replay.</summary>
public readonly struct RunnerObstaclePlan : IEquatable<RunnerObstaclePlan>
{
    public readonly ProceduralObstacleKind Kind;
    public readonly Vector3 Position, Size;
    public readonly float Phase, Amplitude, AngularSpeed, Health, Period, WarningSeconds, ActiveSeconds;
    public readonly int Damage;

    public RunnerObstaclePlan(ProceduralObstacleKind kind,Vector3 position,Vector3 size,float phase,
        float amplitude,float angularSpeed,float health,int damage,float period=3.8f,float warning=.85f,float active=.9f)
    {
        Kind=kind;Position=position;Size=size;Phase=phase;Amplitude=amplitude;AngularSpeed=angularSpeed;
        Health=health;Damage=damage;Period=period;WarningSeconds=warning;ActiveSeconds=active;
    }

    public RunnerHazardCycle CycleAt(double time)
    {
        if(Kind!=ProceduralObstacleKind.PulseLaser&&Kind!=ProceduralObstacleKind.SpikePad)return RunnerHazardCycle.Active;
        double cycle=(time+Phase)%Period;if(cycle<0)cycle+=Period;
        return cycle<WarningSeconds?RunnerHazardCycle.Warning:
            cycle<WarningSeconds+ActiveSeconds?RunnerHazardCycle.Active:RunnerHazardCycle.Safe;
    }

    public Vector3 PositionAt(double time)
    {
        if(Kind!=ProceduralObstacleKind.MovingBlock)return Position;
        return Position+Vector3.right*(Mathf.Sin((float)time*1.15f+Phase)*Amplitude);
    }

    /// <summary>Includes every moving/rotating extreme, not just the authored pose.</summary>
    public Vector2 SweptXBounds
    {
        get
        {
            float extent=Size.x*.5f;
            if(Kind==ProceduralObstacleKind.Spinner)extent=Mathf.Sqrt(Size.x*Size.x+Size.z*Size.z)*.5f;
            if(Kind==ProceduralObstacleKind.MovingBlock)extent+=Amplitude;
            return new Vector2(Position.x-extent,Position.x+extent);
        }
    }

    public bool Equals(RunnerObstaclePlan other)=>Kind==other.Kind&&Position.Equals(other.Position)&&Size.Equals(other.Size)&&
        Phase.Equals(other.Phase)&&Amplitude.Equals(other.Amplitude)&&AngularSpeed.Equals(other.AngularSpeed)&&
        Health.Equals(other.Health)&&Damage==other.Damage&&Period.Equals(other.Period)&&
        WarningSeconds.Equals(other.WarningSeconds)&&ActiveSeconds.Equals(other.ActiveSeconds);
    public override bool Equals(object obj)=>obj is RunnerObstaclePlan other&&Equals(other);
    public override int GetHashCode()=>unchecked(((int)Kind*397)^Position.GetHashCode()^Size.GetHashCode());
}

/// <summary>One continuous escape shoulder is reserved per section. Rows are
/// separated in Z and gate sections finish hazards well before the gate plane.</summary>
public static class RunnerObstaclePlanner
{
    public const float EscapeInnerEdge=3.75f;
    public const float RowSeparation=3.7f;
    public static int EscapeSide(int seed)=>(seed&1)==0?-1:1;

    public static RunnerObstaclePlan[] Generate(int seed,float difficulty,bool gateSection,int minimum=2,int maximum=4)
    {
        var random=new ProceduralRandom(seed);
        int cap=gateSection?2:4;
        int min=Mathf.Clamp(minimum,0,cap),max=Mathf.Clamp(maximum,min,cap);
        int bonus=Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0,difficulty-1)*.5f),0,1);
        int count=random.Range(min,Mathf.Min(cap,max+bonus)+1);
        var result=new RunnerObstaclePlan[count];
        int occupiedSide=-EscapeSide(seed);
        for(int i=0;i<count;i++)
        {
            int pick=random.Range(0,10);
            var kind=pick<2?ProceduralObstacleKind.Crate:pick<4?ProceduralObstacleKind.ExplosiveBarrel:
                pick==4?ProceduralObstacleKind.Block:pick==5?ProceduralObstacleKind.MovingBlock:
                pick==6?ProceduralObstacleKind.Spinner:pick<9?ProceduralObstacleKind.PulseLaser:ProceduralObstacleKind.SpikePad;
            // The opening row teaches the firing lane before adding timed hazards.
            if(i==0&&(kind==ProceduralObstacleKind.PulseLaser||kind==ProceduralObstacleKind.SpikePad))kind=ProceduralObstacleKind.Crate;
            float z=4.2f+i*RowSeparation+random.Range(0,.45f);
            float x=occupiedSide*random.Range(.2f,4.7f);
            Vector3 size;
            float amplitude=0,health=0;
            switch(kind)
            {
                case ProceduralObstacleKind.MovingBlock:
                    size=new Vector3(1.5f,1.7f,1);amplitude=1.1f;
                    x=occupiedSide*random.Range(.6f,4.1f);break;
                case ProceduralObstacleKind.Spinner:
                    size=new Vector3(4.3f,.35f,.4f);x=occupiedSide*1.4f;break;
                case ProceduralObstacleKind.ExplosiveBarrel:
                    size=new Vector3(1.1f,1.6f,1.1f);health=1.8f;break;
                case ProceduralObstacleKind.Crate:
                    size=new Vector3(random.Range(1.6f,2.2f),1.6f,1.2f);health=3.2f;break;
                case ProceduralObstacleKind.PulseLaser:
                    size=new Vector3(2.6f,1.5f,.65f);break;
                case ProceduralObstacleKind.SpikePad:
                    size=new Vector3(2.3f,1.2f,1.7f);break;
                default:
                    size=new Vector3(random.Range(1.7f,2.5f),1.8f,1.1f);health=5;break;
            }
            float y=kind==ProceduralObstacleKind.Spinner?.85f:size.y*.5f;
            int damage=Mathf.Clamp(Mathf.RoundToInt(6+difficulty*1.5f),7,13);
            result[i]=new RunnerObstaclePlan(kind,new Vector3(x,y,z),size,random.Range(0,6.2f),amplitude,
                random.Range(45,80)*(random.Chance(.5f)?1:-1),health,damage,random.Range(3.4f,4.5f),.85f,.9f);
        }
        return result;
    }
}
