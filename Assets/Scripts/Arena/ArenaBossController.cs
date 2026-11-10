using UnityEngine;

public enum ArenaMatchResult : byte { None,Victory,Defeat }

/// <summary>One authored encounter: three readable spell cards with reaction-driven
/// armor breaks. All angles, locks, gaps and hit decisions are server-owned.</summary>
public sealed class ArenaBossController : MonoBehaviour,IArenaDamageable
{
    private ArenaBootstrap bridge;
    private float health,maxHealth,nextWave,nextBroadcast,transitionUntil;
    private int stage=1,wave;
    private bool hadPlayers;
    private ArenaMatchResult result;
    private ArenaBossMotion motion;
    public readonly BossElementState Elements=new BossElementState();
    public int ArenaTeamId=>2;
    public bool IsArenaAlive=>health>0&&result==ArenaMatchResult.None;
    public float Health=>health;
    public float MaxHealth=>maxHealth;
    public ArenaMatchResult Result=>result;
    public int Stage=>stage;
    public void PausePattern(float seconds)=>nextWave=Mathf.Max(nextWave,Time.time+Mathf.Max(0,seconds));
    public void Initialize(ArenaBootstrap owner,float baseHealth)
    {
        bridge=owner;
        motion=new ArenaBossMotion(transform.position);
        int count=Mathf.Max(1,Players().Length);
        maxHealth=Mathf.Max(220,baseHealth)*(1+.65f*(count-1)); health=maxHealth;
        nextWave=Time.time+2; Broadcast();
    }
    private void Update()
    {
        if(bridge==null||!bridge.IsServerAuthority||result!=ArenaMatchResult.None) return;
        var players=Players(); int alive=0;
        foreach(var player in players) if(player.IsArenaAlive)alive++;
        if(alive>0)hadPlayers=true; else if(hadPlayers) { Finish(ArenaMatchResult.Defeat); return; }
        if(!hadPlayers)return;
        int desired=health/maxHealth>.7f?1:health/maxHealth>.35f?2:3;
        if(desired>stage)
        {
            stage=desired; wave=0; transitionUntil=Time.time+1.4f;
            motion.SetStage(stage);motion.HoldUntil(transitionUntil);
            bridge.ClearBossBullets(reason:ArenaBulletClearReason.PhaseTransition); nextWave=transitionUntil; Broadcast();
        }
        transform.position=motion.Tick(Time.time,Time.deltaTime,Time.time<Elements.SlowedUntil);
        transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(Vector3.back),Time.deltaTime*5);
        Physics.SyncTransforms();
        bridge.BroadcastBossPose(transform.position,transform.forward,motion.Velocity);
        if(Time.time>=nextWave&&Time.time>=transitionUntil)
        {
            EmitWave(players);
            float interval=stage==1?.65f:stage==2?.72f:.54f;
            if(Time.time<Elements.SlowedUntil)interval*=1.65f;
            if(Time.time<Elements.ExposedUntil)interval*=1.6f;
            nextWave=Time.time+interval; wave++;
        }
        if(Time.time>=nextBroadcast) { nextBroadcast=Time.time+.2f; Broadcast(); }
    }
    public void ApplyArenaDamage(ArenaDamageInfo hit)
    {
        if(!IsArenaAlive||bridge==null||!bridge.IsServerAuthority||hit.SourceTeamId==2||
            !ArenaProtocol.IsFinite(hit.Amount)||hit.Amount<=0)return;
        float multiplier=Elements.Apply(hit.Element,hit.SourceClientId,Time.time);
        health=Mathf.Max(0,health-hit.Amount*multiplier);
        if(Elements.LastBrokeShield)
        {
            motion.HoldUntil(Elements.ExposedUntil);
            bridge.ClearBossBullets(reason:ArenaBulletClearReason.ArmorBreak); nextWave=Time.time+1.5f;
        }
        Broadcast(); if(health<=0)Finish(ArenaMatchResult.Victory);
    }
    private void EmitWave(ArenaPlayerNetwork[] players)
    {
        if(stage==1) PetalRing();
        else if(stage==2)
        {
            if(wave%3==0) AimedStreams(players); else Spiral();
        }
        else
        {
            if(wave%4==0) RainCurtain();
            else if(wave%4==2) AimedStreams(players);
            else PetalRing();
        }
    }
    private void PetalRing()
    {
        // Two opposed travelling gaps remain open in every ring. Slow inner
        // rings and faster outer rings are separated instead of random spray.
        float gap=180+Mathf.Sin(wave*.43f)*65;
        int count=stage==3?32:24;
        for(int i=0;i<count;i++)
        {
            float angle=i*360f/count+wave*7;
            if(Mathf.Abs(Mathf.DeltaAngle(angle,gap))<23||Mathf.Abs(Mathf.DeltaAngle(angle,gap+180))<23)continue;
            Emit(transform.position,angle,stage==3?5.1f:4.3f,.14f,.4f,0);
        }
    }
    private void AimedStreams(ArenaPlayerNetwork[] players)
    {
        motion.HoldUntil(Time.time+1.1f);
        foreach(var player in players)
        {
            if(!player.IsArenaAlive)continue;
            Vector3 difference=player.transform.position-transform.position;
            float aimed=Mathf.Atan2(difference.x,difference.z)*Mathf.Rad2Deg;
            // Target is locked at creation; the warning gives time to bait the
            // stream away from a teammate instead of continually homing.
            for(int lane=-1;lane<=1;lane++)for(int bead=0;bead<4;bead++)
                Emit(transform.position,aimed+lane*12,6.2f+bead*.35f,.13f,.75f+bead*.1f,1);
        }
    }
    private void Spiral()
    {
        for(int arm=0;arm<5;arm++)
            Emit(transform.position,arm*72+wave*16,4.6f,.13f,.35f,2);
    }
    private void RainCurtain()
    {
        float gap=Mathf.Sin(wave*.37f)*10;
        for(float x=-16;x<=16;x+=1.05f)
        {
            if(Mathf.Abs(x-gap)<2.6f)continue;
            Emit(new Vector3(x,.75f,11),180,5.3f,.13f,.9f,2);
        }
    }
    private void Emit(Vector3 source,float angle,float speed,float radius,float warning,byte style)
    {
        float rad=angle*Mathf.Deg2Rad;
        Vector3 direction=new Vector3(Mathf.Sin(rad),0,Mathf.Cos(rad));
        source.y=.75f;
        if(Mathf.Abs(source.z-11)>.1f)source+=direction*1.4f;
        bridge.SpawnBossBullet(source,direction,speed,8,1,radius,warning,style);
    }
    private static ArenaPlayerNetwork[] Players()
    {
        var all=Object.FindObjectsByType<ArenaPlayerNetwork>(FindObjectsSortMode.None);
        return System.Array.FindAll(all,p=>p.IsSpawned&&p.IsParticipatingPawn);
    }
    private void Broadcast()=>bridge?.BroadcastBossState(health,maxHealth,result,stage,
        Elements.BreakGauge,Mathf.Max(0,Elements.ExposedUntil-Time.time),Elements.Aura,Elements.LastReaction);
    private void Finish(ArenaMatchResult outcome)
    { result=outcome; bridge.ClearBossBullets(reason:ArenaBulletClearReason.MatchEnd); Broadcast(); bridge.NotifyMatchEnded(outcome); }
}
