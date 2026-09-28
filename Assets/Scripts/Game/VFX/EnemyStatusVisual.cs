using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class EnemyStatusVisual : MonoBehaviour
{
    [SerializeField]
    private StatusController
        statusController;


    [SerializeField]
    private Animator
        frozenVfxAnimator;


    private static readonly int
        FrozenHash =
            Animator.StringToHash(
                "Frozen"
            );

    void OnEnable()
    {
        if(statusController != null)
        {
            statusController.FrozenChanged += HandleFrozenChanged;
        }
    }

    void OnDisable()
    {
         if(statusController != null)
        {
            statusController.FrozenChanged -= HandleFrozenChanged;
        }
    }

    private void Start()
    {
        HandleFrozenChanged(
            statusController != null &&
            statusController.IsFrozen
        );
    }
    
    void  HandleFrozenChanged(bool frozen)
    {
         if (frozenVfxAnimator == null)
            return;


        frozenVfxAnimator.SetBool(
            FrozenHash,
            frozen
        );
    }
}
