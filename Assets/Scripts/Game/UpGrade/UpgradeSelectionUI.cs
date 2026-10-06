using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class UpgradeSelectionUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private UpgradeCardUI[] cards;

    // [Header("UpgradePool")]
    // [SerializeField] List<UpgradeData> upgradePool;
    private bool isOpen;
    private Action<UpgradeData> OnSelected;

    private void Awake()
    {
        panel.SetActive(false);
    }


    public void Show(UpgradeData[] options,Action<UpgradeData> callback)
    {
        if(isOpen) return;
        isOpen = true;
        OnSelected = callback;
        panel.SetActive(true);
        int count =
            Mathf.Min(
                cards.Length,
                options.Length
            );
        for(int i = 0; i< count; i++)
        {
            cards[i].gameObject.SetActive(true);
            cards[i].Setup(options[i],HandleSelected);
        }
    }

    private void HandleSelected(UpgradeData selected)
    {
        if(!isOpen) return;
        isOpen = false;
        panel.SetActive(false);

        Action<UpgradeData>
            callback =
                OnSelected;


        OnSelected =
            null;


        callback?.Invoke(
            selected
        );
    }

    // private List<UpgradeData> GetRandomOptions(int count)
    // {
    //     List<UpgradeData> tempPool = new List<UpgradeData>(upgradePool);

    //     List<UpgradeData> result = new List<UpgradeData>();

    //     for(int i = 0; i< count; i++)
    //     {
    //         int randomIndex = UnityEngine.Random.Range(0,tempPool.Count);
    //         result.Add(tempPool[randomIndex]);
    //         tempPool.RemoveAt(randomIndex);
    //     }
    //     return result;
    // }
}
