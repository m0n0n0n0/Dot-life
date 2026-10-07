using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransitionHandler : MonoBehaviour
{
    public DotDrawingHandler trigger;
    [SerializeField] private List<Texture> frames = new List<Texture>();
    [Min(0f)] public float frameRate = 0.05f;
    public event Action OnFinished;
    public bool IsComplete { get; private set; }
    public bool CanPlay => isActiveAndEnabled && configured;
    private bool configured;
    private TextureSurface surface;
    private Coroutine animationCoroutine;

    void Awake()
    {
        if (trigger == null) trigger = GetComponent<DotDrawingHandler>();
        surface = GetComponent<TextureSurface>();
        if (surface == null && GetComponent<Renderer>() != null) surface = gameObject.AddComponent<TextureSurface>();
        configured = trigger != null && surface != null && surface.IsReady && frames != null && frames.Count > 0;
        if (configured) foreach (Texture frame in frames) if (frame == null) configured = false;
        if (!configured) Debug.LogError("Invalid task transition: " + name, this);
    }
    void OnEnable()
    {
        if (trigger != null) trigger.OnDrawingCompleted += StartTransition;
    }
    private void StartTransition(string ignored)
    {
        if (!CanPlay || IsComplete || animationCoroutine != null) return;
        animationCoroutine = StartCoroutine(AnimateMaterial());
    }
    private IEnumerator AnimateMaterial()
    {
        yield return null;
        foreach (Texture frame in frames)
        {
            surface.SetTexture(frame);
            yield return new WaitForSeconds(Mathf.Max(0f, frameRate));
        }
        animationCoroutine = null;
        IsComplete = true;
        OnFinished?.Invoke();
    }
    void OnDisable()
    {
        if (trigger != null) trigger.OnDrawingCompleted -= StartTransition;
        if (animationCoroutine == null) return;
        StopCoroutine(animationCoroutine);
        animationCoroutine = null;
        surface.SetTexture(frames[frames.Count - 1]);
        IsComplete = true;
        OnFinished?.Invoke();
    }
}
