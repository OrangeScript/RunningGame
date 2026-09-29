using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElementAuraIconUI : MonoBehaviour
{
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] Animator animator;

    static readonly int ShowHash = Animator.StringToHash("Show");
    static readonly int HideHash = Animator.StringToHash("Hide");
    static readonly int PulseHash = Animator.StringToHash("Pulse");


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
    }


    public void Show()
    {
        gameObject.SetActive(true);

        animator.ResetTrigger(HideHash);
        animator.SetTrigger(ShowHash);
    }


    public void Pulse()
    {
        if (!gameObject.activeSelf)
        {
            Show();
            return;
        }

        animator.SetTrigger(PulseHash);
    }


    public void Hide()
    {
        if (!gameObject.activeSelf)
            return;

        animator.ResetTrigger(ShowHash);
        animator.SetTrigger(HideHash);
    }


    public void FinishHide()
    {
        CurrentElement =
            ElementType.None;

        spriteRenderer.sprite =
            null;

        gameObject.SetActive(false);
    }
}
