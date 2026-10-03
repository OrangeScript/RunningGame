using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CrowdTestController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private CrowdManager crowdManager;
    [SerializeField]
    private ItemManager itemManager;

    [Header("Test Upgrade")]
    [SerializeField]
    private UpgradeData giantUpgrade;
    private bool giantAcquired;


    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if(keyboard == null) return;

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            crowdManager.AddPopulation(10);
        }
        if (
            keyboard
                .digit2Key
                .wasPressedThisFrame
        )
        {
            crowdManager.MultiplyPopulation(
                2
            );
        }


        // 3 → -10
        if (
            keyboard
                .digit3Key
                .wasPressedThisFrame
        )
        {
            crowdManager.SubtractPopulation(
                10
            );
        }


        // 4 → 200人口
        if (
            keyboard
                .digit4Key
                .wasPressedThisFrame
        )
        {
            crowdManager.SetPopulation(
                200
            );
        }


        // 0 → 清空
        if (
            keyboard
                .digit0Key
                .wasPressedThisFrame
        )
        {
            crowdManager.SetPopulation(
                0
            );
        }


        // G → 获得Giant
        if (
            keyboard
                .gKey
                .wasPressedThisFrame
        )
        {
            AcquireGiant();
        }
    }

    void AcquireGiant()
    {

        if(giantAcquired) return;
        itemManager.Acquire(giantUpgrade);
        giantAcquired = true;
    }

}
