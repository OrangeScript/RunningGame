using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>Bullets are synchronized by spawn time, with small XZ hitboxes,
/// visible arming time, one graze per player and an authoritative clear event.</summary>
public sealed class ArenaBossProjectile : MonoBehaviour
{
    private static readonly HashSet<ArenaBossProjectile> live=new HashSet<ArenaBossProjectile>();
    private readonly HashSet<ulong> grazed=new HashSet<ulong>();
    private Vector3 origin,direction;
    private float speed,damage,radius,lifetime,warning;
    private double born;
    private bool authoritative,cleared,armed;
    private Renderer visual;
    private Color tint;
    private LineRenderer warningLine;
    private static Material warningMaterial;
    public static int LiveCount=>live.Count;
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
        live.Add(bullet);root.transform.position=origin;
        return bullet;
    }
    private static double Now=>NetworkManager.Singleton!=null&&NetworkManager.Singleton.IsListening?
        NetworkManager.Singleton.ServerTime.Time:Time.timeAsDouble;
    private void Update()
    {
        if(cleared)return;
        float age=(float)(Now-born);
        if(age>lifetime+warning) { Destroy(gameObject); return; }
        float travel=Mathf.Max(0,age-warning)*speed;
        Vector3 previous=transform.position;
        Vector3 next=origin+direction*travel;
        transform.position=next;
        transform.localScale=Vector3.one*radius*2*(age<warning?.65f:1);
        if(age>=warning&&!armed)
        {
            armed=true;RunSectionPresentation.Paint(visual,tint);
            if(warningLine!=null)warningLine.enabled=false;
        }
        if(Mathf.Abs(next.x)>21||Mathf.Abs(next.z)>15) { Destroy(gameObject); return; }
        if(!authoritative||age<warning)return;
        foreach(var player in ArenaPlayerNetwork.ActivePlayers)
        {
            if(player==null||!player.IsArenaAlive||!player.IsParticipatingPawn)continue;
            float distance=SegmentDistance(previous,next,player.transform.position);
            if(distance<radius+player.HitRadius)
            {
                player.ApplyArenaDamage(new ArenaDamageInfo(damage,2,0,next,direction));
                cleared=true;Destroy(gameObject);return;
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
    public static void Clear(Vector3 center,float radius)
    {
        foreach(var bullet in new List<ArenaBossProjectile>(live))
            if(bullet!=null&&Vector2.Distance(new Vector2(center.x,center.z),new Vector2(bullet.transform.position.x,bullet.transform.position.z))<=radius)
            { bullet.cleared=true;Object.Destroy(bullet.gameObject); }
    }
    private void OnDestroy()=>live.Remove(this);
}
