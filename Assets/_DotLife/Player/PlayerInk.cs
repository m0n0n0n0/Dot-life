using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInk : MonoBehaviour
{
    [SerializeField] private GameState gameState;
    [SerializeField] private DotDrawingHandler diary;
    [Min(0.01f)] public float maximumInk = 20f;
    [Min(0.01f)] public float stampCost = 1f;
    public float CurrentInk { get; private set; }
    public float StampCost => Positive(stampCost, 1f);
    public float InitialInk => (diary != null ? diary.StepCount : 0) * StampCost;
    public float MaxInk => Mathf.Max(Positive(maximumInk, 20f), InitialInk);
    public float NormalizedInk => Mathf.Clamp01(CurrentInk / MaxInk);
    public bool HasDipped { get; private set; }
    public GameState Runtime => gameState;
    public bool CanSpend => IsPlaying && !committing && CurrentInk + Tolerance >= StampCost;
    public bool CanRefill => IsPlaying && !committing && gameState.IsTaskComplete(DemoTaskId.Diary);
    public event Action<float, float> InkChanged;
    public event Action Refilled;
    public event Action InsufficientInk;
    private bool committing;
    private float Tolerance => Mathf.Max(0.00001f, StampCost * 0.00001f);
    private bool IsPlaying => isActiveAndEnabled && gameState != null && gameState.isActiveAndEnabled
        && gameState.Phase == GameSessionPhase.Playing;

    void Awake()
    {
        if (gameState == null) gameState = FindObjectOfType<GameState>();
        if (diary == null)
            foreach (DotDrawingHandler task in FindObjectsOfType<DotDrawingHandler>())
                if (task.TaskId == DemoTaskId.Diary) { diary = task; break; }
        if (gameState == null || diary == null)
            Debug.LogError("PlayerInk requires a game session and the diary task.", this);
        CurrentInk = InitialInk;
    }

    public bool TryCommitStamp(Func<bool> commit)
    {
        if (!IsPlaying || committing || commit == null) return false;
        if (!CanSpend) { Publish(InsufficientInk); return false; }
        committing = true;
        float previous = CurrentInk;
        bool accepted = false;
        try
        {
            CurrentInk = Mathf.Max(0f, previous - StampCost);
            if (CurrentInk <= Tolerance) CurrentInk = 0f;
            accepted = commit();
            if (accepted) PublishChange(previous);
            return accepted;
        }
        finally
        {
            if (!accepted) CurrentInk = previous;
            committing = false;
        }
    }

    public bool TryRefill()
    {
        if (!CanRefill) return false;
        committing = true;
        try
        {
            float previous = CurrentInk;
            CurrentInk = MaxInk;
            HasDipped = true;
            if (CurrentInk != previous) PublishChange(previous);
            Publish(Refilled);
            return true;
        }
        finally { committing = false; }
    }

    public void ReportInsufficientInk()
    {
        if (IsPlaying && !committing && !CanSpend) Publish(InsufficientInk);
    }

    private void PublishChange(float previous)
    {
        if (InkChanged == null) return;
        foreach (Action<float, float> handler in InkChanged.GetInvocationList())
            try { handler(previous, CurrentInk); }
            catch (Exception exception) { Debug.LogException(exception, this); }
    }

    private void Publish(Action notification)
    {
        if (notification == null) return;
        foreach (Action handler in notification.GetInvocationList())
            try { handler(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
    }

    private static float Positive(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value)
        ? fallback : Mathf.Max(0.01f, value);
}
