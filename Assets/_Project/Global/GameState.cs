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

    public bool isWin = false;

    void Start()
    {
        trigger.onInteractingAreaEnteredAndExited += UpdateInInteractingArea;
        trigger.onTaskCompleted += UpdateTasks;
    }

    void Update()
    {
        
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
        }
    }
}
