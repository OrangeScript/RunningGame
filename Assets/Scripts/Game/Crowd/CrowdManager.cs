using System;
using Unity.Netcode;
using UnityEngine;

public class CrowdManager : NetworkBehaviour
{
    [Header("Population")]
    [SerializeField] 
    [Min(0)]
    private int startingPopulation = 1;

    private NetworkVariable<int> population = new NetworkVariable<int>(
        1,NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public int Population =>population.Value;


    public event Action<int> PopulationChanged;

    // [Header("Crowd")]
    // [SerializeField] private GameObject unitPrefab;
    // [SerializeField] private Transform crowdRoot;

    // [SerializeField] private TMP_Text populationText;

    // [Header("Formation")]
    // [SerializeField] private float spacing = 0.65f;
    // private readonly List<GameObject> units = new List<GameObject>();
    // public int UnitCount => units.Count;
    // public Transform GetUnitTransform(int index)
    // {
    //     if(index < 0 || index >= units.Count)
    //     {
    //         return null;
    //     }

    //     return units[index].transform;
    // }

    // void Start()
    // {
    //     SetPopulation(population);
    // }

    // public void DamagePopulation(int damage)
    // {
    //     if(damage <= 0)
    //     {
    //         return;
    //     }
    //     SetPopulation(population - damage);
    // }

    public override void OnNetworkSpawn()
    {
        population.OnValueChanged += HandlePopulationChanged;
        PopulationChanged?.Invoke(population.Value);
    }

    public override void OnNetworkDespawn()
    {
        population.OnValueChanged -= HandlePopulationChanged;
    }

    private void HandlePopulationChanged(int previousValue,
        int newValue)
    {
        PopulationChanged?.Invoke(newValue);
    }

    public void SetPopulation(int value)
    {
        if(!IsServer) return;
        population.Value = Mathf.Max(0,value);
    }

    // public void OnCrowdDead()
    // {
    //     Debug.Log("Game Over!");
    //     RunnerController runner = GetComponent<RunnerController>();
    //     if (runner != null)
    //     {
    //         runner.enabled = false;
    //     }
    // }

    // void RefreshCrowd()
    // {
    //     AdjustUnitCount();
    //     UpdateFormation();
    //     UpdatePopulationUI();
    // }

    // private void UpdatePopulationUI()
    // {
    //     if(populationText == null) return;
    //     populationText.text = population.ToString();
    // }
    // private void AdjustUnitCount()
    // {
    //     while(units.Count < population)
    //     {
    //         GameObject unit = Instantiate(unitPrefab,crowdRoot);
    //         units.Add(unit);
    //     }

    //     while(units.Count > population)
    //     {
    //         int lastIndex = units.Count - 1;
    //         GameObject unit = units[lastIndex];
    //         units.RemoveAt(lastIndex);
    //         Destroy(unit);
    //     }
    // }

    // private void UpdateFormation()
    // {
    //     int count = units.Count;
    //     if(count == 0) return;

    //     int columns = Mathf.CeilToInt(Mathf.Sqrt(count));

    //     for (int i = 0; i < count; i++)
    //     {
    //         int row =
    //             i / columns;

    //         int column =
    //             i % columns;

    //         float x =
    //             (
    //                 column -
    //                 (columns - 1) * 0.5f
    //             ) * spacing;

    //         float z =
    //             -row * spacing;

    //         units[i].transform.localPosition =
    //             new Vector3(
    //                 x,
    //                 0.5f,
    //                 z
    //             );
    //     }    
    // }

    public void AddPopulation(int amount)
    {
        if (!IsServer)
            return;
        SetPopulation(Population+amount);
    }

    public void MultiplyPopulation(int multiplier)
    {
        if (!IsServer)
            return;
        SetPopulation(population.Value*multiplier);
    }

    public void SubtractPopulation(int amount)
    {
        if (!IsServer)
            return;
        SetPopulation(population.Value - amount);
    }

    public void DividePopulation(int divisor)
    {
        if (!IsServer)
            return;
        if(divisor <= 0)
        {
            return;
        }
        else
        {
            SetPopulation(population.Value / divisor);
        }
    }



}
