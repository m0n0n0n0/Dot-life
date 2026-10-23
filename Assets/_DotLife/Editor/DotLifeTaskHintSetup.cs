using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DotLifeTaskHintSetup
{
    private const string ScenePath = "Assets/_DotLife/Scenes/desk.unity";

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += ConnectOpenScene;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => Connect(scene);

    [MenuItem("Tools/Dot Life/Create Missing Task Hints")]
    private static void ConnectOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ConnectOpenScene;
            return;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++) Connect(SceneManager.GetSceneAt(i));
    }

    private static void Connect(Scene scene)
    {
        if (!scene.isLoaded || scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) return;
        TaskStatusBar hud = null;
        GameState state = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (hud == null) hud = root.GetComponentInChildren<TaskStatusBar>(true);
            if (state == null) state = root.GetComponentInChildren<GameState>(true);
        }
        if (hud == null || state == null) return;
        TriggerBus bus = state.GetComponent<TriggerBus>();
        if (bus == null) return;
        var hudData = new SerializedObject(hud);
        Text hudText = hudData.FindProperty("taskDescriptionText").objectReferenceValue as Text;
        if (hudText == null || hudText.font == null) return;

        bool changed = false;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create task scene hints");
        foreach (DotDrawingHandler task in bus.taskTriggers)
        {
            if (task == null || task.TaskId == DemoTaskId.Unassigned) continue;
            Transform parent = task.transform;
            while (parent.parent != null && parent.parent.name != "Tasks") parent = parent.parent;
            bool exists = false;
            foreach (var hint in parent.GetComponentsInChildren<UITaskSceneHint>(true))
                if (hint.TaskId == task.TaskId) { exists = true; break; }
            if (exists) continue;
            Renderer surface = task.GetComponent<Renderer>();
            if (surface == null) continue;

            var node = new GameObject("TaskHint_" + task.TaskId, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(node, "Create task scene hint");
            SceneManager.MoveGameObjectToScene(node, scene);
            node.transform.SetParent(parent, false);
            node.layer = 5;
            Canvas canvas = node.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            RectTransform rect = node.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0f);
            PlaceAtEdge(rect, surface);

            var child = new GameObject("Description", typeof(RectTransform), typeof(Text), typeof(Outline));
            child.transform.SetParent(node.transform, false);
            child.layer = 5;
            Text label = child.GetComponent<Text>();
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = hudText.font;
            label.fontSize = 64;
            label.alignment = TextAnchor.LowerCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 48;
            label.resizeTextMaxSize = 64;
            label.lineSpacing = 1.1f;
            label.color = new Color(0.07f, 0.075f, 0.085f, 1f);
            label.raycastTarget = false;
            Outline outline = child.GetComponent<Outline>();
            outline.effectColor = new Color(0.98f, 0.96f, 0.87f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);
            var component = node.AddComponent<UITaskSceneHint>();
            component.Bind(task, state, hud, label);
            EditorUtility.SetDirty(component);
            changed = true;
        }
        Undo.CollapseUndoOperations(undoGroup);
        if (changed) EditorSceneManager.MarkSceneDirty(scene);
    }

    // Only called for newly created hints. Existing, manually positioned hints are never moved.
    private static void PlaceAtEdge(RectTransform rect, Renderer surface)
    {
        Bounds bounds = surface.localBounds;
        Transform model = surface.transform;
        Vector3 center = model.TransformPoint(bounds.center);
        Vector3 x = model.TransformVector(Vector3.right * bounds.extents.x);
        Vector3 y = model.TransformVector(Vector3.up * bounds.extents.y);
        Vector3 z = model.TransformVector(Vector3.forward * bounds.extents.z);
        Vector3 right = Vector3.ProjectOnPlane(model.right, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.01f) right = Vector3.right;
        Vector3 forward = Vector3.Cross(right, Vector3.up).normalized;
        Vector3 anchor = center + forward * (Extent(forward, x, y, z) - 0.15f);
        anchor.y = center.y + Extent(Vector3.up, x, y, z) + 0.25f;
        if (Camera.main != null && Vector3.Dot(forward, Camera.main.transform.position - anchor) > 0f) forward = -forward;
        rect.SetPositionAndRotation(anchor, Quaternion.LookRotation(forward, Vector3.up));
        const float scale = 0.42f / 64f;
        Vector3 parentScale = rect.parent.lossyScale;
        rect.localScale = new Vector3(scale / SafeScale(parentScale.x), scale / SafeScale(parentScale.y), scale / SafeScale(parentScale.z));
        rect.sizeDelta = new Vector2(Mathf.Max(1f, Extent(right, x, y, z) * 1.8f) / scale, 64f * 3.2f);
    }

    private static float SafeScale(float value) => Mathf.Abs(value) < 0.001f ? 0.001f : value;
    private static float Extent(Vector3 axis, Vector3 x, Vector3 y, Vector3 z) =>
        Mathf.Abs(Vector3.Dot(axis, x)) + Mathf.Abs(Vector3.Dot(axis, y)) + Mathf.Abs(Vector3.Dot(axis, z));
}
