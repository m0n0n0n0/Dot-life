using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractableSugar : MonoBehaviour
{
    public GameObject interactHint;

    public bool playerInRange = false;

    void Start()
    {
        if (interactHint != null)
            interactHint.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        playerInRange = true;
        if (interactHint != null)
        {
            interactHint.SetActive(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        playerInRange = false;
        if (interactHint != null)
        {
            interactHint.SetActive(false);
        }
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
    }
}
