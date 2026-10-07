using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpgradeUITest : MonoBehaviour
{
    [SerializeField]
    private UpgradeSelectionUI selectionUI;


    private void Update()
    {
        // if (Input.GetKeyDown(
        //     KeyCode.U
        // ))
        // {
        //     selectionUI.Show(
        //         HandleUpgrade
        //     );
        // }
    }


    private void HandleUpgrade(
        UpgradeData upgrade
    )
    {
        Debug.Log(
            $"选择了：{upgrade.UpgradeName}"
        );
    }
}