using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class ArenaPlayerNetwork : NetworkBehaviour, IArenaDamageable
{
    public static readonly HashSet<ArenaPlayerNetwork> ActivePlayers=new HashSet<ArenaPlayerNetwork>();
    private readonly NetworkVariable<float> grazeCharge=new NetworkVariable<float>();
    private float invulnerableUntil,shieldUntil,shieldPoints,nextRegeneration;
    private int shotNumber;
    private Vector2 localAim=Vector2.up;
    private bool localFireHeld;
    private float visualHurtUntil;
    public float GrazeCharge=>grazeCharge.Value;
    public float HitRadius=>IsFocused?.16f:.22f;
    [Header("Components")]
    [SerializeField] private ArenaInputReader inputReader;
    [SerializeField] private ArenaMotor motor;
    [SerializeField] private ArenaRemoteBuffer remoteBuffer;
    [SerializeField] private ArenaAnimatorDriver animatorDriver;
    [SerializeField] private ItemManager itemManager;

    [Header("Input Networking")]
    [SerializeField, Min(0.05f)] private float inputTimeout = 0.35f;

    [Header("State Snapshots")]
    [SerializeField, Range(1f, 30f)] private float snapshotSendRate = 15f;

    [Header("Weapon")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private ArenaProjectile projectilePrefab;
    [SerializeField, Min(0.1f)] private float baseShotsPerSecond = 5f;
    [SerializeField, Min(0f)] private float baseProjectileDamage = 1f;
    [SerializeField, Min(0.1f)] private float baseProjectileSpeed = 18f;
    [SerializeField, Min(0.05f)] private float projectileLifetime = 3f;
    [SerializeField, Range(1, 16)] private int maxProjectilesPerShot = 8;
    [SerializeField, Range(0f, 45f)] private float projectileSpreadDegrees = 5f;

    [Header("Health / Team")]
    [SerializeField, Min(1f)] private float maxHealth = 10f;
    [SerializeField] private int teamId = 1;

    private readonly NetworkVariable<float> currentHealth =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> runFirepower = new NetworkVariable<int>(20);
    public int Firepower => runFirepower.Value;
    public void SetRunFirepowerServer(int value)
    {
        if (IsServer) runFirepower.Value = Mathf.Clamp(value,1,9999);
    }

    private ArenaCameraRig boundCameraRig;
    private Vector2 serverAim = Vector2.up;
    private bool serverFireHeld;
    private bool hasReceivedInput;
    private ushort lastReceivedSequence;
    private ushort localSequence;
    private double lastServerInputTime;
    private double nextSnapshotSendTime;
    private double nextFireTime;
    private readonly MotionPrediction prediction = new MotionPrediction();
    private readonly MovementInputBudget movementBudget = new MovementInputBudget();
    private float inputAccumulator;
    private double lastSnapshotTime = -1;
    private Vector3 visualCorrection;
    public Vector3 LatestAuthorityPosition { get; private set; }
    public bool IsLocallyPredicted => IsOwner && !IsServer && motor != null && motor.SimulationEnabled;
    public bool IsFocused { get; private set; }

    public event Action<float, float> HealthChanged;

    public int ArenaTeamId => teamId;
    public bool IsArenaAlive => currentHealth.Value > 0f;
    public bool IsParticipatingPawn => NetworkObject != null && NetworkObject.IsPlayerObject;
    public float CurrentHealth => currentHealth.Value;
    public float MaxHealth => maxHealth;

    private void Awake()
    {
        ResolveComponents();
    }

    public override void OnNetworkSpawn()
    {
        ResolveComponents();
        currentHealth.OnValueChanged += HandleHealthChanged;
        ActivePlayers.Add(this);

        if (IsServer)
        {
            currentHealth.Value = Mathf.Max(1f, maxHealth);
            invulnerableUntil=Time.time+1.2f;
            lastServerInputTime = Time.unscaledTimeAsDouble;
        }

        bool alive = currentHealth.Value > 0f && IsParticipatingPawn;
        if (inputReader != null)
        {
            inputReader.SetOwnerInputEnabled(IsOwner && alive);
        }

        if (motor != null)
        {
            motor.SetSimulationEnabled((IsServer || IsOwner) && alive);
        }

        if (remoteBuffer != null)
        {
            remoteBuffer.SetPlaybackEnabled(!IsServer && !IsOwner);
        }

        if (IsOwner)
        {
            BindLocalCamera();
        }

        HealthChanged?.Invoke(currentHealth.Value, currentHealth.Value);
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= HandleHealthChanged;
        ActivePlayers.Remove(this);

        if (inputReader != null)
        {
            inputReader.SetOwnerInputEnabled(false);
        }

        if (motor != null)
        {
            motor.SetSimulationEnabled(false);
        }

        if (remoteBuffer != null)
        {
            remoteBuffer.SetPlaybackEnabled(false);
        }

        if (boundCameraRig != null)
        {
            boundCameraRig.Unbind(transform);
            boundCameraRig = null;
        }
    }

    private void Update()
    {
        if (!IsSpawned)
        {
            return;
        }

        if (!IsParticipatingPawn)
            return;

        double now = Time.unscaledTimeAsDouble;
        if (IsOwner)
        {
            SimulateOwnerInputs();
            if(!IsServer&&localFireHeld&&IsArenaAlive)TryFire(now);
        }

        if (IsServer)
        {
            SimulateServer(now);
            PublishSnapshotIfDue(now);
            if(Time.time>=nextRegeneration)
            { nextRegeneration=Time.time+1; HealServer(itemManager!=null?itemManager.GetArenaStats().Regeneration:0); }
        }
        if(IsOwner&&UnityEngine.InputSystem.Keyboard.current!=null&&UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
            RequestGrazeBurstServerRpc();
    }

    private void LateUpdate()
    {
        Transform avatar=transform.Find("ArenaAvatar");
        if (avatar == null) return;
        if(IsOwner&&!IsServer)
        {
            visualCorrection=Vector3.Lerp(visualCorrection,Vector3.zero,1-Mathf.Exp(-24*Time.deltaTime));
            avatar.localPosition=Vector3.up+transform.InverseTransformVector(visualCorrection);
        }
        var body=avatar.GetComponent<Renderer>();
        if(body!=null)body.enabled=Time.time>=visualHurtUntil||((int)(Time.time*16)&1)==0;
    }

    private void SimulateOwnerInputs()
    {
        if (inputReader == null || motor == null || !IsArenaAlive) return;
        inputAccumulator=Mathf.Min(.1f,inputAccumulator+Time.deltaTime);
        while (inputAccumulator >= MotionPrediction.Step)
        {
            inputAccumulator-=MotionPrediction.Step;
            if (!inputReader.TryCapture(++localSequence,out ArenaInputFrame frame)) continue;
            ArenaProtocol.TryDecode(frame,out _,out localAim,out localFireHeld);
            if (IsServer) ProcessCommand(frame);
            else
            {
                SimulateFrame(frame);
                prediction.Record(frame);
                SubmitInputBatchServerRpc(prediction.RedundantBatch());
            }
        }
    }

    private void SimulateFrame(ArenaInputFrame frame)
    {
        if (!ArenaProtocol.TryDecode(frame,out Vector2 move,out Vector2 aim,out _)) return;
        IsFocused=frame.FocusHeld;
        float speed = frame.FocusHeld ? .42f : 1f;
        if (itemManager != null) speed*=itemManager.ArenaMoveSpeedMultiplier;
        motor.Simulate(move,aim,MotionPrediction.Step,speed);
        animatorDriver?.SetMotion(motor.PlanarVelocity,motor.MaxSpeed);
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    private void SubmitInputBatchServerRpc(ArenaInputFrame[] frames,ServerRpcParams rpc=default)
    {
        if (rpc.Receive.SenderClientId != OwnerClientId || frames == null || frames.Length > 4) return;
        foreach (var frame in frames) ProcessCommand(frame);
    }

    private void ProcessCommand(ArenaInputFrame frame)
    {
        if (!IsArenaAlive || motor == null ||
            (hasReceivedInput && !ArenaProtocol.IsSequenceNewer(frame.Sequence,lastReceivedSequence))) return;
        if (!ArenaProtocol.TryDecode(frame,out _,out Vector2 aim,out bool fire)) return;
        double now=Time.unscaledTimeAsDouble;
        if (!movementBudget.Consume(now)) return;
        hasReceivedInput=true; lastReceivedSequence=frame.Sequence;
        serverAim=aim; serverFireHeld=fire; lastServerInputTime=now;
        SimulateFrame(frame);
    }

    public void ApplyArenaDamage(ArenaDamageInfo damage)
    {
        if (!IsServer || !IsArenaAlive)
        {
            return;
        }

        if (!ArenaProtocol.IsFinite(damage.Amount) || damage.Amount <= 0f || Time.time<invulnerableUntil)
        {
            return;
        }

        var stats=itemManager!=null?itemManager.GetArenaStats():new ArenaBuildStats();
        float amount=damage.Amount*(1-Mathf.Clamp(stats.DamageReduction,0,.45f));
        if(Time.time<shieldUntil&&shieldPoints>0)
        { float absorbed=Mathf.Min(shieldPoints,amount); shieldPoints-=absorbed; amount-=absorbed; }
        invulnerableUntil=Time.time+.6f+stats.ExtraInvulnerability;
        currentHealth.Value = Mathf.Max(0f, currentHealth.Value - amount);
    }
    public void HealServer(float amount) { if(IsServer&&IsArenaAlive&&amount>0)currentHealth.Value=Mathf.Min(maxHealth,currentHealth.Value+amount); }
    public void GrantShieldServer(float amount,float duration=6) { if(IsServer){shieldPoints=Mathf.Max(shieldPoints,amount);shieldUntil=Time.time+duration;} }
    public void AddGrazeServer()
    { if(IsServer)grazeCharge.Value=Mathf.Min(100,grazeCharge.Value+4*(itemManager!=null?itemManager.GetArenaStats().GrazeMultiplier:1)); }
    [ServerRpc]
    private void RequestGrazeBurstServerRpc()
    {
        if(!IsArenaAlive||grazeCharge.Value<100)return;
        grazeCharge.Value=0;FindFirstObjectByType<ArenaBootstrap>()?.ClearBossBullets(transform.position,6);
        GrantShieldServer(1,3);
    }

    /// <summary>
    /// Server-only firing hook for encounter directors and Play Mode smoke
    /// tests. It exercises the same projectile and item modifier path as owner
    /// input without accepting any client-supplied damage value.
    /// </summary>
    public bool FireServerAt(Vector3 worldTarget, float damageMultiplier = 1f)
    {
        if (!IsServer || !IsSpawned || !IsParticipatingPawn || !IsArenaAlive)
            return false;

        Vector3 direction = worldTarget - transform.position;
        direction.y = 0f;
        if (!ArenaProtocol.IsFinite(direction) || direction.sqrMagnitude < 0.001f)
            return false;

        SpawnProjectile(direction.normalized, Mathf.Clamp(damageMultiplier, 0f, 1000f));
        return true;
    }

    private void SimulateServer(double now)
    {
        bool inputFresh = hasReceivedInput && now - lastServerInputTime <= inputTimeout;
        bool fire = inputFresh && IsArenaAlive && serverFireHeld;

        if (!inputFresh) motor?.Stop();

        if (animatorDriver != null)
        {
            Vector3 velocity = motor != null ? motor.PlanarVelocity : Vector3.zero;
            float referenceSpeed = motor != null ? motor.MaxSpeed : 1f;
            animatorDriver.SetMotion(velocity, referenceSpeed);
        }

        if (fire)
        {
            TryFire(now);
        }
    }

    private void TryFire(double now)
    {
        if (now < nextFireTime)
        {
            return;
        }

        float baseInterval = 1f / Mathf.Max(0.1f, baseShotsPerSecond);
        AttackPlan plan = new AttackPlan
        {
            damageMultiplier = 1f,
            attackInterval = baseInterval,
            attackRange = 0f,
            projectileCount = 1,
            shotSpacing = 0f,
            projectileArchetype = null
        };

        if (itemManager != null)
        {
            itemManager.ModifyAttack(plan);
        }

        float interval = SanitizeRange(plan.attackInterval, baseInterval, 0.04f, 10f);
        int projectileCount = Mathf.Clamp(plan.projectileCount, 1, maxProjectilesPerShot);
        float damageMultiplier = SanitizeRange(plan.damageMultiplier, 1f, 0f, 1000f);
        nextFireTime = now + interval;

        Vector2 aim=IsServer?serverAim:localAim;
        Vector3 forward = new Vector3(aim.x, 0f, aim.y);
        if (!ArenaProtocol.IsFinite(forward) || forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        float totalSpread = projectileSpreadDegrees * Mathf.Max(0, projectileCount - 1);

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = projectileCount > 1
                ? -totalSpread * 0.5f + projectileSpreadDegrees * i
                : 0f;

            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            SpawnProjectile(direction, damageMultiplier);
        }
    }

    private void SpawnProjectile(Vector3 direction, float damageMultiplier)
    {
        ProjectileSpec spec = new ProjectileSpec
        {
            damage = Mathf.Max(0f, baseProjectileDamage * damageMultiplier * Mathf.Clamp(Firepower/20f,.1f,50f)),
            speed = baseProjectileSpeed,
            sizeMultiplier = 1f,
            element = ElementType.None
        };

        if (itemManager != null)
        {
            itemManager.ModifyProjectile(spec);
        }
        if(spec.secondaryElement!=ElementType.None&&(shotNumber++&1)==1)spec.element=spec.secondaryElement;

        float damage = SanitizeRange(spec.damage, baseProjectileDamage, 0f, 100000f);
        float speed = SanitizeRange(spec.speed, baseProjectileSpeed, 0.1f, 100f);
        float size = SanitizeRange(spec.sizeMultiplier, 1f, 0.05f, 20f);

        Vector3 origin = firePoint != null
            ? firePoint.position
            : transform.position + Vector3.up * 0.75f + direction * 0.65f;
        if(!IsServer)
        {
            ArenaRuntimeProjectile.SpawnVisual(origin,direction,speed,projectileLifetime,size,teamId,spec.element,spec.bounce);
            return;
        }

        if (projectilePrefab == null)
        {
            ArenaRuntimeProjectile.SpawnServer(
                origin,
                direction,
                speed,
                projectileLifetime,
                damage,
                size,
                teamId,
                OwnerClientId,
                spec.element,
                spec.pierce,
                spec.bounce,
                spec.explosionRadius>0?spec.explosionDamageMultiplier:0);
            SpawnRuntimeProjectileVisualClientRpc(
                origin,
                direction,
                speed,
                projectileLifetime,
                size,
                teamId,
                spec.element,
                spec.bounce);
            return;
        }

        ArenaProjectile projectile = Instantiate(
            projectilePrefab,
            origin,
            Quaternion.LookRotation(direction, Vector3.up));

        if (projectile == null)
        {
            return;
        }

        projectile.PrepareServer(
            origin,
            direction,
            speed,
            projectileLifetime,
            damage,
            size,
            teamId,
            OwnerClientId,
            spec.element);

        NetworkObject projectileObject = projectile.NetworkObject;
        if (projectileObject == null)
        {
            Destroy(projectile.gameObject);
            return;
        }

        projectileObject.Spawn(true);
    }

    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    private void SpawnRuntimeProjectileVisualClientRpc(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float lifetime,
        float size,
        int projectileTeam,
        ElementType element,
        int bounce)
    {
        if (IsServer || IsOwner)
            return;

        ArenaRuntimeProjectile.SpawnVisual(
            origin,
            direction,
            speed,
            lifetime,
            size,
            projectileTeam,
            element,
            bounce);
    }

    private void PublishSnapshotIfDue(double now)
    {
        if (now < nextSnapshotSendTime)
        {
            return;
        }

        float safeRate = Mathf.Clamp(snapshotSendRate, 1f, 30f);
        nextSnapshotSendTime = now + 1d / safeRate;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        ArenaStateSnapshot snapshot = new ArenaStateSnapshot
        {
            ServerTime = NetworkManager != null
                ? NetworkManager.ServerTime.Time
                : Time.unscaledTimeAsDouble,
            Position = transform.position,
            Forward = forward.normalized,
            Velocity = motor != null ? motor.PlanarVelocity : Vector3.zero,
            LastProcessedInput = lastReceivedSequence
        };

        PublishSnapshotClientRpc(snapshot);
    }

    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    private void PublishSnapshotClientRpc(ArenaStateSnapshot snapshot)
    {
        if (IsServer)
        {
            return;
        }

        if (snapshot.ServerTime <= lastSnapshotTime) return;
        lastSnapshotTime=snapshot.ServerTime;
        LatestAuthorityPosition=snapshot.Position;
        if (IsOwner && motor != null)
        {
            Vector3 previous=transform.position;
            prediction.Acknowledge(snapshot.LastProcessedInput);
            motor.Restore(snapshot.Position,snapshot.Velocity);
            foreach (var frame in prediction.Pending) SimulateFrame(frame);
            Vector3 correction=previous-transform.position;
            if (correction.sqrMagnitude < 9) visualCorrection+=correction;
        }

        if (!IsOwner && remoteBuffer != null)
        {
            remoteBuffer.Push(snapshot);
        }

        if (animatorDriver != null)
        {
            float referenceSpeed = motor != null ? motor.MaxSpeed : 1f;
            animatorDriver.SetMotion(snapshot.Velocity, referenceSpeed);
        }
    }

    private void BindLocalCamera()
    {
        boundCameraRig = FindFirstObjectByType<ArenaCameraRig>();
        if (boundCameraRig == null)
        {
            return;
        }

        boundCameraRig.Bind(transform);
        if (inputReader != null)
        {
            inputReader.SetAimCamera(boundCameraRig.OutputCamera);
        }
    }

    private void HandleHealthChanged(float previous, float current)
    {
        HealthChanged?.Invoke(previous, current);
        if(current<previous)visualHurtUntil=Time.time+.6f;
        bool alive = current > 0f;

        if (inputReader != null)
        {
            inputReader.SetOwnerInputEnabled(IsOwner && alive);
        }

        if (motor != null && (IsServer || IsOwner))
        {
            motor.SetSimulationEnabled(alive);
            if (!alive)
            {
                motor.Stop();
            }
        }

        if (!alive)
        {
            serverFireHeld = false;
            animatorDriver?.ResetMotion();
        }
    }

    private void ResolveComponents()
    {
        if (inputReader == null)
        {
            inputReader = GetComponent<ArenaInputReader>();
        }

        if (motor == null)
        {
            motor = GetComponent<ArenaMotor>();
        }

        if (remoteBuffer == null)
        {
            remoteBuffer = GetComponent<ArenaRemoteBuffer>();
        }

        if (animatorDriver == null)
        {
            animatorDriver = GetComponent<ArenaAnimatorDriver>();
        }

        if (itemManager == null)
        {
            itemManager = GetComponent<ItemManager>();
        }
    }

    /// <summary>
    /// Called by ArenaBootstrap after it adds optional non-network components
    /// to a minimal registered Player_Arena prefab.
    /// </summary>
    public void RefreshRuntimeSetup()
    {
        ResolveComponents();
        bool alive = currentHealth.Value > 0f && IsParticipatingPawn;

        inputReader?.SetOwnerInputEnabled(IsOwner && alive);
        motor?.SetSimulationEnabled((IsServer || IsOwner) && alive);
        remoteBuffer?.SetPlaybackEnabled(!IsServer && !IsOwner);

        if (IsServer && itemManager != null)
            itemManager.SetGameplayModeServer(GameplayMode.BossArena);

        if (IsOwner)
            BindLocalCamera();
    }

    private static float SanitizeRange(
        float value,
        float fallback,
        float minimum,
        float maximum)
    {
        if (!ArenaProtocol.IsFinite(value))
        {
            value = fallback;
        }

        return Mathf.Clamp(value, minimum, maximum);
    }

    private void OnValidate()
    {
        inputTimeout = Mathf.Max(0.05f, inputTimeout);
        snapshotSendRate = Mathf.Clamp(snapshotSendRate, 1f, 30f);
        baseShotsPerSecond = Mathf.Max(0.1f, baseShotsPerSecond);
        baseProjectileDamage = Mathf.Max(0f, baseProjectileDamage);
        baseProjectileSpeed = Mathf.Max(0.1f, baseProjectileSpeed);
        projectileLifetime = Mathf.Max(0.05f, projectileLifetime);
        maxProjectilesPerShot = Mathf.Clamp(maxProjectilesPerShot, 1, 16);
        projectileSpreadDegrees = Mathf.Clamp(projectileSpreadDegrees, 0f, 45f);
        maxHealth = Mathf.Max(1f, maxHealth);
    }
}
