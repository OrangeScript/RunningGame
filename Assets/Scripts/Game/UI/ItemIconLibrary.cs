using System.Collections.Generic;
using UnityEngine;

/// <summary>Offline Iconify/Lucide sprites. No network requests during play.</summary>
public static class ItemIconLibrary
{
    private static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
    public static Sprite Get(string key)
    {
        if(string.IsNullOrWhiteSpace(key))return null;
        if(sprites.TryGetValue(key,out var cached)&&cached!=null)return cached;
        var sprite=Resources.Load<Sprite>("ItemIcons/"+key);
        if(sprite==null)return null;
        sprites[key]=sprite;return sprite;
    }
    public static string LegacyKey(int id)=>id switch
    {
        1001=>"crosshair",1002=>"snowflake",1003=>"weight",1004=>"repeat-2",1005=>"bomb",
        1006=>"move-up-right",1007=>"cloud-rain",1008=>"cloud-lightning",1009=>"mountain",
        1101=>"gauge",1102=>"split",1103=>"target",1201=>"orbit",1202=>"eclipse",_=>"sparkles"
    };
}
