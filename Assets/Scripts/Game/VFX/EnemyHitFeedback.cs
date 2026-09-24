using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyHitFeedback : MonoBehaviour
{
    [Header("References")]

    [SerializeField] private Transform visualRoot;
    [SerializeField] private Renderer targetRenderer;

    [Header("Hit Feedback")]

    [SerializeField] private float hitDuration = 0.1f;
    [SerializeField] private float scaleMultiplier = 0.85f;

    [SerializeField] private Color hitColor = Color.white;

    Vector3 originalScale;
    Color originalColor;
    Coroutine feedbackCoroutine;

    void Awake()
    {
        originalScale = visualRoot.localScale;
        originalColor = targetRenderer.material.color;
    }

    public void Play()
    {
        if(feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
        }
        feedbackCoroutine = StartCoroutine(PlayFeedBack());
    }

    private IEnumerator PlayFeedBack()
    {
                if (visualRoot != null)
        {
            visualRoot.localScale =
                originalScale *
                scaleMultiplier;
        }


        if (targetRenderer != null)
        {
            targetRenderer.material.color =
                hitColor;
        }


        yield return new WaitForSeconds(
            hitDuration
        );


        // ----------------
        // 恢复
        // ----------------

        if (visualRoot != null)
        {
            visualRoot.localScale =
                originalScale;
        }


        if (targetRenderer != null)
        {
            targetRenderer.material.color =
                originalColor;
        }


        feedbackCoroutine = null;
    }
}
