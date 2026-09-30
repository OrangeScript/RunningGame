using System.Collections.Generic;
using UnityEngine;


public enum CrowdVisualMode
{
    Normal,
    Giant
}


public class CrowdVisualizer : MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private CrowdManager crowdManager;

    [SerializeField]
    private Transform crowdRoot;

    [SerializeField]
    private GameObject unitPrefab;


    [Header("Normal Mode")]

    [SerializeField]
    [Min(1)]
    private int maxVisualUnits = 80;

    [SerializeField]
    private float spacing = 0.8f;

    [SerializeField]
    private float normalUnitY = 0.5f;


    [Header("Giant Mode")]

    [SerializeField]
    private float giantScaleMultiplier = 4f;

    [SerializeField]
    private float giantUnitY = 1.5f;


    private readonly List<GameObject>
        units = new();


    private Vector3 baseUnitScale =
        Vector3.one;


    public CrowdVisualMode Mode
    {
        get;
        private set;
    } = CrowdVisualMode.Normal;


    public int VisualUnitCount =>
        units.Count;


    private void Awake()
    {
        if (crowdManager == null)
        {
            crowdManager =
                GetComponent<CrowdManager>();
        }


        if (unitPrefab != null)
        {
            baseUnitScale =
                unitPrefab
                    .transform
                    .localScale;
        }
    }


    private void OnEnable()
    {
        if (crowdManager != null)
        {
            crowdManager
                .PopulationChanged +=
                HandlePopulationChanged;
        }
    }


    private void Start()
    {
        RefreshVisuals();
    }


    private void OnDisable()
    {
        if (crowdManager != null)
        {
            crowdManager
                .PopulationChanged -=
                HandlePopulationChanged;
        }
    }


    private void HandlePopulationChanged(
        int population
    )
    {
        RefreshVisuals();
    }


    // ========================================
    // Visual Mode
    // ========================================

    public void SetMode(
        CrowdVisualMode mode
    )
    {
        if (Mode == mode)
            return;


        Mode =
            mode;


        RefreshVisuals();
    }


    public void SetGiantMode(
        bool enabled
    )
    {
        SetMode(
            enabled
                ? CrowdVisualMode.Giant
                : CrowdVisualMode.Normal
        );
    }


    // ========================================
    // 对外提供视觉单位
    // ========================================

    public Transform GetUnitTransform(
        int index
    )
    {
        if (
            index < 0 ||
            index >= units.Count
        )
        {
            return null;
        }


        return units[
            index
        ].transform;
    }


    // ========================================
    // 刷新
    // ========================================

    public void RefreshVisuals()
    {
        if (
            crowdManager == null ||
            crowdRoot == null ||
            unitPrefab == null
        )
        {
            return;
        }


        int targetVisualCount =
            CalculateTargetVisualCount();


        SetVisualUnitCount(
            targetVisualCount
        );


        ApplyAppearance();


        LayoutUnits();
    }


    private int CalculateTargetVisualCount()
    {
        int population =
            crowdManager.Population;


        if (population <= 0)
        {
            return 0;
        }


        if (
            Mode ==
            CrowdVisualMode.Giant
        )
        {
            return 1;
        }


        return Mathf.Min(
            population,
            maxVisualUnits
        );
    }


    // ========================================
    // Spawn / Remove
    // ========================================

    private void SetVisualUnitCount(
        int targetCount
    )
    {
        while (
            units.Count <
            targetCount
        )
        {
            GameObject unit =
                Instantiate(
                    unitPrefab,
                    crowdRoot
                );


            units.Add(
                unit
            );
        }


        while (
            units.Count >
            targetCount
        )
        {
            int lastIndex =
                units.Count - 1;


            GameObject unit =
                units[lastIndex];


            units.RemoveAt(
                lastIndex
            );


            Destroy(
                unit
            );
        }
    }


    // ========================================
    // Appearance
    // ========================================

    private void ApplyAppearance()
    {
        if (
            Mode ==
            CrowdVisualMode.Giant
        )
        {
            if (units.Count == 0)
                return;


            units[0]
                .transform
                .localScale =
                    baseUnitScale *
                    giantScaleMultiplier;


            return;
        }


        foreach (
            GameObject unit
            in units
        )
        {
            unit.transform.localScale =
                baseUnitScale;
        }
    }


    // ========================================
    // Layout
    // ========================================

    private void LayoutUnits()
    {
        if (units.Count == 0)
            return;


        if (
            Mode ==
            CrowdVisualMode.Giant
        )
        {
            units[0]
                .transform
                .localPosition =
                    new Vector3(
                        0f,
                        giantUnitY,
                        0f
                    );


            return;
        }


        int count =
            units.Count;


        int columns =
            Mathf.CeilToInt(
                Mathf.Sqrt(
                    count
                )
            );


        for (
            int i = 0;
            i < count;
            i++
        )
        {
            int row =
                i / columns;

            int column =
                i % columns;


            float x =
                (
                    column -
                    (columns - 1) *
                    0.5f
                )
                *
                spacing;


            float z =
                -row *
                spacing;


            units[i]
                .transform
                .localPosition =
                    new Vector3(
                        x,
                        normalUnitY,
                        z
                    );
        }
    }
}