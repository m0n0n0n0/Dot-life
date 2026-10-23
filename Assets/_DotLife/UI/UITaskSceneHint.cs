using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// Keep the prototype's script GUID stable for existing references.
[ExecuteAlways]
[DefaultExecutionOrder(1100)]
[RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
public class UITaskSceneHint : MonoBehaviour
{
    [SerializeField] private DotDrawingHandler task;
    [SerializeField] private GameState gameState;
    [SerializeField] private TaskStatusBar hud;
    [SerializeField] private Text label;
    [SerializeField] private bool previewInEditor = true;
    [Header("Text Reveal")]
    [SerializeField, Min(1f)] private float charactersPerSecond = 10f;
    [Header("Task Area")]
    [Tooltip("Optional region root. When empty, use the book or album group under Tasks.")]
    [SerializeField] private Transform taskAreaRoot;
    [SerializeField, Min(0f)] private float areaPadding = 0.25f;
    [SerializeField, Min(0f)] private float areaHeightTolerance = 0.5f;
    private Canvas hintCanvas;
    private CanvasGroup group;
    private bool enteredTaskArea;
    private float revealTime;
    private string cachedDescription;
    private int[] characterStarts = new int[0];
    private string displayedText;
    private int lastRevealedCount = -1;
    private Renderer[] areaRenderers;
    private UITaskTextDissolve dissolveEffect;
    private bool completing;
    private float completionProgress;
    private float completionAlpha;

    public DemoTaskId TaskId => task != null ? task.TaskId : DemoTaskId.Unassigned;
    public string FullDescription => hud != null ? hud.GetTaskDescription(TaskId) : "";
    public bool IsReady => isActiveAndEnabled && task != null && task.isActiveAndEnabled
        && label != null && label.font != null && !string.IsNullOrWhiteSpace(FullDescription);

    public void BeginCompletion()
    {
        if (!Application.IsPlaying(gameObject)) return;
        if (group == null) group = GetComponent<CanvasGroup>();
        completing = true;
        completionProgress = 0f;
        completionAlpha = group.alpha;
        if (label == null) return;
        dissolveEffect = label.GetComponent<UITaskTextDissolve>();
        if (dissolveEffect == null) dissolveEffect = label.gameObject.AddComponent<UITaskTextDissolve>();
        dissolveEffect.enabled = true;
        dissolveEffect.SetProgress(0f);
    }

    public void SetCompletionProgress(float progress)
    {
        if (!completing) return;
        completionProgress = Mathf.Clamp01(progress);
        if (dissolveEffect != null) dissolveEffect.SetProgress(completionProgress);
    }

    public void FinishCompletion()
    {
        completing = false;
        if (dissolveEffect != null) dissolveEffect.enabled = false;
        if (group != null) group.alpha = 0f;
        if (hintCanvas != null) hintCanvas.enabled = false;
    }

    public bool TryGetCompletionScreenPoint(out Vector2 screen)
    {
        screen = default;
        if (label == null) return false;
        Camera camera = hintCanvas != null && hintCanvas.worldCamera != null ? hintCanvas.worldCamera : Camera.main;
        if (camera == null) return false;
        Vector3 point = camera.WorldToScreenPoint(label.rectTransform.TransformPoint(label.rectTransform.rect.center));
        if (point.z <= 0f) return false;
        screen = point;
        return true;
    }

    public void Bind(DotDrawingHandler source, GameState state, TaskStatusBar owner, Text text)
    {
        task = source;
        gameState = state;
        hud = owner;
        label = text;
        areaRenderers = null;
        Refresh();
    }

    private void OnEnable()
    {
        completing = false;
        if (dissolveEffect != null) dissolveEffect.enabled = false;
        group = GetComponent<CanvasGroup>();
        areaRenderers = null;
        if (Application.IsPlaying(gameObject))
        {
            group.alpha = 0f;
            enteredTaskArea = false;
            revealTime = 0f;
            lastRevealedCount = -1;
        }
        Refresh();
    }
    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (hintCanvas == null) hintCanvas = GetComponent<Canvas>();
        if (group == null) group = GetComponent<CanvasGroup>();
        bool playing = Application.IsPlaying(gameObject);
        if (playing && completing)
        {
            group.alpha = completionProgress < 1f ? completionAlpha : 0f;
            hintCanvas.enabled = group.alpha > 0.001f;
            return;
        }
        string description = FullDescription;
        if (description != cachedDescription)
        {
            cachedDescription = description;
            characterStarts = StringInfo.ParseCombiningCharacters(description);
            revealTime = 0f;
            lastRevealedCount = -1;
        }
        bool currentTask = IsReady && hud != null && hud.isActiveAndEnabled
            && gameState != null && gameState.isActiveAndEnabled
            && gameState.Phase == GameSessionPhase.Playing && hud.SceneHintsEnabled
            && hud.ActiveTaskIndex == (int)TaskId;
        if (playing && gameState != null && gameState.Phase == GameSessionPhase.Waiting)
        {
            enteredTaskArea = false;
            revealTime = 0f;
        }
        if (playing && currentTask && !enteredTaskArea && IsPlayerInTaskArea()) enteredTaskArea = true;
        bool show = !playing ? previewInEditor : currentTask && enteredTaskArea;
        if (playing && show) revealTime += Time.deltaTime;
        if (label != null)
        {
            string text = description;
            if (playing)
            {
                int count = Mathf.Min(characterStarts.Length,
                    Mathf.FloorToInt(revealTime * Mathf.Max(1f, charactersPerSecond)));
                if (count == lastRevealedCount && displayedText != null) text = displayedText;
                else if (count < characterStarts.Length)
                {
                    int split = characterStarts[count];
                    // Transparent suffix keeps line wrapping and best-fit sizing stable during reveal.
                    text = description.Substring(0, split) + "<color=#00000000>"
                        + description.Substring(split) + "</color>";
                }
                lastRevealedCount = count;
                label.supportRichText = true;
            }
            if (displayedText != text || label.text != text)
            {
                label.text = text;
                displayedText = text;
            }
        }
        // Preview changes visibility only; authored transforms and typography remain untouched.
        group.alpha = !playing ? (show ? 1f : 0f)
            : Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);
        group.interactable = false;
        group.blocksRaycasts = false;
        hintCanvas.enabled = group.alpha > 0.001f;
    }

    private bool IsPlayerInTaskArea()
    {
        BasicBehaviourScript player = hud != null ? hud.HintPlayer : null;
        if (player == null || player.IsAppearing || !player.IsGrounded || player.Controller == null || !player.Controller.enabled) return false;
        if (areaRenderers == null)
        {
            Transform root = taskAreaRoot;
            if (root == null)
            {
                root = task.transform;
                Transform candidate = root;
                while (candidate.parent != null && candidate.parent.name != "Tasks") candidate = candidate.parent;
                if (candidate.parent != null) root = candidate;
            }
            areaRenderers = root.GetComponentsInChildren<Renderer>(true);
        }

        bool hasBounds = false;
        Bounds area = default;
        foreach (Renderer renderer in areaRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            // UI hints and individual stamp triggers must not enlarge the book's region.
            if (renderer.GetComponentInParent<UITaskSceneHint>() != null
                || renderer.GetComponentInParent<DotDrawing>() != null) continue;
            if (!hasBounds) { area = renderer.bounds; hasBounds = true; }
            else area.Encapsulate(renderer.bounds);
        }
        if (!hasBounds) return false;
        area.Expand(new Vector3(areaPadding * 2f, areaHeightTolerance * 2f, areaPadding * 2f));
        Bounds body = player.Controller.bounds;
        Vector3 feet = new Vector3(body.center.x, body.min.y, body.center.z);
        return area.Contains(feet);
    }

    private void OnDisable()
    {
        completing = false;
        if (dissolveEffect != null) dissolveEffect.enabled = false;
        if (hintCanvas != null) hintCanvas.enabled = false;
        if (group != null) group.alpha = 0f;
    }
}
