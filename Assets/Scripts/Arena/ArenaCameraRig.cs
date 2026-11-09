using UnityEngine;

[DisallowMultipleComponent]
public class ArenaCameraRig : MonoBehaviour
{
    [SerializeField] private Camera outputCamera;
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 deadZoneHalfSize=new Vector2(4.5f,3f);
    [SerializeField] private float smoothTime=.22f;
    [SerializeField] private float viewSize=14f;
    private Vector3 center,velocity;
    public Camera OutputCamera=>outputCamera;
    public Transform Target=>target;
    public Vector3 FocusPoint=>center;
    private void Awake() { outputCamera=GetComponent<Camera>(); center=new Vector3(0,.75f,0); }
    public void Bind(Transform owner)
    {
        if(owner==null||target==owner) return;
        target=owner; center=new Vector3(0,.75f,0); velocity=Vector3.zero; UpdatePose(true);
    }
    public void Unbind(Transform expected=null) { if(expected==null||target==expected) target=null; }
    public bool TryBindLocalOwner()
    {
        foreach(var player in FindObjectsByType<ArenaPlayerNetwork>(FindObjectsSortMode.None))
            if(player.IsSpawned&&player.IsOwner) { Bind(player.transform); return true; }
        return false;
    }
    private void LateUpdate() { if(target==null&&!TryBindLocalOwner())return; UpdatePose(false); }
    public static Vector3 DeadZoneCenter(Vector3 current,Vector3 player,Vector2 halfSize)
    {
        Vector3 next=current;
        if(player.x>current.x+halfSize.x) next.x=player.x-halfSize.x;
        if(player.x<current.x-halfSize.x) next.x=player.x+halfSize.x;
        if(player.z>current.z+halfSize.y) next.z=player.z-halfSize.y;
        if(player.z<current.z-halfSize.y) next.z=player.z+halfSize.y;
        return next;
    }
    private void UpdatePose(bool snap)
    {
        if(target==null) return;
        Vector3 desired=DeadZoneCenter(center,target.position,deadZoneHalfSize);
        desired.x=Mathf.Clamp(desired.x,-6,6); desired.z=Mathf.Clamp(desired.z,-3,3);
        center=snap?desired:Vector3.SmoothDamp(center,desired,ref velocity,smoothTime,20,Time.unscaledDeltaTime);
        transform.SetPositionAndRotation(center+Quaternion.Euler(78,0,0)*Vector3.back*32,Quaternion.Euler(78,0,0));
        if(outputCamera==null) return;
        outputCamera.orthographic=true;
        outputCamera.orthographicSize=Mathf.Max(viewSize,19/Mathf.Max(.5f,outputCamera.aspect));
        outputCamera.clearFlags=CameraClearFlags.SolidColor;
        outputCamera.backgroundColor=new Color(.83f,.86f,.82f);
    }
}
