using UnityEngine;

public class ExplosionVFX : MonoBehaviour
{
    [SerializeField] private float duration = 0.2f;
    private float elapsedTime;
    private float targetRadius;
    private LineRenderer ring;
    private static Material material;

    public void Initialize(float radius)
    {
        targetRadius=Mathf.Clamp(radius,.2f,8);elapsedTime=0;
        // VFX must not hide the next firing lane or stop a CharacterController.
        foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
        foreach(var renderer in GetComponentsInChildren<Renderer>())renderer.enabled=false;
        transform.localScale=Vector3.one;
        transform.position=new Vector3(transform.position.x,.12f,transform.position.z);
        var visual=new GameObject("BlastRing");visual.transform.SetParent(transform,false);
        ring=visual.AddComponent<LineRenderer>();ring.useWorldSpace=false;ring.loop=true;ring.positionCount=36;
        if(material==null)material=new Material(Shader.Find("Sprites/Default"));
        ring.sharedMaterial=material;ring.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows=false;
        for(int i=0;i<36;i++)
        {float angle=i*Mathf.PI*2/36;ring.SetPosition(i,new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)));}
    }


    void Update()
    {
        elapsedTime += Time.deltaTime;
        float t = elapsedTime / Mathf.Max(.1f,duration);
        t = Mathf.Clamp01(t);

        if(ring!=null)
        {
            float radius=Mathf.Lerp(.15f,targetRadius,t);
            ring.transform.localScale=new Vector3(radius,1,radius);
            ring.startWidth=ring.endWidth=Mathf.Lerp(.13f,.035f,t);
            ring.startColor=ring.endColor=new Color(.83f,.57f,.25f,1-t);
        }
        if(elapsedTime >= duration)
        {
            Destroy(gameObject);
        }
    }
}
