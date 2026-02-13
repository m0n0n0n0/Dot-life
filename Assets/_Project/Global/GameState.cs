using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameState : MonoBehaviour
{
    [SerializeField]
    private TriggerBus trigger;

    public bool isInInteractingArea;
    public List<bool> taskCompleted;

    private int taskCount = 0;

    public bool isGameRunning = false;
    public bool isWin = false;

    public event Action OnGameStart;
    public event Action OnGameOver;

    void Start()
    {
        trigger.onInteractingAreaEnteredAndExited += UpdateInInteractingArea;
        trigger.onTaskCompleted += UpdateTasks;
    }

    void Update()
    {
        if (!isGameRunning && Input.anyKeyDown)
        {
            OnGameStart?.Invoke();
            isGameRunning = true;
        }
    }

    void UpdateInInteractingArea(bool isEnter)
    {
        isInInteractingArea = isEnter;
    }

    void UpdateTasks(int n)
    {
        taskCompleted[n] = true;
        taskCount++;
        if (taskCount == taskCompleted.Count)
        {
            isWin = true;
            Debug.Log("Win");
            isGameRunning = false;
            OnGameOver.Invoke();
        }
    }
}
