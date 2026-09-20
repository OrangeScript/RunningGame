
using UnityEngine;
using TMPro;
public enum GateOperation
{
    Add,
    Subtract,
    Multiply,
    Divide
}

public class Gate : MonoBehaviour
{
    [Header("Gate")]
    [SerializeField] private GateOperation operation;

    [SerializeField] private int value;

    [Header("Visual")]
    [SerializeField] private TMP_Text gateText;
    private bool hasTriggered;

    void Start()
    {
        UpdateText();
    }

    void OnTriggerEnter(Collider other)
    {
        if(hasTriggered) return;
        CrowdManager crowd = other.GetComponent<CrowdManager>();
        if(crowd == null)
        {
            return;
        }
        ApplyGate(crowd);
        hasTriggered = true;
    }

    private void ApplyGate(CrowdManager crowd)
    {
        switch (operation)
        {
            case GateOperation.Add:
                crowd.AddPopulation(value);
                break;

            case GateOperation.Divide:
                crowd.DividePopulation(value);
                break;

            case GateOperation.Multiply:
                crowd.MultiplyPopulation(value);
                break;

            case GateOperation.Subtract:
                crowd.SubtractPopulation(value);
                break;

        }
    }
    
    void UpdateText()
    {
        if(gateText == null)    return;

        string symbol = operation switch
        {
            GateOperation.Add => "+",
            GateOperation.Subtract => "-",
            GateOperation.Divide => "/",
            GateOperation.Multiply => "*",
            _ => ""
        };

        gateText.text = $"{symbol}{value}";
    }
}
