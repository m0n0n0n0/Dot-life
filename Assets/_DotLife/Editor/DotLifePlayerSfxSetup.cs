using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DotLifePlayerSfxSetup
{
    public const string SettingsPath = "Assets/_DotLife/Audio/SFX_Player.asset";
    private const string ScenePath = "Assets/_DotLife/Scenes/desk.unity";

    [MenuItem("Tools/Dot Life/Connect Player SFX")]
    public static void Connect()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Open desk and stop Play mode before connecting player SFX.");
        var players = UnityEngine.Object.FindObjectsOfType<BasicBehaviourScript>();
        if (players.Length != 1 || players[0].GetComponent<PlayerInk>() == null)
            throw new InvalidOperationException("Expected one player with PlayerInk.");
        Transform audio = null;
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "_Systems") audio = root.transform.Find("Audio");
        if (audio == null) throw new InvalidOperationException("Missing _Systems/Audio group.");
        var legacy = players[0].GetComponent<PlayerSfx>();
        Transform node = audio.Find("PlayerSfxManager");
        PlayerSfx sfx = node != null ? node.GetComponent<PlayerSfx>() : null;
        if (node != null && sfx == null)
            throw new InvalidOperationException("PlayerSfxManager exists without a PlayerSfx component.");
        var settings = sfx != null ? ReadSettings(sfx) : null;
        if (settings == null && legacy != null) settings = ReadSettings(legacy);
        if (settings == null) settings = AssetDatabase.LoadAssetAtPath<PlayerSfxSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<PlayerSfxSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        if (node == null)
        {
            var manager = new GameObject("PlayerSfxManager");
            Undo.RegisterCreatedObjectUndo(manager, "Create player SFX manager");
            Undo.SetTransformParent(manager.transform, audio, "Group player SFX manager");
            manager.transform.localPosition = Vector3.zero;
            manager.transform.localRotation = Quaternion.identity;
            manager.transform.localScale = Vector3.one;
            sfx = Undo.AddComponent<PlayerSfx>(manager);
        }
        var data = new SerializedObject(sfx);
        data.FindProperty("motor").objectReferenceValue = players[0];
        data.FindProperty("ink").objectReferenceValue = players[0].GetComponent<PlayerInk>();
        data.FindProperty("settings").objectReferenceValue = settings;
        data.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(sfx);
        var relay = players[0].GetComponent<PlayerSfxEvents>();
        if (relay == null) relay = Undo.AddComponent<PlayerSfxEvents>(players[0].gameObject);
        var events = new SerializedObject(relay);
        events.FindProperty("output").objectReferenceValue = sfx;
        events.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(relay);
        if (legacy != null) Undo.DestroyObjectImmediate(legacy);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("PLAYER_SFX_SETUP PASS _Systems/Audio/PlayerSfxManager connected; original settings preserved.");
    }

    private static PlayerSfxSettings ReadSettings(PlayerSfx sfx) =>
        new SerializedObject(sfx).FindProperty("settings").objectReferenceValue as PlayerSfxSettings;

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use Connect in the interactive editor.");
        EditorApplication.update += FinishBatch;
    }

    private static void FinishBatch()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= FinishBatch;
        try
        {
            EditorSceneManager.OpenScene(ScenePath);
            Connect();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
