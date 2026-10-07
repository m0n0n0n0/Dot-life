using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DotDrawingHandler : MonoBehaviour
{
    [SerializeField] private List<DotDrawing> dotTriggers = new List<DotDrawing>();
    [SerializeField] private List<Texture> frames = new List<Texture>();
    [SerializeField] private DemoTaskId taskId = DemoTaskId.Unassigned;
    public string message = "";
    [Min(0f)] public float frameDuration = 0.2f;
    public event Action<string> OnDrawingCompleted;
    public event Action<DotDrawingHandler> OnTaskCompleted;
    public event Action<bool> OnPlayerEnterExit;
    public event Action OnProgressChanged;
    public DemoTaskId TaskId => taskId;
    public int AcceptedSteps => curDotIndex;
    public int StepCount => dotTriggers.Count;
    public bool IsComplete { get; private set; }
    public bool IsConfigured { get; private set; }
    public bool HasPlayerInRange => occupiedDots.Count > 0;
    public IReadOnlyList<DotDrawing> Dots => dotTriggers;

    private readonly HashSet<DotDrawing> occupiedDots = new HashSet<DotDrawing>();
    private readonly Dictionary<DotDrawing, Action<bool>> rangeHandlers = new Dictionary<DotDrawing, Action<bool>>();
    private TextureSurface surface;
    private TransitionHandler transition;
    private Coroutine animationCoroutine;
    private int curDotIndex;
    private int curTexIndex;
    private int targetTexIndex;
    private bool drawingComplete;

    void Awake()
    {
        surface = GetComponent<TextureSurface>();
        if (surface == null && GetComponent<Renderer>() != null) surface = gameObject.AddComponent<TextureSurface>();
        transition = GetComponent<TransitionHandler>();
        IsConfigured = ValidateConfiguration();
        if (dotTriggers == null) return;
        foreach (DotDrawing dot in dotTriggers)
            if (dot != null) dot.Bind(this);
    }

    public bool ValidateConfiguration()
    {
        bool valid = taskId != DemoTaskId.Unassigned && dotTriggers != null
            && dotTriggers.Count > 0 && frames != null && frames.Count > 0 && surface != null && surface.IsReady;
        if (valid)
        {
            var ordered = new DotDrawing[dotTriggers.Count];
            foreach (DotDrawing dot in dotTriggers)
            {
                if (dot == null || dot.dotIndex < 0 || dot.dotIndex >= ordered.Length
                    || ordered[dot.dotIndex] != null) { valid = false; break; }
                ordered[dot.dotIndex] = dot;
            }
            int previous = 0;
            foreach (DotDrawing dot in ordered)
            {
                if (dot == null || dot.texIndex <= previous || dot.texIndex >= frames.Count)
                { valid = false; break; }
                previous = dot.texIndex;
            }
            if (previous != frames.Count - 1) valid = false;
            foreach (Texture frame in frames) if (frame == null) valid = false;
        }
        if (!valid) Debug.LogError("Invalid drawing configuration: " + name
            + ". Check task ID, unique ordered dots, increasing texture indices, and frames.", this);
        return valid;
    }

    void OnEnable()
    {
        if (dotTriggers == null) return;
        foreach (DotDrawing dot in dotTriggers)
        {
            if (dot == null || rangeHandlers.ContainsKey(dot)) continue;
            DotDrawing source = dot;
            Action<bool> handler = entered => UpdateRange(source, entered);
            rangeHandlers.Add(dot, handler);
            dot.OnEnteringAndExiting += handler;
            if (dot.IsPlayerInRange) occupiedDots.Add(dot);
        }
        if (transition != null) transition.OnFinished += CompleteTask;
        ResumePresentation();
    }

    private void UpdateRange(DotDrawing dot, bool entered)
    {
        bool wasInside = HasPlayerInRange;
        if (entered) occupiedDots.Add(dot); else occupiedDots.Remove(dot);
        if (wasInside != HasPlayerInRange) OnPlayerEnterExit?.Invoke(HasPlayerInRange);
    }

    public bool CanAccept(DotDrawing dot) => isActiveAndEnabled && IsConfigured && !IsComplete
        && !drawingComplete && dot != null && dot.Owner == this && dot.dotIndex == curDotIndex;

    public bool TryCommit(DotDrawing dot)
    {
        if (!CanAccept(dot)) return false;
        targetTexIndex = dot.texIndex;
        curDotIndex++;
        NotifyProgress();
        ResumePresentation();
        return true;
    }

    private void ResumePresentation()
    {
        if (!isActiveAndEnabled || !IsConfigured || IsComplete) return;
        if (drawingComplete)
        {
            if (transition == null || !transition.CanPlay || transition.IsComplete) CompleteTask();
        }
        else if (animationCoroutine == null && curDotIndex > 0)
            animationCoroutine = StartCoroutine(AnimateMaterial());
    }

    private IEnumerator AnimateMaterial()
    {
        yield return null;
        while (curTexIndex < targetTexIndex)
        {
            surface.SetTexture(frames[++curTexIndex]);
            yield return new WaitForSeconds(Mathf.Max(0f, frameDuration));
        }
        animationCoroutine = null;
        if (curDotIndex != dotTriggers.Count) yield break;
        drawingComplete = true;
        OnDrawingCompleted?.Invoke(message);
        ResumePresentation();
    }

    private void CompleteTask()
    {
        if (!isActiveAndEnabled || !drawingComplete || IsComplete) return;
        IsComplete = true;
        OnTaskCompleted?.Invoke(this);
        NotifyProgress();
    }

    private void NotifyProgress()
    {
        if (OnProgressChanged == null) return;
        foreach (Action handler in OnProgressChanged.GetInvocationList())
            try { handler(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
    }

    void OnDisable()
    {
        foreach (var subscription in rangeHandlers)
            if (subscription.Key != null) subscription.Key.OnEnteringAndExiting -= subscription.Value;
        rangeHandlers.Clear();
        if (transition != null) transition.OnFinished -= CompleteTask;
        bool wasInside = HasPlayerInRange;
        occupiedDots.Clear();
        if (wasInside) OnPlayerEnterExit?.Invoke(false);
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = null;
    }
}
