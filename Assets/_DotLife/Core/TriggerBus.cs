using System;
using System.Collections.Generic;
using UnityEngine;

public class TriggerBus : MonoBehaviour
{
    public List<EdgeFalling> edgeTriggers = new List<EdgeFalling>();
    public List<InkWell> inkWells = new List<InkWell>();
    public List<DotDrawing> dotTriggers = new List<DotDrawing>();
    public List<DotDrawingHandler> taskTriggers = new List<DotDrawingHandler>();
    public event Action<bool> onInteractingAreaEnteredAndExited;
    public event Action<int> onTaskCompleted;
    public event Action<int, bool> onTaskAreaEnterExit;
    public bool HasPlayerInRange => occupiedTargets.Count > 0;
    private readonly HashSet<InteractionTarget> occupiedTargets = new HashSet<InteractionTarget>();
    private readonly Dictionary<InteractionTarget, Action<bool>> rangeHandlers = new Dictionary<InteractionTarget, Action<bool>>();
    private readonly Dictionary<DotDrawingHandler, Action<bool>> taskRangeHandlers = new Dictionary<DotDrawingHandler, Action<bool>>();

    void OnEnable()
    {
        foreach (EdgeFalling edge in edgeTriggers) BindRange(edge);
        foreach (InkWell well in inkWells) BindRange(well);
        foreach (DotDrawing dot in dotTriggers) BindRange(dot);
        foreach (DotDrawingHandler task in taskTriggers)
        {
            if (task == null || taskRangeHandlers.ContainsKey(task)) continue;
            DotDrawingHandler source = task;
            Action<bool> handler = entered => onTaskAreaEnterExit?.Invoke((int)source.TaskId, entered);
            taskRangeHandlers.Add(task, handler);
            task.OnPlayerEnterExit += handler;
            task.OnTaskCompleted += BroadcastTask;
            if (task.IsComplete) BroadcastTask(task);
        }
        if (HasPlayerInRange) onInteractingAreaEnteredAndExited?.Invoke(true);
    }
    private void BindRange(InteractionTarget target)
    {
        if (target == null || rangeHandlers.ContainsKey(target)) return;
        InteractionTarget source = target;
        Action<bool> handler = entered => UpdateRange(source, entered);
        rangeHandlers.Add(target, handler);
        target.OnEnteringAndExiting += handler;
        if (target.IsPlayerInRange) occupiedTargets.Add(target);
    }
    private void UpdateRange(InteractionTarget target, bool entered)
    {
        bool wasInside = HasPlayerInRange;
        if (entered) occupiedTargets.Add(target); else occupiedTargets.Remove(target);
        if (wasInside != HasPlayerInRange) onInteractingAreaEnteredAndExited?.Invoke(HasPlayerInRange);
    }
    private void BroadcastTask(DotDrawingHandler task) => onTaskCompleted?.Invoke((int)task.TaskId);
    void OnDisable()
    {
        foreach (var subscription in rangeHandlers)
            if (subscription.Key != null) subscription.Key.OnEnteringAndExiting -= subscription.Value;
        foreach (var subscription in taskRangeHandlers)
        {
            if (subscription.Key == null) continue;
            subscription.Key.OnPlayerEnterExit -= subscription.Value;
            subscription.Key.OnTaskCompleted -= BroadcastTask;
        }
        rangeHandlers.Clear();
        taskRangeHandlers.Clear();
        bool wasInside = HasPlayerInRange;
        occupiedTargets.Clear();
        if (wasInside) onInteractingAreaEnteredAndExited?.Invoke(false);
    }
}
