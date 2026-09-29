using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CombatTextPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;
    }

    public void Setup(string content,Color color)
    {
        text.text = content;
        text.color = color;
    }

    void LateUpdate()
    {
        transform.rotation = mainCamera.transform.rotation;
    }

    public void Finish()
    {
        Destroy(gameObject);
    }
}
