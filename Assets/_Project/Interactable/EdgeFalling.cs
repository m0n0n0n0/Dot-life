using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EdgeFalling : MonoBehaviour
{
    public event Action<bool> OnEnteringAndExiting;
    public event Action<Vector3> OnEdgeFalling;

    private bool playerInRange = false;
    public Vector3 endPoint = Vector3.zero;

    void Start()
    {

    }

    void OnTriggerEnter(Collider other)
    {
        OnEnteringAndExiting?.Invoke(true);
        playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        OnEnteringAndExiting?.Invoke(false);
        playerInRange = false;
    }

    void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            Interact();
        }
    }

    void Interact()
    {
        Debug.Log("Interact with " + gameObject.name);
        OnEdgeFalling?.Invoke(endPoint);
    }
}
