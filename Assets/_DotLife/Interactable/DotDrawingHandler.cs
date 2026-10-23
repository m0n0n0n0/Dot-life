using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DotDrawingHandler : MonoBehaviour
{
    [SerializeField] private List<DotDrawing> dotTriggers = new List<DotDrawing>();
    [SerializeField] private List<Texture> frames = new List<Texture>();
    [SerializeField] private DemoTaskId taskId = DemoTaskId.Unassigned;
    [UnityEngine.Serialization.FormerlySerializedAs("diaryCompositeShader")]
    [SerializeField] private Shader frameCompositeShader;
    public string message = "";
    [Min(0f)] public float frameDuration = 0.2f;
    public event Action<string> OnDrawingCompleted;
    public event Action<DotDrawingHandler> OnTaskCompleted;
    public event Action<bool> OnPlayerEnterExit;
    public event Action OnProgressChanged;
    public DemoTaskId TaskId => taskId;
    public int AcceptedSteps => acceptedDots.Count;
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
    private bool drawingComplete;
    private readonly HashSet<DotDrawing> acceptedDots = new HashSet<DotDrawing>();
    private readonly Queue<int> pendingFrames = new Queue<int>();
    private TaskInkComposite composite;

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
        if (dotTriggers != null && dotTriggers.Count > 1 && frameCompositeShader == null) valid = false;
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
            + ". Check task ID, unique dot indices, increasing texture indices, frames, and frame composite shader.", this);
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
        && !drawingComplete && dot != null && dot.Owner == this
        && dotTriggers.Contains(dot) && !acceptedDots.Contains(dot);

    public bool TryCommit(DotDrawing dot)
    {
        if (!CanAccept(dot)) return false;
        if (dotTriggers.Count > 1)
        {
            if (composite == null) composite = new TaskInkComposite(frames[0], frameCompositeShader);
            if (!composite.IsReady) return false;
        }
        int firstFrame = 1;
        foreach (DotDrawing previous in dotTriggers)
            if (previous.dotIndex == dot.dotIndex - 1) { firstFrame = previous.texIndex + 1; break; }
        for (int frame = firstFrame; frame <= dot.texIndex; frame++) pendingFrames.Enqueue(frame);
        acceptedDots.Add(dot);
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
        else if (animationCoroutine == null && AcceptedSteps > 0)
            animationCoroutine = StartCoroutine(AnimateMaterial());
    }

    private IEnumerator AnimateMaterial()
    {
        yield return null;
        while (pendingFrames.Count > 0)
        {
            int frame = pendingFrames.Dequeue();
            if (composite != null)
            {
                composite.Apply(frames[frame - 1], frames[frame]);
                surface.SetTexture(composite.Texture);
            }
            else surface.SetTexture(frames[frame]);
            yield return new WaitForSeconds(Mathf.Max(0f, frameDuration));
        }
        animationCoroutine = null;
        if (AcceptedSteps != dotTriggers.Count) yield break;
        surface.SetTexture(frames[frames.Count - 1]);
        if (composite != null)
        {
            composite.Dispose();
            composite = null;
        }
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

    void OnDestroy()
    {
        if (composite == null) return;
        if (surface != null && surface.CurrentTexture == composite.Texture) surface.SetTexture(frames[0]);
        composite.Dispose();
        composite = null;
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
