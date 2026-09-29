using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Diagnostics.Tracing;

public class ElementAuraDisplay : MonoBehaviour
{
    [Serializable]
    public class ElementSpriteBinding
    {
        public ElementType element;

        public Sprite sprite;
    }
    [Header("References")]

    [SerializeField]
    private StatusController statusController;

    [SerializeField]
    private Transform displayRoot;


    [Header("Slots")]

    [SerializeField]
    private ElementAuraIconUI[] slots;


    [Header("Sprites")]

    [SerializeField]
    private ElementSpriteBinding[] elementSprites;


    [Header("Layout")]

    [SerializeField]
    private float spacing = 0.45f;


    private readonly Dictionary<
        ElementType,
        Sprite
    > spriteMap = new();


    private readonly Dictionary<
        ElementType,
        ElementAuraIconUI
    > activeIcons = new();


    private Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;

        foreach(ElementSpriteBinding binding in elementSprites)
        {
            if(binding == null || binding.sprite == null)
                continue;

            spriteMap[binding.element] = binding.sprite;
        }

        foreach(ElementAuraIconUI slot in slots)
        {
            if(slot == null) continue;
            slot.HideFinished += HandleSlotHideFinished;
            slot.gameObject.SetActive(false);
        }


    }

    void OnEnable()
    {
        statusController.AuraChanged += HandleAuraChanged;
    }

    void OnDisable()
    {
        statusController.AuraChanged -= HandleAuraChanged;
    }

    void LateUpdate()
    {
        displayRoot.rotation = mainCamera.transform.rotation;
    }

    void HandleAuraChanged(ElementType element,float amount)
    {
        if(amount > 0.001f)
        {
            ShowElement(element);
        }
        else
        {
            HideElement(element);
        }
    }

    void ShowElement(ElementType element)
    {
        if(activeIcons.TryGetValue(element,out ElementAuraIconUI existing))
        {
            return;
        }
        ElementAuraIconUI freeSlot = FindFreeSlot();

        if (
            !spriteMap.TryGetValue(
                element,
                out Sprite sprite
            )
        )
        {
            Debug.LogWarning(
                $"没有配置 {element} 的Sprite"
            );

            return;
        }
        freeSlot.SetElement(element,sprite);

        activeIcons.Add(element,freeSlot);

        RefreshLayout();
        freeSlot.Show();
    }


    void HideElement(ElementType element)
    {
        if(!activeIcons.TryGetValue(element,out ElementAuraIconUI slot))
        {
            return;
        }

        slot.Hide();
    }
    private void HandleSlotHideFinished(
        ElementAuraIconUI slot,
        ElementType element
    )
    {
        if (
            activeIcons.TryGetValue(
                element,
                out ElementAuraIconUI currentSlot
            )
        )
        {
            // 确保这个元素现在仍然对应这个Slot
            if (currentSlot == slot)
            {
                activeIcons.Remove(
                    element
                );
            }
        }


        RefreshLayout();
    }
    ElementAuraIconUI FindFreeSlot()
    {
        foreach(ElementAuraIconUI slot in slots)
        {
            if(slot == null) continue;
            if (!slot.IsOccupied)
            {
                return slot;
            }
        }
        return null;
    }

    private void RefreshLayout()
    {
        List<ElementAuraIconUI>
            visibleIcons =
                new List<ElementAuraIconUI>();


        foreach (
            KeyValuePair<
                ElementType,
                ElementAuraIconUI
            > pair in activeIcons
        )
        {
            if (pair.Value != null)
            {
                visibleIcons.Add(
                    pair.Value
                );
            }
        }


        int count =
            visibleIcons.Count;


        for (
            int i = 0;
            i < count;
            i++
        )
        {
            float x =
                (
                    i -
                    (count - 1) * 0.5f
                )
                *
                spacing;


            visibleIcons[i]
                .transform
                .localPosition =
                    new Vector3(
                        x,
                        0f,
                        0f
                    );
        }
    }
}
