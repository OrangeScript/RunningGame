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

    [Header("UpgradePool")]
    [SerializeField] List<UpgradeData> upgradePool;
    private bool isOpen;
    private Action<UpgradeData> OnUpgradeSelected;

    private void Awake()
    {
        panel.SetActive(false);
    }


    public void Show(Action<UpgradeData> callback)
    {
        if(isOpen) return;
        isOpen = true;
        OnUpgradeSelected = callback;
        Time.timeScale = 0f;
        panel.SetActive(true);

        List<UpgradeData> options = GetRandomOptions(3);

        for(int i = 0; i< cards.Length; i++)
        {
            cards[i].Setup(options[i],HandleSelected);
        }
    }

    private void HandleSelected(UpgradeData selected)
    {
        OnUpgradeSelected?.Invoke(selected);
        panel.SetActive(false);
        Time.timeScale = 1f;
        isOpen = false;
        OnUpgradeSelected = null;
    }

    private List<UpgradeData> GetRandomOptions(int count)
    {
        List<UpgradeData> tempPool = new List<UpgradeData>(upgradePool);

        List<UpgradeData> result = new List<UpgradeData>();

        for(int i = 0; i< count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0,tempPool.Count);
            result.Add(tempPool[randomIndex]);
            tempPool.RemoveAt(randomIndex);
        }
        return result;
    }
}
