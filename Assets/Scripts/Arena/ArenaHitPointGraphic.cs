using UnityEngine;
using UnityEngine.UI;

/// <summary>Screen-space focus indicator, aligned with the bullet collision
/// plane instead of the top of a tilted 3D capsule.</summary>
public sealed class ArenaHitPointGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        const int segments=24;
        float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;
        mesh.AddVert(Vector3.zero,MinimalUI.Surface,Vector2.zero);
        for(int i=0;i<=segments;i++)
        {
            float angle=i*Mathf.PI*2/segments;
            mesh.AddVert(new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*radius,MinimalUI.Ink,Vector2.zero);
            if(i>0)mesh.AddTriangle(0,i,i+1);
        }
    }
}
