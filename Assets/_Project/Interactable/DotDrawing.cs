using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DotDrawing : MonoBehaviour
{
    public bool isPressInteract = false;

    public int dotIndex = 0;
    public int texIndex = 0;

    public int characterAction = -1;

    public event Action<bool> OnEnteringAndExiting;
    public event Action<int> OnInteract;
    public event Action<int, int> OnDrawing;

    private bool playerInRange = false;

    void Start()
    {
        
    }

    void Update()
    {
        if (isPressInteract && playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            Interact();
        }
    }

    void Interact()
    {
        Debug.Log("Interact with " + gameObject.name);
        OnInteract?.Invoke(characterAction);
        OnDrawing?.Invoke(dotIndex, texIndex);
    }

    void OnTriggerEnter(Collider other)
    {
        playerInRange = true;
        OnEnteringAndExiting?.Invoke(true);
        if (!isPressInteract)
        { 
            OnDrawing?.Invoke(dotIndex, texIndex); 
        }
    }

    void OnTriggerExit(Collider other)
    {
        OnEnteringAndExiting?.Invoke(false);
        playerInRange = false;
    }
}
