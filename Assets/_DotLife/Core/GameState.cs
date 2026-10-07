using System;
using System.Collections.Generic;
using UnityEngine;

public enum DemoTaskId { Unassigned = -1, Diary = 0, YuwenBook = 1, MathBook = 2, Photo = 3 }
public enum GameSessionPhase { Waiting, Playing, Finished }

public class GameState : MonoBehaviour
{
    [SerializeField] private TriggerBus trigger;
    public bool isInInteractingArea;
    public List<bool> taskCompleted = new List<bool>();
    public bool isGameRunning;
    public bool isWin;

    public GameSessionPhase Phase => isWin ? GameSessionPhase.Finished
        : isGameRunning ? GameSessionPhase.Playing : GameSessionPhase.Waiting;
    public event Action OnGameStart;
    public event Action OnGameOver;
    public event Action<int> OnTaskStateChanged;

    void Awake()
    {
        if (trigger == null) trigger = GetComponent<TriggerBus>();
        if (taskCompleted == null) taskCompleted = new List<bool>();
    }

    void OnEnable()
    {
        if (trigger == null) return;
        trigger.onInteractingAreaEnteredAndExited += UpdateInInteractingArea;
        trigger.onTaskCompleted += UpdateTasks;
        isInInteractingArea = trigger.HasPlayerInRange;
        foreach (DotDrawingHandler task in trigger.taskTriggers)
            if (task != null && task.IsComplete) UpdateTasks((int)task.TaskId);
    }

    public bool TryStartGame()
    {
        if (!isActiveAndEnabled || Phase != GameSessionPhase.Waiting) return false;
        isGameRunning = true;
        OnGameStart?.Invoke();
        return true;
    }

    public bool IsTaskComplete(DemoTaskId id)
    {
        int index = (int)id;
        return index >= 0 && index < taskCompleted.Count && taskCompleted[index];
    }

    void UpdateInInteractingArea(bool entered) => isInInteractingArea = entered;

    public void UpdateTasks(int index)
    {
        if (Phase != GameSessionPhase.Playing || index < 0
            || index >= taskCompleted.Count || taskCompleted[index]) return;
        taskCompleted[index] = true;
        OnTaskStateChanged?.Invoke(index);
        foreach (bool completed in taskCompleted)
            if (!completed) return;
        isWin = true;
        isGameRunning = false;
        OnGameOver?.Invoke();
    }

    void OnDisable()
    {
        if (trigger == null) return;
        trigger.onInteractingAreaEnteredAndExited -= UpdateInInteractingArea;
        trigger.onTaskCompleted -= UpdateTasks;
    }
}
