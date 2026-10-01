using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EdgeFalling : MonoBehaviour
{
    public event Action<bool> OnEnteringAndExiting;
    public event Action<Vector3> OnEdgeFalling;

    [SerializeField]
    private GameState gameState;

    [SerializeField]
    private int requiredTaskIndex = 0;

    private bool playerInRange = false;
    public Vector3 endPoint = Vector3.zero;

    void Start()
    {
        if (gameState == null)
        {
            gameState = FindObjectOfType<GameState>();
        }
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
        if (playerInRange && CanFall() && Input.GetKeyDown(KeyCode.E))
        {
            Interact();
        }
    }

    public bool CanFall()
    {
        return gameState != null
            && gameState.taskCompleted != null
            && requiredTaskIndex >= 0
            && requiredTaskIndex < gameState.taskCompleted.Count
            && gameState.taskCompleted[requiredTaskIndex];
    }

    void Interact()
    {
        Debug.Log("Interact with " + gameObject.name);
        OnEdgeFalling?.Invoke(endPoint);
    }
}
