using UnityEngine;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(CanvasGroup), typeof(InkMeterGraphic))]
public class UIPlayerInk : MonoBehaviour
{
    [SerializeField] private Vector2 screenOffset = new Vector2(16f, 8f);
    [SerializeField] private float heightOffset = 0.25f;
    [SerializeField, Min(0.1f)] private float heightScale = 1.4f;
    [SerializeField, Min(1f)] private float width = 12f;
    [SerializeField, Min(1f)] private float minimumHeight = 48f;
    [SerializeField, Min(1f)] private float maximumHeight = 160f;
    [SerializeField, Min(0f)] private float showDuration = 1f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.18f;
    [SerializeField, Min(0f)] private float spentHoldDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float spentFadeSpeed = 2f;
    [SerializeField, Min(0.01f)] private float flashInterval = 0.16f;
    private PlayerInk ink;
    private CanvasGroup group;
    private InkMeterGraphic graphic;
    private RectTransform rect;
    private Canvas canvas;
    private CharacterController body;
    private float visibleRemaining;
    private float trailRemaining;
    private float trail;
    private float flashElapsed;
    private bool flashing;
    public PlayerInk Source => ink;
    public bool IsFlashing => flashing;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        graphic = GetComponent<InkMeterGraphic>();
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        group.alpha = 0f;
        group.interactable = group.blocksRaycasts = graphic.raycastTarget = false;
    }

    public void Bind(PlayerInk source)
    {
        Unsubscribe();
        ink = source;
        body = ink != null ? ink.GetComponent<CharacterController>() : null;
        ResetDisplay();
        if (isActiveAndEnabled) Subscribe();
    }

    void OnEnable() { Subscribe(); ResetDisplay(); }
    void OnDisable() { Unsubscribe(); ResetDisplay(); }
    private void Subscribe()
    {
        if (ink == null) return;
        ink.InkChanged -= OnInkChanged;
        ink.Refilled -= OnRefilled;
        ink.InsufficientInk -= OnInsufficient;
        ink.InkChanged += OnInkChanged;
        ink.Refilled += OnRefilled;
        ink.InsufficientInk += OnInsufficient;
    }
    private void Unsubscribe()
    {
        if (ink == null) return;
        ink.InkChanged -= OnInkChanged;
        ink.Refilled -= OnRefilled;
        ink.InsufficientInk -= OnInsufficient;
    }

    void LateUpdate() => TickDisplay(Time.deltaTime);
    public void TickDisplay(float deltaTime)
    {
        if (ink == null || !ink.isActiveAndEnabled || ink.Runtime == null
            || ink.Runtime.Phase != GameSessionPhase.Playing)
        { ResetDisplay(); return; }
        float dt = Mathf.Max(0f, deltaTime);
        float fill = ink.NormalizedInk;
        float catchup = Mathf.Max(0f, dt - trailRemaining);
        trailRemaining = Mathf.Max(0f, trailRemaining - dt);
        trail = Mathf.Max(fill, Mathf.MoveTowards(trail, fill, catchup * spentFadeSpeed));
        visibleRemaining = Mathf.Max(0f, visibleRemaining - dt);
        bool projected = UpdatePosition();
        if (flashing)
        {
            flashElapsed += dt;
            int interval = Mathf.FloorToInt(flashElapsed / Mathf.Max(0.01f, flashInterval));
            if (interval >= 4) { flashing = false; group.alpha = 0f; }
            else group.alpha = projected && interval % 2 == 0 ? 1f : 0f;
        }
        else
            group.alpha = Mathf.MoveTowards(group.alpha, projected && visibleRemaining > 0f ? 1f : 0f,
                dt / Mathf.Max(0.01f, fadeDuration));
        graphic.SetValues(fill, trail, flashing ? 1f : 0f);
    }

    private bool UpdatePosition()
    {
        Camera camera = Camera.main;
        if (body == null || camera == null || canvas == null || !(rect.parent is RectTransform parent)) return false;
        Vector3 center = body.transform.TransformPoint(body.center);
        float bodyHeight = body.height * Mathf.Abs(body.transform.lossyScale.y);
        Vector3 screen = camera.WorldToScreenPoint(center + Vector3.up * heightOffset);
        if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height) return false;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out Vector2 position)) return false;
        float scale = Mathf.Max(0.001f, canvas.scaleFactor);
        float radius = body.radius * Mathf.Max(Mathf.Abs(body.transform.lossyScale.x), Mathf.Abs(body.transform.lossyScale.z));
        float right = camera.WorldToScreenPoint(center + camera.transform.right * radius).x
            - camera.WorldToScreenPoint(center).x;
        position += screenOffset + Vector2.right * (right / scale + width * 0.5f);
        rect.anchoredPosition = position;
        float projectedHeight = Mathf.Abs(camera.WorldToScreenPoint(center + Vector3.up * bodyHeight * 0.5f).y
            - camera.WorldToScreenPoint(center - Vector3.up * bodyHeight * 0.5f).y) / scale;
        rect.sizeDelta = new Vector2(Mathf.Max(1f, width), Mathf.Clamp(projectedHeight * Mathf.Max(0.1f, heightScale),
            minimumHeight, Mathf.Max(minimumHeight, maximumHeight)));
        return true;
    }

    private void OnInkChanged(float previous, float current)
    {
        if (current >= previous) { trail = ink.NormalizedInk; trailRemaining = 0f; return; }
        trail = Mathf.Max(trail, previous / ink.MaxInk);
        trailRemaining = spentHoldDuration;
        visibleRemaining = showDuration;
        flashing = false;
        group.alpha = 1f;
        graphic.SetValues(ink.NormalizedInk, trail, 0f);
    }
    private void OnRefilled()
    {
        flashing = false;
        visibleRemaining = showDuration;
        group.alpha = 1f;
    }
    private void OnInsufficient()
    {
        if (flashing) return;
        flashing = true;
        flashElapsed = visibleRemaining = trailRemaining = trail = 0f;
        group.alpha = 1f;
        graphic.SetValues(ink.NormalizedInk, ink.NormalizedInk, 1f);
    }
    private void ResetDisplay()
    {
        visibleRemaining = trailRemaining = flashElapsed = 0f;
        flashing = false;
        trail = ink != null ? ink.NormalizedInk : 0f;
        if (group != null) group.alpha = 0f;
        if (graphic != null) graphic.SetValues(trail, trail, 0f);
    }
}
