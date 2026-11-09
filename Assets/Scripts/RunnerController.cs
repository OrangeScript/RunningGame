using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class RunnerController : NetworkBehaviour
{
    [SerializeField] private float forwardSpeed=4f;
    [SerializeField] private float horizontalSpeed=7f;
    [SerializeField] private float xLimit=RunSectionPresentation.MovementHalfWidth;
    private readonly NetworkVariable<PlayerRunPhase> playerPhase=new NetworkVariable<PlayerRunPhase>(PlayerRunPhase.Lobby);
    private readonly NetworkVariable<int> laneId=new NetworkVariable<int>();
    private readonly NetworkVariable<float> laneCenterX=new NetworkVariable<float>();
    private readonly NetworkVariable<float> laneHalfWidth=new NetworkVariable<float>(RunSectionPresentation.MovementHalfWidth);
    private CharacterController controller;
    private PlayerInput playerInput;
    private CrowdManager crowd;
    private ArenaRemoteBuffer remote;
    private readonly MotionPrediction prediction=new MotionPrediction();
    private readonly MovementInputBudget budget=new MovementInputBudget();
    private float input,accumulator;
    private ushort sequence,lastProcessed;
    private bool receivedInput;
    private uint revision,receivedRevision;
    private double nextSnapshot,lastSnapshot=-1;
    private Vector3 velocity,visualCorrection;
    public PlayerRunPhase Phase=>playerPhase.Value;
    public int LaneId=>laneId.Value;
    public float LaneCenterX=>laneCenterX.Value;
    public float MovementHalfWidth=>laneHalfWidth.Value;
    private bool CanMove=>Phase==PlayerRunPhase.Running || Phase==PlayerRunPhase.Combat;
    private void Awake()
    {
        controller=GetComponent<CharacterController>(); playerInput=GetComponent<PlayerInput>(); crowd=GetComponent<CrowdManager>();
        remote=gameObject.AddComponent<ArenaRemoteBuffer>();
        if(playerInput!=null) playerInput.enabled=false;
        controller.enabled=false;
    }
    public override void OnNetworkSpawn()
    {
        if(playerInput!=null) playerInput.enabled=IsOwner;
        controller.enabled=IsServer||IsOwner; remote.SetPlaybackEnabled(!IsServer&&!IsOwner);
        playerPhase.OnValueChanged+=HandlePhase;
        if(IsServer) { laneCenterX.Value=transform.position.x; laneHalfWidth.Value=xLimit; }
        if(crowd!=null) crowd.PopulationDepleted+=HandleDepleted;
        if(!IsOwner) return;
        Camera.main?.GetComponent<CameraFollow>()?.SetTarget(transform);
        RunnerRuntimeHUD.Ensure(this,crowd,GetComponent<ItemManager>());
    }
    public override void OnNetworkDespawn()
    {
        if(playerInput!=null) playerInput.enabled=false;
        if(crowd!=null) crowd.PopulationDepleted-=HandleDepleted;
        playerPhase.OnValueChanged-=HandlePhase; prediction.Clear();
    }
    private void OnMove(InputValue value) { if(IsOwner) input=Mathf.Clamp(value.Get<float>(),-1,1); }
    private void HandlePhase(PlayerRunPhase previous,PlayerRunPhase current)
    { if(!CanMove) { prediction.Clear(); accumulator=0; velocity=Vector3.zero; } }
    private void Update()
    {
        if(!IsSpawned) return;
        if(IsOwner && CanMove)
        {
            accumulator=Mathf.Min(.1f,accumulator+Time.deltaTime);
            while(accumulator>=MotionPrediction.Step)
            {
                accumulator-=MotionPrediction.Step;
                var frame=ArenaProtocol.Pack(++sequence,new Vector2(input,0),Vector2.up,false);
                if(IsServer) Process(frame);
                else { Simulate(frame); prediction.Record(frame); SubmitInputsServerRpc(prediction.RedundantBatch(),receivedRevision); }
            }
        }
        if(IsServer && Time.unscaledTimeAsDouble>=nextSnapshot)
        {
            nextSnapshot=Time.unscaledTimeAsDouble+1d/30;
            SnapshotClientRpc(new ArenaStateSnapshot{ServerTime=NetworkManager.ServerTime.Time,Position=transform.position,
                Forward=transform.forward,Velocity=velocity,LastProcessedInput=lastProcessed},revision);
        }
    }
    private void LateUpdate()
    {
        if(!IsOwner||IsServer) return;
        Transform avatar=transform.Find("Capsule"); if(avatar==null) return;
        visualCorrection=Vector3.Lerp(visualCorrection,Vector3.zero,1-Mathf.Exp(-24*Time.deltaTime));
        avatar.localPosition=Vector3.up+visualCorrection;
    }
    [ServerRpc(Delivery=RpcDelivery.Unreliable)]
    private void SubmitInputsServerRpc(ArenaInputFrame[] frames,uint inputRevision,ServerRpcParams rpc=default)
    {
        if(rpc.Receive.SenderClientId!=OwnerClientId||frames==null||frames.Length>4||inputRevision!=revision) return;
        foreach(var frame in frames) Process(frame);
    }
    private void Process(ArenaInputFrame frame)
    {
        if(!CanMove||(receivedInput&&!ArenaProtocol.IsSequenceNewer(frame.Sequence,lastProcessed))) return;
        if(!budget.Consume(Time.unscaledTimeAsDouble)) return;
        receivedInput=true; lastProcessed=frame.Sequence; Simulate(frame);
    }
    private void Simulate(ArenaInputFrame frame)
    {
        if(!CanMove||!controller.enabled||!ArenaProtocol.TryDecode(frame,out Vector2 move,out _,out _)) return;
        Vector3 before=transform.position;
        float x=Mathf.Clamp(before.x+move.x*horizontalSpeed*MotionPrediction.Step,LaneCenterX-MovementHalfWidth,LaneCenterX+MovementHalfWidth);
        controller.Move(new Vector3(x-before.x,-2*MotionPrediction.Step,Phase==PlayerRunPhase.Running?forwardSpeed*MotionPrediction.Step:0));
        velocity=(transform.position-before)/MotionPrediction.Step;
    }
    [ClientRpc(Delivery=RpcDelivery.Unreliable)]
    private void SnapshotClientRpc(ArenaStateSnapshot snapshot,uint serverRevision)
    {
        if(IsServer||snapshot.ServerTime<=lastSnapshot) return;
        lastSnapshot=snapshot.ServerTime;
        if(!IsOwner) { remote.Push(snapshot); return; }
        Vector3 before=transform.position;
        if(receivedRevision!=serverRevision) { receivedRevision=serverRevision; prediction.Clear(); visualCorrection=Vector3.zero; }
        prediction.Acknowledge(snapshot.LastProcessedInput);
        controller.enabled=false; transform.position=snapshot.Position; controller.enabled=true;
        foreach(var frame in prediction.Pending) Simulate(frame);
        Vector3 correction=before-transform.position;
        if(correction.sqrMagnitude<9) visualCorrection+=correction;
    }
    public void ConfigureLaneServer(int lane,float center,float halfWidth)
    { if(!IsServer) return; laneId.Value=Mathf.Max(0,lane); laneCenterX.Value=center; laneHalfWidth.Value=Mathf.Max(.5f,halfWidth); }
    public void TeleportToLaneServer(int lane,Vector3 position,float halfWidth)
    {
        if(!IsServer) return;
        controller.enabled=false; transform.position=position; controller.enabled=true;
        ConfigureLaneServer(lane,position.x,halfWidth); revision++; velocity=Vector3.zero;
    }
    public void BeginRunServer()=>SetPhase(PlayerRunPhase.Running);
    public void EnterCombatServer()=>SetPhase(PlayerRunPhase.Combat);
    public void EnterRewardChoiceServer()=>SetPhase(PlayerRunPhase.RewardChoice);
    public void ResumeRunningServer()=>SetPhase(PlayerRunPhase.Running);
    public void WaitAtExitServer()=>SetPhase(PlayerRunPhase.WaitingAtExit);
    private void SetPhase(PlayerRunPhase value) { if(IsServer) playerPhase.Value=value; }
    private void HandleDepleted() { if(!IsServer)return; SetPhase(PlayerRunPhase.Eliminated); GameManager.instance?.NotifyRunnerEliminated(this); }
}
