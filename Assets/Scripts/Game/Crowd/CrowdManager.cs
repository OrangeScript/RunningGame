using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>Legacy population storage now represents firepower, never avatar count.</summary>
public class CrowdManager : NetworkBehaviour
{
    [SerializeField, Min(1)] private int startingPopulation = 20;
    [SerializeField, Min(1)] private int startingHealth = 100;
    private readonly NetworkVariable<int> population = new NetworkVariable<int>(20);
    private readonly NetworkVariable<int> health = new NetworkVariable<int>(100);
    public int Population => population.Value;
    public int Firepower => population.Value;
    public int Health => health.Value;
    public int MaxHealth => startingHealth;
    public event Action<int> PopulationChanged;
    public event Action<int> HealthChanged;
    public event Action PopulationDepleted;

    public override void OnNetworkSpawn()
    {
        population.OnValueChanged += HandlePower;
        health.OnValueChanged += HandleHealth;
        if (IsServer) { population.Value = startingPopulation; health.Value = startingHealth; }
        PopulationChanged?.Invoke(population.Value);
        HealthChanged?.Invoke(health.Value);
    }
    public override void OnNetworkDespawn()
    {
        population.OnValueChanged -= HandlePower;
        health.OnValueChanged -= HandleHealth;
    }
    private void HandlePower(int before,int after) => PopulationChanged?.Invoke(after);
    private void HandleHealth(int before,int after)
    {
        HealthChanged?.Invoke(after);
        if (before > 0 && after == 0) PopulationDepleted?.Invoke();
    }
    public void SetPopulation(int value) { if (IsServer) population.Value = Mathf.Clamp(value,1,9999); }
    public void DamagePopulation(int damage) => DamageHealth(damage);
    public void DamageHealth(int damage) { if (IsServer && damage > 0) health.Value = Mathf.Max(0,health.Value-damage); }
    public void AddPopulation(int amount) => SetPopulation((int)Math.Min(9999L,(long)Population+amount));
    public void SubtractPopulation(int amount) => SetPopulation(Population-Mathf.Max(0,amount));
    public void MultiplyPopulation(int multiplier) => SetPopulation((int)Math.Min(9999L,(long)Population*Mathf.Max(1,multiplier)));
    public void DividePopulation(int divisor) { if (divisor > 0) SetPopulation(Population/divisor); }
}
