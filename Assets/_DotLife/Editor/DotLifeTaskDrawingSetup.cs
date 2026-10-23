using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DotLifeTaskDrawingSetup
{
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += ConnectOpenScenes;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => Connect(scene);

    [MenuItem("Tools/Dot Life/Connect Task Free Order")]
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
        if (!scene.isLoaded || scene.path != "Assets/_DotLife/Scenes/desk.unity"
            || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_DotLife/Rendering/TaskFrameDelta.shader");
        if (shader == null) return;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (DotDrawingHandler task in root.GetComponentsInChildren<DotDrawingHandler>(true))
        {
            if (task.TaskId == DemoTaskId.Unassigned) continue;
            var data = new SerializedObject(task);
            SerializedProperty field = data.FindProperty("frameCompositeShader");
            if (field.objectReferenceValue != null) continue;
            field.objectReferenceValue = shader;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(task);
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
