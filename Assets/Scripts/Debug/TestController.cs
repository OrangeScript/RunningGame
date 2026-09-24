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
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            itemManager.Acquire(
                heavyBullet
            );
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            itemManager.Acquire(
                iceCore
            );
        }
        if(Input.GetKeyDown(KeyCode.R))
        {
            itemManager.Acquire(explosion);
        }
        if (
            Input.GetKeyDown(
                KeyCode.E
            )
        )
        {
            itemManager.Acquire(
                Bounce
            );
        }
    }
}
