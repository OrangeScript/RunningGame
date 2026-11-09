using UnityEngine;

/// <summary>Deterministic encounter rules, independent of rendering and networking.</summary>
public sealed class BossElementState
{
    public ElementType Aura { get; private set; }
    public float BreakGauge { get; private set; }
    public float ExposedUntil { get; private set; }
    public float SlowedUntil { get; private set; }
    public string LastReaction { get; private set; }="";
    public bool LastBrokeShield { get; private set; }
    private float auraUntil,nextReaction;
    private ulong auraSource;
    public float Apply(ElementType incoming,ulong source,float now)
    {
        LastBrokeShield=false;
        if(incoming==ElementType.None) return now<ExposedUntil?1.8f:.72f;
        if(now>auraUntil) Aura=ElementType.None;
        float multiplier=now<ExposedUntil?1.8f:.85f;
        if(Aura!=ElementType.None&&Aura!=incoming&&now>=nextReaction)
        {
            var reaction=ElementReactionSystem.Resolve(Aura,incoming);
            float gain=18;
            LastReaction=reaction.reactionType switch
            {
                ElementReactionType.Frozen=>"凝镜",ElementReactionType.Melt=>"融化",
                ElementReactionType.Vaporize=>"蒸发",ElementReactionType.ElectroCharged=>"感电",
                ElementReactionType.Overload=>"超载",ElementReactionType.SuperConduct=>"超导",_=>"元素交织"
            };
            if(reaction.reactionType==ElementReactionType.Frozen) { SlowedUntil=Mathf.Max(SlowedUntil,now+3.5f); gain=26; }
            if(reaction.reactionType==ElementReactionType.Overload) gain=35;
            if(reaction.reactionType==ElementReactionType.ElectroCharged) gain=28;
            if(incoming==ElementType.Wind||Aura==ElementType.Wind) LastReaction="扩散";
            if(incoming==ElementType.Earth||Aura==ElementType.Earth) { LastReaction="结晶"; gain=22; }
            if(incoming==ElementType.Nature||Aura==ElementType.Nature) { LastReaction="绽生"; gain=24; }
            multiplier*=Mathf.Clamp(reaction.damageMultiplier,1,2);
            if(now>=ExposedUntil)
            {
                BreakGauge=Mathf.Min(100,BreakGauge+gain*(source!=auraSource?1.3f:1));
                if(BreakGauge>=100)
                {
                    BreakGauge=0; ExposedUntil=now+5; LastBrokeShield=true; LastReaction="镜甲破碎";
                }
            }
            nextReaction=now+.45f;
        }
        Aura=incoming; auraSource=source; auraUntil=now+5;
        return multiplier;
    }
}
