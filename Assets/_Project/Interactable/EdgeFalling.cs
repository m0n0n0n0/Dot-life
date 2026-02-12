using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EdgeFalling : MonoBehaviour
{
    public event Action OnInteract;
    public event Action<Vector3> OnEdgeFalling;

    public bool playerInRange = false;
    public Vector3 endPoint = Vector3.zero;

    void Start()
    {

    }

    void OnTriggerEnter(Collider other)
    {
        playerInRange = true;
        OnInteract?.Invoke();
    }

    void OnTriggerExit(Collider other)
    {
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
