using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestController : MonoBehaviour
{
    [Header("Items")]

    [SerializeField]
    private ItemManager itemManager;

    [SerializeField]
    private UpgradeData heavyBullet;
    [SerializeField]
    private UpgradeData Bounce;

    [SerializeField]
    private UpgradeData iceCore;
    [SerializeField]
    private UpgradeData explosion;
    [SerializeField] 
    private UpgradeData raincore;
    [SerializeField]
    private UpgradeData earthCore;
    [SerializeField]
    private UpgradeData thunderStrike;
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            itemManager.AcquireServer(
                heavyBullet
            );
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            itemManager.AcquireServer(
                thunderStrike
            );
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            itemManager.AcquireServer(
                raincore
            );
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            itemManager.AcquireServer(
                earthCore
            );
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            itemManager.AcquireServer(
                iceCore
            );
        }
        if(Input.GetKeyDown(KeyCode.R))
        {
            itemManager.AcquireServer(explosion);
        }
        if (
            Input.GetKeyDown(
                KeyCode.E
            )
        )
        {
            itemManager.AcquireServer(
                Bounce
            );
        }
    }
}
