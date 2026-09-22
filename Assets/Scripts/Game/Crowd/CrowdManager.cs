using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class CrowdManager : MonoBehaviour
{
    
    [Header("Crowd")]
    [SerializeField] private GameObject unitPrefab;
    [SerializeField] private Transform crowdRoot;

    [SerializeField] private int population = 5;
    [SerializeField] private TMP_Text populationText;

    [Header("Formation")]
    [SerializeField] private float spacing = 0.65f;
    private readonly List<GameObject> units = new List<GameObject>();
    public int UnitCount => units.Count;
    public Transform GetUnitTransform(int index)
    {
        if(index < 0 || index >= units.Count)
        {
            return null;
        }

        return units[index].transform;
    }

    public int Population => population;
    void Start()
    {
        SetPopulation(population);
    }

    public void DamagePopulation(int damage)
    {
        if(damage <= 0)
        {
            return;
        }
        SetPopulation(population - damage);
    }

    public void SetPopulation(int targetPopulation)
    {
        targetPopulation = Mathf.Max(0,targetPopulation);
        population = targetPopulation;
        RefreshCrowd();
        if(population <= 0)
        {
            OnCrowdDead();
        }
    }

    public void OnCrowdDead()
    {
        Debug.Log("Game Over!");
        RunnerController runner = GetComponent<RunnerController>();
        if (runner != null)
        {
            runner.enabled = false;
        }
    }

    void RefreshCrowd()
    {
        AdjustUnitCount();
        UpdateFormation();
        UpdatePopulationUI();
    }

    private void UpdatePopulationUI()
    {
        if(populationText == null) return;
        populationText.text = population.ToString();
    }
    private void AdjustUnitCount()
    {
        while(units.Count < population)
        {
            GameObject unit = Instantiate(unitPrefab,crowdRoot);
            units.Add(unit);
        }

        while(units.Count > population)
        {
            int lastIndex = units.Count - 1;
            GameObject unit = units[lastIndex];
            units.RemoveAt(lastIndex);
            Destroy(unit);
        }
    }

    private void UpdateFormation()
    {
        int count = units.Count;
        if(count == 0) return;

        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));

        for (int i = 0; i < count; i++)
        {
            int row =
                i / columns;

            int column =
                i % columns;

            float x =
                (
                    column -
                    (columns - 1) * 0.5f
                ) * spacing;

            float z =
                -row * spacing;

            units[i].transform.localPosition =
                new Vector3(
                    x,
                    0.5f,
                    z
                );
        }    
    }

    public void AddPopulation(int amount)
    {
        SetPopulation(population+amount);
    }

    public void MultiplyPopulation(int multiplier)
    {
        SetPopulation(population*multiplier);
    }

    public void SubtractPopulation(int amount)
    {
        SetPopulation(population - amount);
    }

    public void DividePopulation(int divisor)
    {
        if(divisor <= 0)
        {
            return;
        }
        else
        {
            SetPopulation(population / divisor);
        }
    }



}
