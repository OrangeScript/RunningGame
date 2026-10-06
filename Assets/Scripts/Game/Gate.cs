
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using Unity.Netcode;
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
    // private bool hasTriggered;

    private readonly HashSet<ulong> triggeredPlayers = new HashSet<ulong>();

    void Start()
    {
        UpdateText();
    }

    void OnTriggerEnter(Collider other)
    {
        if(!NetworkManager.Singleton.IsServer) return;

        CrowdManager crowd = other.GetComponentInParent<CrowdManager>();
        if(crowd == null)
        {
            return;
        }

        ulong num = crowd.OwnerClientId;
        if(!triggeredPlayers.Add(num))return;
        ApplyGate(crowd);
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
