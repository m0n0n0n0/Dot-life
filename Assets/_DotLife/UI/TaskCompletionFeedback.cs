using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class TaskCompletionFeedback : IDisposable
{
    private readonly UITaskSceneHint hint;
    private readonly Image target;
    private readonly Text status;
    private readonly RectTransform layer;
    private readonly Canvas canvas;
    private readonly UITaskCompletionGlow glow;
    private readonly Vector3 originalScale;
    private readonly Color originalLabelColor;
    private readonly Color startColor;
    private readonly Color endColor;
    private readonly Color lightColor;
    private readonly float dissolveDuration;
    private readonly float flightDuration;
    private readonly float pulseDuration;
    private readonly float fillDuration;
    private readonly float pulseScale;
    private Vector2 launch;
    private float elapsed;
    private bool launched;
    public bool IsComplete { get; private set; }

    public TaskCompletionFeedback(UITaskSceneHint hint, Image target, Text status, RectTransform layer,
        Canvas canvas, Color startColor, Color endColor, Color lightColor,
        float dissolve, float flight, float pulse, float fill, float pulseScale)
    {
        this.hint = hint;
        this.target = target;
        this.status = status;
        this.layer = layer;
        this.canvas = canvas;
        this.startColor = startColor;
        this.endColor = endColor;
        this.lightColor = lightColor;
        this.pulseScale = Mathf.Max(1f, pulseScale);
        dissolveDuration = Mathf.Max(0.01f, dissolve);
        flightDuration = Mathf.Max(0.01f, flight);
        pulseDuration = Mathf.Max(0.01f, pulse);
        fillDuration = Mathf.Max(0.01f, fill);
        originalScale = target.rectTransform.localScale;
        originalLabelColor = status != null ? status.color : Color.black;
        var node = new GameObject("TaskCompletion_" + target.name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(UITaskCompletionGlow));
        node.layer = layer.gameObject.layer;
        node.transform.SetParent(layer, false);
        RectTransform rect = node.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        glow = node.GetComponent<UITaskCompletionGlow>();
        glow.raycastTarget = false;
        glow.maskable = false;
        if (hint != null) hint.BeginCompletion();
        launch = GetOrigin(GetDestination());
        target.color = startColor;
        if (status != null) status.text = "";
    }

    public void Tick(float deltaTime)
    {
        if (IsComplete) return;
        if (target == null || !target.isActiveAndEnabled || layer == null || glow == null)
        { IsComplete = true; return; }
        elapsed += Mathf.Max(0f, deltaTime);
        glow.Clear();
        Vector2 destination = GetDestination();
        if (elapsed < dissolveDuration)
        {
            float t = elapsed / dissolveDuration;
            launch = GetOrigin(destination);
            if (hint != null) hint.SetCompletionProgress(t);
            for (int i = 0; i < 6; i++)
            {
                Vector2 offset = new Vector2((i - 2.5f) * 21f, Mathf.Sin(i * 2.1f) * 18f);
                Vector2 point = launch + offset * (1f - Mathf.SmoothStep(0f, 1f, t));
                glow.AddLight(point, 6f, lightColor, Mathf.Sin(t * Mathf.PI) * 0.7f);
            }
            glow.AddLight(launch, Mathf.Lerp(4f, 14f, t), lightColor, Mathf.SmoothStep(0f, 1f, t));
        }
        else
        {
            if (!launched)
            {
                launch = GetOrigin(destination);
                launched = true;
            }
            if (hint != null) hint.SetCompletionProgress(1f);
            float time = elapsed - dissolveDuration;
            if (time < flightDuration)
            {
                float t = Mathf.SmoothStep(0f, 1f, time / flightDuration);
                Vector2 control = (launch + destination) * 0.5f + Vector2.up * 80f;
                control.y = Mathf.Min(control.y, layer.rect.yMax - 18f);
                for (int i = 7; i >= 1; i--)
                {
                    float trailT = Mathf.Max(0f, t - i * 0.035f);
                    glow.AddLight(Bezier(launch, control, destination, trailT), 9f - i * 0.65f,
                        lightColor, (1f - i / 8f) * 0.4f * Mathf.Sin(Mathf.PI * time / flightDuration));
                }
                glow.AddLight(Bezier(launch, control, destination, t), 14f, lightColor, 1f);
            }
            else if (time < flightDuration + pulseDuration)
            {
                float t = (time - flightDuration) / pulseDuration;
                float bump = Mathf.Sin(t * Mathf.PI);
                target.rectTransform.localScale = originalScale * (1f + (pulseScale - 1f) * bump * bump);
                glow.AddLight(destination, Mathf.Lerp(17f, 24f, t), lightColor, 1f - t);
            }
            else
            {
                target.rectTransform.localScale = originalScale;
                float t = Mathf.Clamp01((time - flightDuration - pulseDuration) / fillDuration);
                target.color = Color.Lerp(startColor, endColor, Mathf.SmoothStep(0f, 1f, t));
                if (status != null)
                {
                    status.text = "OK";
                    Color color = originalLabelColor;
                    color.a *= t;
                    status.color = color;
                }
                if (t >= 1f) IsComplete = true;
            }
        }
        glow.RefreshMesh();
    }

    private Camera UICamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

    private Vector2 GetDestination()
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(UICamera, target.rectTransform.TransformPoint(target.rectTransform.rect.center));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, UICamera, out Vector2 local);
        return local;
    }

    private Vector2 GetOrigin(Vector2 destination)
    {
        Vector2 local = destination + new Vector2(160f, -160f);
        if (hint != null && hint.TryGetCompletionScreenPoint(out Vector2 screen))
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, UICamera, out local);
        Rect bounds = layer.rect;
        local.x = Mathf.Clamp(local.x, bounds.xMin + 20f, bounds.xMax - 20f);
        local.y = Mathf.Clamp(local.y, bounds.yMin + 20f, bounds.yMax - 20f);
        return local;
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) =>
        (1f - t) * (1f - t) * a + 2f * (1f - t) * t * b + t * t * c;

    public void Dispose()
    {
        IsComplete = true;
        if (target != null) target.rectTransform.localScale = originalScale;
        if (status != null) status.color = originalLabelColor;
        if (hint != null) hint.FinishCompletion();
        if (glow != null) UnityEngine.Object.Destroy(glow.gameObject);
    }
}
