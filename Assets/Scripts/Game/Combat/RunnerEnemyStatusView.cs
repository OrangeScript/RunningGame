using TMPro;
using UnityEngine;

/// <summary>Small health/name marker; a player can distinguish special gunners
/// before committing to their firing line. No interactive UI or raycaster.</summary>
public sealed class RunnerEnemyStatusView : MonoBehaviour
{
    private Enemy enemy;
    private RectTransform card,fill;
    private TMP_Text label;
    private Camera cameraView;
    private void Awake()
    {
        enemy=GetComponent<Enemy>();
        var root=new GameObject("DefenderStatus",typeof(RectTransform),typeof(Canvas));root.transform.SetParent(transform,false);
        card=(RectTransform)root.transform;card.localPosition=Vector3.up*2.5f;card.localScale=Vector3.one*.012f;card.sizeDelta=new Vector2(140,44);
        root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        label=MinimalUI.Text(card,"Role","",18,MinimalUI.Ink,TextAlignmentOptions.Center);
        MinimalUI.Place(label.rectTransform,new Vector2(0,.32f),Vector2.one);
        var track=MinimalUI.Box(card,"HealthTrack",MinimalUI.Line);
        MinimalUI.Place(track,new Vector2(.12f,.05f),new Vector2(.88f,.17f));
        fill=MinimalUI.Box(track,"Health",new Color(.62f,.37f,.29f));
    }
    private void LateUpdate()
    {
        if(enemy==null||card==null)return;
        bool visible=enemy.IsSpawned&&!enemy.IsDead;
        foreach(var runner in RunnerController.ActiveRunners)
        {
            if(runner==null||!runner.IsOwner)continue;
            float distance=transform.position.z-runner.transform.position.z;
            visible&=runner.LaneId==enemy.LaneId&&distance>=-1&&distance<=34;break;
        }
        card.gameObject.SetActive(visible);if(!visible)return;
        if(cameraView==null)cameraView=Camera.main;
        if(cameraView!=null)card.rotation=cameraView.transform.rotation;
        label.text=enemy.ArchetypeLabel;
        fill.anchorMax=new Vector2(enemy.MaxHealth>0?Mathf.Clamp01(enemy.CurrentHealth/enemy.MaxHealth):0,1);
    }
}
