using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Offline item art on a small, readable world-space card.</summary>
public sealed class RunItemPickupVisual : MonoBehaviour
{
    private Transform card;
    private Vector3 origin;
    private float phase;
    public static GameObject Create(Vector3 position,UpgradeData item,ItemRarity rarity,uint id)
    {
        var root=new GameObject("RunPickup_"+id);root.transform.position=position;
        var visual=root.AddComponent<RunItemPickupVisual>();visual.origin=position;visual.phase=(id%17)*.37f;
        var baseObject=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObject.name="PickupBase";baseObject.transform.SetParent(root.transform,false);
        baseObject.transform.localPosition=new Vector3(0,.08f,0);baseObject.transform.localScale=new Vector3(1.1f,.04f,1.1f);
        baseObject.GetComponent<Collider>().enabled=false;
        RunSectionPresentation.Paint(baseObject.GetComponent<Renderer>(),RunLootPolicy.Tint(rarity));
        var card=new GameObject("ItemCard",typeof(RectTransform),typeof(Canvas));card.transform.SetParent(root.transform,false);
        card.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        card.transform.localScale=Vector3.one*.012f;((RectTransform)card.transform).sizeDelta=new Vector2(120,120);
        visual.card=card.transform;
        var paper=MinimalUI.Box(card.transform,"Paper",new Color(.98f,.98f,.94f,.95f));
        var icon=MinimalUI.Box(paper,"Icon",RunLootPolicy.Tint(rarity));
        MinimalUI.Place(icon,new Vector2(.25f,.28f),new Vector2(.75f,.80f));
        icon.GetComponent<Image>().sprite=item!=null?item.Icon:ItemIconLibrary.Get("shield");
        icon.GetComponent<Image>().preserveAspect=true;
        var text=MinimalUI.Text(paper,"Name",item!=null?item.UpgradeName:"护盾碎片",13,MinimalUI.Ink,TextAlignmentOptions.Center);
        MinimalUI.Place(text.rectTransform,new Vector2(.02f,.03f),new Vector2(.98f,.25f));
        visual.Update();return root;
    }
    private void Update()
    {
        if(card==null)return;
        card.position=origin+Vector3.up*(1.25f+Mathf.Sin(Time.time*2+phase)*.10f);
        if(Camera.main!=null)card.rotation=Camera.main.transform.rotation;
    }
}
