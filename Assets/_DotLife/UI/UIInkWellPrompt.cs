using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(1100)]
[RequireComponent(typeof(Canvas))]
public class UIInkWellPrompt : MonoBehaviour
{
    [SerializeField, Min(0f)] private float heightOffset = 0.4f;
    [SerializeField, Min(0f)] private float floatAmplitude = 0.06f;
    [SerializeField, Min(0.1f)] private float floatPeriod = 1.8f;
    [SerializeField, Min(0.001f)] private float worldScale = 0.012f;
    private InkWell well;
    private InteractingHandler interactor;
    private Collider area;
    private Renderer[] modelRenderers;
    private Canvas promptCanvas;
    private Text symbol;
    public InkWell Source => well;
    public bool IsVisible => promptCanvas != null && promptCanvas.enabled;

    void Awake()
    {
        promptCanvas = GetComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.WorldSpace;
        promptCanvas.sortingOrder = 10;
        promptCanvas.enabled = false;
        GetComponent<RectTransform>().sizeDelta = new Vector2(96f, 72f);
        var label = new GameObject("Key", typeof(RectTransform), typeof(Text), typeof(Outline));
        label.transform.SetParent(transform, false);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        symbol = label.GetComponent<Text>();
        symbol.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        symbol.fontSize = 42;
        symbol.fontStyle = FontStyle.Bold;
        symbol.resizeTextForBestFit = true;
        symbol.resizeTextMinSize = 18;
        symbol.resizeTextMaxSize = 42;
        symbol.alignment = TextAnchor.MiddleCenter;
        symbol.color = Color.white;
        symbol.raycastTarget = false;
        Outline outline = label.GetComponent<Outline>();
        outline.effectColor = new Color(0.08f, 0.1f, 0.12f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }

    public void Bind(InkWell source, InteractingHandler actor, Font font)
    {
        well = source;
        interactor = actor;
        area = well != null ? well.GetComponent<Collider>() : null;
        modelRenderers = well != null ? well.GetComponentsInChildren<Renderer>(true) : null;
        if (font != null) symbol.font = font;
    }

    void LateUpdate() => TickPrompt(Time.time);

    public void TickPrompt(float time)
    {
        Camera camera = Camera.main;
        if (well == null || interactor == null || interactor.Movement == null || camera == null
            || area == null || !area.enabled || !area.isTrigger || !well.CanRefill(interactor.Movement))
        { promptCanvas.enabled = false; return; }
        Bounds bounds = area.bounds;
        bool hasModel = false;
        foreach (Renderer renderer in modelRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!hasModel) { bounds = renderer.bounds; hasModel = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        float bob = Mathf.Sin(time * Mathf.PI * 2f / Mathf.Max(0.1f, floatPeriod)) * Mathf.Max(0f, floatAmplitude);
        transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.max.y + heightOffset + bob, bounds.center.z), camera.transform.rotation);
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(worldScale / SafeScale(parentScale.x), worldScale / SafeScale(parentScale.y), worldScale / SafeScale(parentScale.z));
        symbol.text = interactor.InteractionKey.ToString();
        Vector3 screen = camera.WorldToViewportPoint(transform.position);
        promptCanvas.enabled = screen.z > 0f && screen.x >= 0f && screen.x <= 1f && screen.y >= 0f && screen.y <= 1f;
    }

    private static float SafeScale(float value) => Mathf.Abs(value) < 0.001f ? 0.001f : value;
    void OnDisable() { if (promptCanvas != null) promptCanvas.enabled = false; }
}
