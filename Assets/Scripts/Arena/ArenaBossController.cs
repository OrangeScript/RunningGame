using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum ArenaMatchResult : byte
{
    None = 0,
    Victory = 1,
    Defeat = 2
}

/// <summary>
/// Server-only Boss simulation. Three deterministic patterns are selected in
/// sequence and become denser at 65% and 30% health.
/// </summary>
public sealed class ArenaBossController : MonoBehaviour, IArenaDamageable
{
    private ArenaBootstrap bridge;
    private float maxHealth;
    private float health;
    private float nextPatternTime;
    private float nextStateBroadcast;
    private int patternCounter;
    private bool hadPlayers;
    private ArenaMatchResult result;

    public int ArenaTeamId => 2;
    public bool IsArenaAlive => health > 0f && result == ArenaMatchResult.None;
    public float Health => health;
    public float MaxHealth => maxHealth;
    public ArenaMatchResult Result => result;

    public void Initialize(ArenaBootstrap owner, float baseHealth)
    {
        bridge = owner;
        int players = Mathf.Max(1, CountActivePlayers());
        maxHealth = Mathf.Max(50f, baseHealth) * (1f + 0.55f * (players - 1));
        health = maxHealth;
        nextPatternTime = Time.time + 1.5f;
        nextStateBroadcast = Time.time;
        BroadcastState();
    }

    private void Update()
    {
        if (bridge == null || !bridge.IsServerAuthority || result != ArenaMatchResult.None)
            return;

        int alivePlayers = CountActivePlayers();
        if (alivePlayers > 0)
            hadPlayers = true;
        else if (hadPlayers)
        {
            Finish(ArenaMatchResult.Defeat);
            return;
        }

        if (!hadPlayers)
            return;

        if (Time.time >= nextPatternTime)
        {
            EmitNextPattern();
            float healthRatio = maxHealth > 0f ? health / maxHealth : 0f;
            nextPatternTime = Time.time + (healthRatio > 0.65f ? 2.7f : healthRatio > 0.3f ? 2.05f : 1.45f);
        }

        if (Time.time >= nextStateBroadcast)
        {
            nextStateBroadcast = Time.time + 0.5f;
            BroadcastState();
        }
    }

    public void ApplyArenaDamage(ArenaDamageInfo damage)
    {
        if (!IsArenaAlive || bridge == null || !bridge.IsServerAuthority)
            return;
        if (damage.SourceTeamId == ArenaTeamId || !ArenaProtocol.IsFinite(damage.Amount) || damage.Amount <= 0f)
            return;

        health = Mathf.Max(0f, health - damage.Amount);
        BroadcastState();
        if (health <= 0f)
            Finish(ArenaMatchResult.Victory);
    }

    private void EmitNextPattern()
    {
        switch (patternCounter % 3)
        {
            case 0:
                EmitRadialBurst();
                break;
            case 1:
                EmitAimedFan();
                break;
            default:
                EmitSpiralArms();
                break;
        }

        patternCounter++;
    }

    private void EmitRadialBurst()
    {
        float ratio = health / maxHealth;
        int count = ratio > 0.65f ? 18 : ratio > 0.3f ? 24 : 32;
        float offset = DeterministicAngle(patternCounter, 17);
        for (int i = 0; i < count; i++)
        {
            float angle = offset + 360f * i / count;
            Emit(AngleDirection(angle), 5.8f, 1f, 0.2f);
        }
    }

    private void EmitAimedFan()
    {
        ArenaPlayerNetwork[] players = FindObjectsByType<ArenaPlayerNetwork>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < players.Length; i++)
        {
            ArenaPlayerNetwork player = players[i];
            if (!IsParticipatingPlayer(player))
                continue;

            Vector3 direction = player.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector3.back;

            direction.Normalize();
            for (int shot = -2; shot <= 2; shot++)
            {
                Vector3 spread = Quaternion.AngleAxis(shot * 10f, Vector3.up) * direction;
                Emit(spread, 8.5f, 1.25f, 0.17f);
            }
        }
    }

    private void EmitSpiralArms()
    {
        float baseAngle = DeterministicAngle(patternCounter, 71);
        int arms = health / maxHealth > 0.3f ? 4 : 6;
        for (int ring = 0; ring < 5; ring++)
        {
            for (int arm = 0; arm < arms; arm++)
            {
                float angle = baseAngle + arm * (360f / arms) + ring * 13f;
                Emit(AngleDirection(angle), 4.5f + ring * 0.8f, 0.85f, 0.18f);
            }
        }
    }

    private void Emit(Vector3 direction, float speed, float damage, float radius)
    {
        bridge.SpawnBossBullet(
            transform.position + Vector3.up * 0.3f + direction * 1.3f,
            direction,
            speed,
            7f,
            damage,
            radius);
    }

    private void Finish(ArenaMatchResult finalResult)
    {
        if (result != ArenaMatchResult.None)
            return;

        result = finalResult;
        BroadcastState();
        bridge.NotifyMatchEnded(finalResult);
    }

    private void BroadcastState()
    {
        bridge?.BroadcastBossState(health, maxHealth, result);
    }

    private static int CountActivePlayers()
    {
        ArenaPlayerNetwork[] players = FindObjectsByType<ArenaPlayerNetwork>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < players.Length; i++)
        {
            if (IsParticipatingPlayer(players[i]))
                count++;
        }

        return count;
    }

    private static bool IsParticipatingPlayer(ArenaPlayerNetwork player)
    {
        return player != null &&
               player.IsSpawned &&
               player.NetworkObject != null &&
               player.NetworkObject.IsPlayerObject &&
               player.IsArenaAlive;
    }

    private static Vector3 AngleDirection(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
    }

    private static float DeterministicAngle(int counter, int salt)
    {
        unchecked
        {
            uint value = (uint)(counter + 1) * 747796405u + (uint)salt * 2891336453u;
            value ^= value >> 16;
            return (value % 36000u) * 0.01f;
        }
    }
}
