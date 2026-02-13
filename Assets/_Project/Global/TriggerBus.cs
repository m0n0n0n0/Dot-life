using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerBus : MonoBehaviour
{
    [SerializeField]
    public List<EdgeFalling> edgeTriggers;

    [SerializeField]
    public List<DotDrawing> dotTriggers;

    [SerializeField]
    public List<DotDrawingHandler> taskTriggers;

    public event Action<bool> onInteractingAreaEnteredAndExited;
    public event Action<int> onTaskCompleted;
    public event Action<int> onAnimationStarted;
    public event Action<int, bool> onTaskAreaEnterExit;

    void Start()
    {
        foreach (var e in edgeTriggers)
        {
            e.OnEnteringAndExiting += broadcastEnterExit;
        }
        foreach (var d in dotTriggers)
        {
            d.OnEnteringAndExiting += broadcastEnterExit;
            d.OnInteract += broadcastAnimation;
        }
        for (int i = 0; i < taskTriggers.Count; i++)
        {
            int index = i;
            taskTriggers[i].OnDrawingCompleted += broadcastTask;
            taskTriggers[i].OnPlayerEnterExit += (isEnter) => onTaskAreaEnterExit?.Invoke(index, isEnter);
        }
    }

    void Update()
    {
        
    }

    void broadcastEnterExit(bool isEnter)
    {
        //Debug.Log("broadcastEnterExit");
        onInteractingAreaEnteredAndExited?.Invoke(isEnter);
    }

    void broadcastAnimation(int n)
    {
        //Debug.Log("broadcastAnimation");
        onAnimationStarted?.Invoke(n);
    }

    void broadcastTask(string s) 
    {
        //Debug.Log("broadcastTask");
        int n = -1;
        for (int i = 0; i < taskTriggers.Count; i++)
        {
            var t = taskTriggers[i];
            if (s == t.message)
            {
                n = i;
                break;
            }
        }
        if (n >= 0)
        { 
            onTaskCompleted?.Invoke(n); 
        }
    }
}
