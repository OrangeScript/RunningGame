using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Serialization;
using UnityEngine;

public class ElementAuraIconUI : MonoBehaviour
{
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] Animator animator;

    static readonly int ShowHash = Animator.StringToHash("Show");
    static readonly int HideHash = Animator.StringToHash("Hide");
    static readonly int PulseHash = Animator.StringToHash("Pulse");

    public bool IsHiding
    {
        get;
        private set;
    }


    // Hide真正完成后通知Display
    public event Action<
        ElementAuraIconUI,
        ElementType
    > HideFinished;
     public ElementType CurrentElement
    {
        get;
        private set;
    } = ElementType.None;


    public bool IsOccupied =>
        CurrentElement != ElementType.None;


    public void SetElement(
        ElementType element,
        Sprite sprite
    )
    {
        CurrentElement = element;

        spriteRenderer.sprite = sprite;
        IsHiding = false;
    }


    public void Show()
    {
        gameObject.SetActive(true);
        IsHiding = false;
        animator.ResetTrigger(HideHash);
        animator.SetTrigger(ShowHash);
    }


    // public void Pulse()
    // {
    //     if (!gameObject.activeSelf)
    //     {
    //         Show();
    //         return;
    //     }

    //     animator.SetTrigger(PulseHash);
    // }


    public void Hide()
    {
        if (!gameObject.activeSelf)
            return;
        if(IsHiding) return;
        IsHiding = true;
        animator.ResetTrigger(ShowHash);
        animator.SetTrigger(HideHash);
    }


    public void FinishHide()
    {
        // 如果Hide过程中又重新Show了，
        // 老的Animation Event到这里时不能真的隐藏
        if (!IsHiding)
            return;


        ElementType hiddenElement =
            CurrentElement;


        CurrentElement =
            ElementType.None;


        spriteRenderer.sprite =
            null;


        IsHiding =
            false;


        gameObject.SetActive(
            false
        );


        HideFinished?.Invoke(
            this,
            hiddenElement
        );
    }
}
