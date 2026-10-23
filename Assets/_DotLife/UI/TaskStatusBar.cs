using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(1200)]
public class TaskStatusBar : MonoBehaviour
{
    [SerializeField] private GameState gameState;
    [SerializeField] private InteractingHandler interactor;
    [SerializeField] private List<Image> taskStatusImages;
    [SerializeField] private Color completedColor = Color.green;
    [SerializeField] private Color incompleteColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float incompleteOpacity = 0.5f;
    [SerializeField] private Text taskDescriptionText;
    [SerializeField] private List<string> taskDescriptions = new List<string> { "日记", "语文书", "数学书", "相册" };
    [Header("Task Scene Hints")]
    [SerializeField, InspectorName("Use Task Scene Hints")] private bool diaryHintInScene = true;
    [Header("Interaction Prompt")]
    [SerializeField] private Text interactionPromptText;
    [SerializeField] private UIPlayerInk inkMeterPrefab;
    [SerializeField] private string gameStartMessage = "按任意键开始游玩";
    [SerializeField] private string gameOverMessage = "我们的游戏就到这里！感谢你的游玩^^";
    [SerializeField] private string returnToMenuMessage = "按ESC回到开始页面";
    [Header("Completion Feedback")]
    [SerializeField, Min(0.01f)] private float textDissolveDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float lightFlightDuration = 0.65f;
    [SerializeField, Min(0.01f)] private float circlePulseDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float circleFillDuration = 0.35f;
    [SerializeField, Range(1f, 1.3f)] private float circlePulseScale = 1.15f;
    [SerializeField] private Color completionLightColor = new Color(1f, 0.9f, 0.62f, 1f);
    [Header("Readability")]
    [SerializeField] private Text taskHeadingText;
    [SerializeField, Min(1)] private int taskTextSize = 36;
    [SerializeField, Min(1)] private int taskTextMinSize = 28;
    [SerializeField, Min(1)] private int promptTextSize = 28;
    [SerializeField, Min(1)] private int headingTextSize = 24;
    [SerializeField, Min(1)] private int statusTextSize = 26;
    [SerializeField, Min(1f)] private float statusCircleSize = 60f;
    [SerializeField, Min(0f)] private float statusCircleGap = 12f;
    private GameSessionPhase lastPhase = (GameSessionPhase)(-1);
    private InteractionTarget lastTarget;
    private int lastTaskIndex = -2;
    private KeyCode lastKey;
    private bool lastFreeStamp;
    private bool lastEndingReady;
    private readonly List<Text> statusLabels = new List<Text>();
    private readonly List<UIInkWellPrompt> inkWellPrompts = new List<UIInkWellPrompt>();
    private UITaskSceneHint[] taskSceneHints;
    private bool lastSceneDiary;
    private Text startPromptText;
    private TaskCompletionFeedback[] completionEffects;
    private bool[] completedShown;
    private RectTransform feedbackLayer;
    private Canvas feedbackCanvas;
    private bool lastFeedbackPending;

    public bool HasPendingCompletionFeedback
    {
        get
        {
            if (completionEffects != null)
                foreach (var effect in completionEffects) if (effect != null) return true;
            return false;
        }
    }

    public bool SceneHintsEnabled => diaryHintInScene;
    public BasicBehaviourScript HintPlayer => interactor != null ? interactor.Movement : null;
    public int ActiveTaskIndex => gameState != null ? CurrentTaskIndex() : -1;
    public string GetTaskDescription(DemoTaskId task)
    {
        int index = (int)task;
        return index >= 0 && index < taskDescriptions.Count ? taskDescriptions[index] : "";
    }

    public UITaskSceneHint GetSceneHint(int taskIndex)
    {
        if (taskSceneHints == null) return null;
        foreach (var hint in taskSceneHints)
            if (hint != null && (int)hint.TaskId == taskIndex) return hint;
        return null;
    }

    void Awake()
    {
        if (gameState == null) gameState = FindObjectOfType<GameState>();
        if (interactor == null) interactor = FindObjectOfType<InteractingHandler>();
        ApplyReadability();
        CreateInkWellPrompts();
        taskSceneHints = FindObjectsOfType<UITaskSceneHint>(true);
        if (inkMeterPrefab != null && interactor != null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                UIPlayerInk meter = Instantiate(inkMeterPrefab, canvas.transform, false);
                meter.name = "PlayerInkMeter";
                meter.Bind(interactor.Ink);
            }
        }
        if (taskDescriptionText != null)
        {
            taskDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            taskDescriptionText.verticalOverflow = VerticalWrapMode.Truncate;
            taskDescriptionText.resizeTextForBestFit = true;
            taskDescriptionText.resizeTextMinSize = Mathf.Clamp(taskTextMinSize, 1, taskDescriptionText.fontSize);
            taskDescriptionText.resizeTextMaxSize = taskDescriptionText.fontSize;
        }
        CreateStartPrompt();
        if (taskStatusImages == null) return;
        completionEffects = new TaskCompletionFeedback[taskStatusImages.Count];
        completedShown = new bool[taskStatusImages.Count];
        CreateFeedbackLayer();
        foreach (Image image in taskStatusImages)
        {
            if (image == null) { statusLabels.Add(null); continue; }
            var label = new GameObject("StatusLabel", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(image.transform, false);
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Text text = label.GetComponent<Text>();
            text.font = taskDescriptionText != null ? taskDescriptionText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.Max(1, statusTextSize);
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.raycastTarget = false;
            statusLabels.Add(text);
        }
    }

    private void CreateFeedbackLayer()
    {
        Canvas parent = GetComponentInParent<Canvas>();
        if (parent == null) return;
        feedbackCanvas = parent.rootCanvas;
        var node = new GameObject("TaskCompletionFeedback", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        node.layer = feedbackCanvas.gameObject.layer;
        node.transform.SetParent(feedbackCanvas.transform, false);
        feedbackLayer = node.GetComponent<RectTransform>();
        feedbackLayer.anchorMin = Vector2.zero;
        feedbackLayer.anchorMax = Vector2.one;
        feedbackLayer.offsetMin = feedbackLayer.offsetMax = Vector2.zero;
        Canvas overlay = node.GetComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingLayerID = feedbackCanvas.sortingLayerID;
        overlay.sortingOrder = feedbackCanvas.sortingOrder + 20;
        CanvasGroup group = node.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void CreateStartPrompt()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || taskDescriptionText == null) return;
        startPromptText = Instantiate(taskDescriptionText, canvas.transform, false);
        startPromptText.name = "GameStartPrompt";
        startPromptText.text = gameStartMessage;
        startPromptText.alignment = TextAnchor.MiddleCenter;
        startPromptText.raycastTarget = false;
        RectTransform rect = startPromptText.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.85f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition3D = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(640f, 100f);
        startPromptText.gameObject.SetActive(gameState != null && gameState.Phase == GameSessionPhase.Waiting);
    }

    private void CreateInkWellPrompts()
    {
        if (gameState == null || interactor == null) return;
        TriggerBus bus = gameState.GetComponent<TriggerBus>();
        if (bus == null) return;
        foreach (InkWell well in bus.inkWells)
        {
            if (well == null) continue;
            var root = new GameObject("InkWellPrompt", typeof(RectTransform), typeof(Canvas), typeof(UIInkWellPrompt));
            root.transform.SetParent(well.transform, false);
            UIInkWellPrompt prompt = root.GetComponent<UIInkWellPrompt>();
            prompt.Bind(well, interactor, taskDescriptionText != null ? taskDescriptionText.font : null);
            inkWellPrompts.Add(prompt);
        }
    }

    private void ApplyReadability()
    {
        if (taskHeadingText == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform heading = canvas != null ? canvas.transform.Find("TaskHeading") : null;
            if (heading != null) taskHeadingText = heading.GetComponent<Text>();
        }
        if (taskHeadingText != null)
        {
            taskHeadingText.fontSize = Mathf.Max(taskHeadingText.fontSize, headingTextSize);
            GrowFromTopLeft(taskHeadingText.rectTransform, new Vector2(260f, 44f));
        }
        if (taskDescriptionText != null)
        {
            taskDescriptionText.fontSize = Mathf.Max(taskDescriptionText.fontSize, taskTextSize);
            GrowFromTopLeft(taskDescriptionText.rectTransform, new Vector2(640f, 180f));
        }
        if (interactionPromptText != null)
        {
            interactionPromptText.fontSize = Mathf.Max(interactionPromptText.fontSize, promptTextSize);
            RectTransform rect = interactionPromptText.rectTransform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(rect.rect.width, 480f));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(rect.rect.height, 64f));
        }
        if (taskStatusImages == null) return;
        RectTransform previous = null;
        foreach (Image image in taskStatusImages)
        {
            if (image == null) continue;
            RectTransform rect = image.rectTransform;
            float size = Mathf.Max(1f, statusCircleSize, rect.rect.width, rect.rect.height);
            GrowFromTopLeft(rect, new Vector2(size, size));
            if (previous != null)
            {
                Vector2 position = rect.anchoredPosition;
                position.x = previous.anchoredPosition.x + (1f - previous.pivot.x) * previous.rect.width
                    + Mathf.Max(0f, statusCircleGap) + rect.pivot.x * size;
                rect.anchoredPosition = position;
            }
            if (taskHeadingText != null)
            {
                var corners = new Vector3[4];
                taskHeadingText.rectTransform.GetWorldCorners(corners);
                float headingBottom = corners[0].y;
                rect.GetWorldCorners(corners);
                float overlap = corners[1].y - headingBottom + 8f * rect.lossyScale.y;
                if (overlap > 0f) rect.position += Vector3.down * overlap;
            }
            previous = rect;
        }
    }

    // Enlarge UI at runtime without rewriting the scene or shifting the authored top-left edge.
    private static void GrowFromTopLeft(RectTransform rect, Vector2 minimumSize)
    {
        Vector2 oldSize = rect.rect.size;
        Vector2 newSize = Vector2.Max(oldSize, minimumSize);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, newSize.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newSize.y);
        rect.anchoredPosition += Vector2.Scale(newSize - oldSize, new Vector2(rect.pivot.x, rect.pivot.y - 1f));
    }

    void OnEnable()
    {
        if (gameState != null) gameState.OnTaskStateChanged += RefreshStatus;
        RefreshStatus(-1);
        lastPhase = (GameSessionPhase)(-1);
        foreach (UIInkWellPrompt prompt in inkWellPrompts)
            if (prompt != null) prompt.enabled = true;
    }

    private void RefreshStatus(int changedIndex)
    {
        if (gameState == null || taskStatusImages == null) return;
        for (int i = 0; i < taskStatusImages.Count; i++)
        {
            bool complete = i < gameState.taskCompleted.Count && gameState.taskCompleted[i];
            if (completionEffects != null && i < completionEffects.Length)
            {
                if (!complete && completionEffects[i] != null)
                {
                    completionEffects[i].Dispose();
                    completionEffects[i] = null;
                }
                if (complete && completionEffects[i] != null) continue;
                bool animate = complete && i == changedIndex && !completedShown[i]
                    && feedbackLayer != null && taskStatusImages[i] != null && taskStatusImages[i].isActiveAndEnabled;
                completedShown[i] = complete;
                if (animate)
                {
                    Color from = incompleteColor;
                    from.a = Mathf.Clamp01(incompleteOpacity);
                    Color to = completedColor;
                    to.a = 1f;
                    completionEffects[i] = new TaskCompletionFeedback(SceneHintsEnabled ? GetSceneHint(i) : null,
                        taskStatusImages[i], i < statusLabels.Count ? statusLabels[i] : null, feedbackLayer, feedbackCanvas,
                        from, to, completionLightColor, textDissolveDuration, lightFlightDuration,
                        circlePulseDuration, circleFillDuration, circlePulseScale);
                    continue;
                }
            }
            SetStatusVisual(i, complete);
        }
        if (interactor != null) interactor.SetCompletionFeedbackPending(HasPendingCompletionFeedback);
    }

    private void SetStatusVisual(int index, bool complete)
    {
        if (taskStatusImages == null || index < 0 || index >= taskStatusImages.Count) return;
        Color color = complete ? completedColor : incompleteColor;
        color.a = complete ? 1f : Mathf.Clamp01(incompleteOpacity);
        if (taskStatusImages[index] != null) taskStatusImages[index].color = color;
        if (index < statusLabels.Count && statusLabels[index] != null)
        {
            statusLabels[index].text = complete ? "OK" : "";
            Color labelColor = statusLabels[index].color;
            labelColor.a = 1f;
            statusLabels[index].color = labelColor;
        }
    }

    private void TickCompletionFeedback()
    {
        if (completionEffects == null) return;
        for (int i = 0; i < completionEffects.Length; i++)
        {
            var effect = completionEffects[i];
            if (effect == null) continue;
            effect.Tick(Time.unscaledDeltaTime);
            if (!effect.IsComplete) continue;
            effect.Dispose();
            completionEffects[i] = null;
            SetStatusVisual(i, gameState != null && i < gameState.taskCompleted.Count && gameState.taskCompleted[i]);
        }
        if (interactor != null) interactor.SetCompletionFeedbackPending(HasPendingCompletionFeedback);
    }

    private void ClearCompletionFeedback()
    {
        if (completionEffects != null)
            for (int i = 0; i < completionEffects.Length; i++)
            {
                if (completionEffects[i] == null) continue;
                completionEffects[i].Dispose();
                completionEffects[i] = null;
                SetStatusVisual(i, gameState != null && i < gameState.taskCompleted.Count && gameState.taskCompleted[i]);
            }
        if (interactor != null) interactor.SetCompletionFeedbackPending(false);
    }

    void LateUpdate()
    {
        TickCompletionFeedback();
        if (gameState == null) return;
        GameSessionPhase phase = gameState.Phase;
        InteractionTarget target = interactor != null ? interactor.CurrentTarget : null;
        if (target != null && !target.CanInteract(interactor.Movement)) target = null;
        int taskIndex = CurrentTaskIndex();
        KeyCode key = interactor != null ? interactor.InteractionKey : KeyCode.E;
        bool freeStamp = target == null && interactor != null && interactor.Stamping != null && interactor.Stamping.CanOfferStamp;
        bool endingReady = phase == GameSessionPhase.Finished && interactor != null && interactor.CanReturnToMenu;
        bool feedbackPending = HasPendingCompletionFeedback;
        UITaskSceneHint sceneHint = GetSceneHint(taskIndex);
        bool sceneDiary = phase == GameSessionPhase.Playing && diaryHintInScene
            && sceneHint != null && sceneHint.IsReady && Camera.main != null;
        if (phase == lastPhase && target == lastTarget && taskIndex == lastTaskIndex && key == lastKey && freeStamp == lastFreeStamp
            && endingReady == lastEndingReady && sceneDiary == lastSceneDiary && feedbackPending == lastFeedbackPending) return;
        lastPhase = phase;
        lastTarget = target;
        lastTaskIndex = taskIndex;
        lastKey = key;
        lastFreeStamp = freeStamp;
        lastEndingReady = endingReady;
        lastSceneDiary = sceneDiary;
        lastFeedbackPending = feedbackPending;
        string description = "";
        string prompt = "";
        if (startPromptText != null) startPromptText.gameObject.SetActive(phase == GameSessionPhase.Waiting);
        if (phase == GameSessionPhase.Waiting)
        {
            if (startPromptText != null) startPromptText.text = gameStartMessage;
            else description = gameStartMessage;
        }
        else if (phase == GameSessionPhase.Finished)
        {
            if (!feedbackPending) description = gameOverMessage;
            if (endingReady && !feedbackPending) prompt = returnToMenuMessage;
        }
        else
        {
            if (!sceneDiary && taskIndex >= 0 && taskIndex < taskDescriptions.Count)
                description = taskDescriptions[taskIndex];
            if ((target != null && !(target is InkWell)) || freeStamp)
                prompt = target is EdgeFalling ? "按 " + key + " 跳下" : key.ToString();
        }
        if (taskDescriptionText != null) taskDescriptionText.text = description;
        if (interactionPromptText != null) interactionPromptText.text = prompt;
    }

    private int CurrentTaskIndex()
    {
        for (int i = 0; i < gameState.taskCompleted.Count; i++)
            if (!gameState.taskCompleted[i]) return i;
        return -1;
    }

    void OnDisable()
    {
        ClearCompletionFeedback();
        if (startPromptText != null) startPromptText.gameObject.SetActive(false);
        if (gameState != null) gameState.OnTaskStateChanged -= RefreshStatus;
        foreach (UIInkWellPrompt prompt in inkWellPrompts)
            if (prompt != null) prompt.enabled = false;
    }

    void OnDestroy()
    {
        ClearCompletionFeedback();
        if (feedbackLayer != null) Destroy(feedbackLayer.gameObject);
        if (startPromptText != null) Destroy(startPromptText.gameObject);
        foreach (UIInkWellPrompt prompt in inkWellPrompts)
            if (prompt != null) Destroy(prompt.gameObject);
        inkWellPrompts.Clear();
    }
}
