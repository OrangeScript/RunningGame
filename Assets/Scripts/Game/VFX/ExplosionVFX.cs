using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class ExplosionVFX : MonoBehaviour
{
    [SerializeField] private float duration = 0.2f;
    [SerializeField] private float startScale = 0.1f;
    private float elapsedTime;
    private float targetScale;

    public void Initialize(float radius)
    {
        targetScale = radius * 2f;
        transform.localScale = Vector3.one * startScale;
    }


    void Update()
    {
        elapsedTime += Time.deltaTime;
        float t = elapsedTime / duration;
        t = Mathf.Clamp01(t);

        transform.localScale = Vector3.Lerp(
            Vector3.one*startScale,
            Vector3.one*targetScale,
            t
            );
        if(elapsedTime >= duration)
        {
            Destroy(gameObject);
        }
    }
}
