using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

public class PopulationUI : MonoBehaviour
{
    [SerializeField]
    private CrowdManager crowdManager;

    [SerializeField]
    private TMP_Text populationText;


    private void OnEnable()
    {
        if (crowdManager == null)
            return;

        crowdManager.PopulationChanged +=
            HandlePopulationChanged;
    }


    private void Start()
    {
        if (crowdManager == null)
            return;

        // 很重要：
        // 初始 Population 不一定触发过事件
        HandlePopulationChanged(
            crowdManager.Population
        );
    }


    private void OnDisable()
    {
        if (crowdManager == null)
            return;

        crowdManager.PopulationChanged -=
            HandlePopulationChanged;
    }


    private void HandlePopulationChanged(
        int population
    )
    {
        if (populationText == null)
            return;

        populationText.text =
            population.ToString();
    }

    internal void Bind(CrowdManager crowdManager)
    {
        this.crowdManager.PopulationChanged -=HandlePopulationChanged;
        this.crowdManager = crowdManager;
        crowdManager.PopulationChanged += HandlePopulationChanged;
        HandlePopulationChanged(crowdManager.Population);
    }
    // Start is called before the first frame update

}
