using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DotLifePlayerEntranceSetup
{
    private const string ScenePath = "Assets/_DotLife/Scenes/desk.unity";

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += ConnectOpenScenes;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => Connect(scene);

    [MenuItem("Tools/Dot Life/Connect Player Entrance")]
    private static void ConnectOpenScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ConnectOpenScenes;
            return;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++) Connect(SceneManager.GetSceneAt(i));
    }

    private static void Connect(Scene scene)
    {
        if (!scene.isLoaded || scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) return;
        BasicBehaviourScript player = null;
        DotDrawingHandler diary = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (player == null) player = root.GetComponentInChildren<BasicBehaviourScript>(true);
            foreach (var task in root.GetComponentsInChildren<DotDrawingHandler>(true))
                if (task.TaskId == DemoTaskId.Diary) diary = task;
        }
        if (player == null || diary == null) return;
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller == null) return;

        PlayerEntrance entrance = player.GetComponent<PlayerEntrance>();
        if (entrance != null && new SerializedObject(entrance).FindProperty("spawnPoint").objectReferenceValue != null) return;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Connect player entrance");
        if (entrance == null) entrance = Undo.AddComponent<PlayerEntrance>(player.gameObject);
        Transform parent = diary.transform;
        Transform candidate = parent;
        while (candidate.parent != null && candidate.parent.name != "Tasks") candidate = candidate.parent;
        if (candidate.parent != null) parent = candidate;
        Transform point = parent.Find("PlayerEntrancePoint");
        if (point == null)
        {
            var node = new GameObject("PlayerEntrancePoint");
            Undo.RegisterCreatedObjectUndo(node, "Create player entrance point");
            SceneManager.MoveGameObjectToScene(node, scene);
            node.transform.SetParent(parent, false);
            // Start at the existing player footprint; never move authored scene objects.
            Bounds body = controller.bounds;
            node.transform.position = body.size.sqrMagnitude > 0f
                ? new Vector3(body.center.x, body.min.y, body.center.z)
                : player.transform.TransformPoint(controller.center + Vector3.down * controller.height * 0.5f);
            point = node.transform;
        }
        var data = new SerializedObject(entrance);
        data.FindProperty("spawnPoint").objectReferenceValue = point;
        data.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(entrance);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
    }
}
