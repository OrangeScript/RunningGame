using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    [SerializeField] private Vector3 offset = new Vector3(0f,7f,-9f);
    [SerializeField] private float followSpeed = 8f;

    internal void SetTarget(Transform transform)
    {
        target = transform;
    }

    private void LateUpdate()
    {
        if(target == null)
        {
            return;
        }

        Vector3 targetPosition = target.position + offset;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed*Time.deltaTime
        );
        // transform.LookAt(target.position+Vector3.up);
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
