using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>Shared time-based trajectories. Hits never consume a bullet;
/// clears disable damage immediately and visibly dissolve it.</summary>
public sealed class ArenaBossProjectile : MonoBehaviour
{
    public const float DissolveDuration=.28f;
    private static readonly HashSet<ArenaBossProjectile> live=new HashSet<ArenaBossProjectile>();
    private readonly HashSet<ulong> grazed=new HashSet<ulong>();
    private readonly HashSet<ulong> hitPlayers=new HashSet<ulong>();
    private Vector3 origin,direction;
    private float speed,damage,radius,lifetime,warning;
    private float dissolveStarted;
    private double born;
    private bool authoritative,cleared,armed;
    private Renderer visual;
    private Color tint;
    private LineRenderer warningLine;
    private static Material warningMaterial;
    public static int LiveCount=>live.Count;
    public bool IsCleared=>cleared;
    public bool IsAuthoritative=>authoritative;
    public double BornTime=>born;
    public static ArenaBossProjectile Spawn(Vector3 origin,Vector3 direction,float speed,float lifetime,
        float damage,float radius,bool authoritative,float warning=.35f,byte style=0,double born=-1)
    {
        var root=GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name=authoritative?"BossBullet_Server":"BossBullet_Visual";
        root.GetComponent<Collider>().enabled=false;
        var bullet=root.AddComponent<ArenaBossProjectile>();
        direction.y=0;bullet.origin=origin;bullet.direction=direction.normalized;
        bullet.speed=speed;bullet.lifetime=lifetime;bullet.damage=damage;bullet.radius=radius;
        bullet.warning=warning;bullet.authoritative=authoritative;
        bullet.born=born<0?Now:born;
        bullet.tint=style==0?new Color(.86f,.3f,.28f):style==1?new Color(.25f,.58f,.86f):new Color(.75f,.48f,.83f);
        bullet.visual=root.GetComponent<Renderer>();
        RunSectionPresentation.Paint(bullet.visual,Color.Lerp(bullet.tint,Color.white,.5f));
        if(style==1)
        {
            if(warningMaterial==null)warningMaterial=new Material(Shader.Find("Sprites/Default"));
            bullet.warningLine=root.AddComponent<LineRenderer>();
            bullet.warningLine.sharedMaterial=warningMaterial;bullet.warningLine.positionCount=2;
            bullet.warningLine.SetPosition(0,origin);bullet.warningLine.SetPosition(1,origin+direction*9);
            bullet.warningLine.startWidth=.035f;bullet.warningLine.endWidth=.015f;
            bullet.warningLine.startColor=new Color(bullet.tint.r,bullet.tint.g,bullet.tint.b,.5f);
            bullet.warningLine.endColor=new Color(bullet.tint.r,bullet.tint.g,bullet.tint.b,.15f);
            bullet.warningLine.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            bullet.warningLine.receiveShadows=false;
        }
        live.Add(bullet);root.transform.position=bullet.PositionAt(Now);
        root.transform.localScale=Vector3.one*radius*2*.65f;
        return bullet;
    }
    private static double Now=>NetworkManager.Singleton!=null&&NetworkManager.Singleton.IsListening?
        NetworkManager.Singleton.ServerTime.Time:Time.timeAsDouble;
    public Vector3 PositionAt(double time)=>EvaluatePosition(origin,direction,speed,warning,born,time);
    public static Vector3 EvaluatePosition(Vector3 origin,Vector3 direction,float speed,float warning,double born,double time)=>
        origin+direction*Mathf.Max(0,(float)(time-born)-warning)*speed;
    private void Update()
    {
        if(cleared)
        {
            float progress=(Time.unscaledTime-dissolveStarted)/DissolveDuration;
            if(progress>=1) { Destroy(gameObject);return; }
            transform.localScale=Vector3.one*radius*2*(1-progress);
            RunSectionPresentation.Paint(visual,Color.Lerp(tint,Color.white,.65f+progress*.35f));
            return;
        }
        float age=(float)(Now-born);
        Vector3 previous=transform.position;
        Vector3 next=PositionAt(Now);
        transform.position=next;
        transform.localScale=Vector3.one*radius*2*(age<warning?.65f:1);
        if(age>=warning&&!armed)
        {
            armed=true;RunSectionPresentation.Paint(visual,tint);
            if(warningLine!=null)warningLine.enabled=false;
        }
        if(age>lifetime+warning||Mathf.Abs(next.x)>21||Mathf.Abs(next.z)>15) { BeginDissolve(); return; }
        if(!authoritative||age<warning)return;
        foreach(var player in ArenaPlayerNetwork.ActivePlayers)
        {
            if(player==null||!player.IsArenaAlive||!player.IsParticipatingPawn)continue;
            float distance=SegmentDistance(previous,next,player.transform.position);
            if(distance<radius+player.HitRadius)
            {
                if(hitPlayers.Add(player.OwnerClientId))
                {
                    // Passing through the same bullet after a hit is not a
                    // second, successful graze.
                    grazed.Add(player.OwnerClientId);
                    player.ApplyArenaDamage(new ArenaDamageInfo(damage,2,0,next,direction));
                }
                continue;
            }
            if(distance<radius+.72f&&grazed.Add(player.OwnerClientId))player.AddGrazeServer();
        }
    }
    public static float SegmentDistance(Vector3 from,Vector3 to,Vector3 point)
    {
        from.y=to.y=point.y=0;Vector3 line=to-from;
        float t=line.sqrMagnitude>.000001f?Mathf.Clamp01(Vector3.Dot(point-from,line)/line.sqrMagnitude):0;
        return Vector3.Distance(point,from+line*t);
    }
    public static void Clear(Vector3 center,float radius,double atTime=-1)
    {
        double time=atTime<0?Now:atTime;
        foreach(var bullet in new List<ArenaBossProjectile>(live))
        {
            if(bullet==null)continue;
            // Compare the event's server-time position on every peer, even
            // before a received bullet's first Update.
            Vector3 position=bullet.PositionAt(time);position.y=center.y;
            if((position-center).sqrMagnitude<=radius*radius)bullet.BeginDissolve();
        }
    }
    private void BeginDissolve()
    {
        if(cleared)return;
        cleared=true;dissolveStarted=Time.unscaledTime;live.Remove(this);
        if(warningLine!=null)warningLine.enabled=false;
    }
    private void OnDestroy()=>live.Remove(this);
}
