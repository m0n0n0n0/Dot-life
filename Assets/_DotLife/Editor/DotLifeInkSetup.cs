using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DotLifeInkSetup
{
    private const string WellPath = "Assets/_DotLife/Interactable/Prefabs/PF_InkWell.prefab";
    private const string MeterPath = "Assets/_DotLife/UI/Prefabs/PF_UI_PlayerInk.prefab";

    [MenuItem("Tools/Dot Life/Connect Ink B1")]
    public static void Connect()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before connecting ink.");
        if (EditorSceneManager.GetActiveScene().path != "Assets/_DotLife/Scenes/desk.unity")
            throw new InvalidOperationException("Open desk before connecting ink.");
        BasicBehaviourScript player = UnityEngine.Object.FindObjectOfType<BasicBehaviourScript>();
        GameState game = UnityEngine.Object.FindObjectOfType<GameState>();
        TriggerBus bus = UnityEngine.Object.FindObjectOfType<TriggerBus>();
        TaskStatusBar hud = UnityEngine.Object.FindObjectOfType<TaskStatusBar>();
        GameObject model = GameObject.Find("ink_low");
        if (player == null || game == null || bus == null || hud == null || model == null)
            throw new InvalidOperationException("Missing desk player, game, HUD or ink_low.");
        var poses = new Dictionary<Transform, Matrix4x4>();
        foreach (Transform node in UnityEngine.Object.FindObjectsOfType<Transform>()) poses.Add(node, node.localToWorldMatrix);
        var colliders = new Dictionary<Collider, string>();
        foreach (Collider collider in UnityEngine.Object.FindObjectsOfType<Collider>()) colliders.Add(collider, EditorJsonUtility.ToJson(collider));
        var renderers = new Dictionary<Renderer, string>();
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsOfType<Renderer>()) renderers.Add(renderer, EditorJsonUtility.ToJson(renderer));
        PlayerInk ink = player.GetComponent<PlayerInk>();
        if (ink == null) ink = Undo.AddComponent<PlayerInk>(player.gameObject);
        var inkData = new SerializedObject(ink);
        inkData.FindProperty("gameState").objectReferenceValue = game;
        inkData.FindProperty("diary").objectReferenceValue = bus.taskTriggers.Find(task => task.TaskId == DemoTaskId.Diary);
        inkData.ApplyModifiedProperties();
        InkWell well = model.GetComponent<InkWell>();
        if (well == null)
        {
            well = Undo.AddComponent<InkWell>(model);
            BoxCollider trigger = model.GetComponent<BoxCollider>();
            Bounds localBounds = ModelBounds(model.transform);
            Vector3 scale = model.transform.lossyScale;
            trigger.isTrigger = true;
            trigger.center = localBounds.center;
            trigger.size = localBounds.size + new Vector3(1.5f / Mathf.Abs(scale.x), 2f / Mathf.Abs(scale.y), 1.5f / Mathf.Abs(scale.z));
            EditorUtility.SetDirty(trigger);
        }
        if (!bus.inkWells.Contains(well)) { Undo.RecordObject(bus, "Connect ink well"); bus.inkWells.Add(well); }
        EnsureFolder("Assets/_DotLife/Interactable", "Prefabs");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(WellPath) == null)
        {
            GameObject clone = UnityEngine.Object.Instantiate(model);
            try
            {
                clone.name = "PF_InkWell";
                clone.transform.SetParent(null);
                clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                clone.transform.localScale = model.transform.lossyScale;
                PrefabUtility.SaveAsPrefabAsset(clone, WellPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }
        GameObject meter = AssetDatabase.LoadAssetAtPath<GameObject>(MeterPath);
        if (meter == null)
        {
            GameObject root = new GameObject("PF_UI_PlayerInk", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(CanvasGroup), typeof(InkMeterGraphic), typeof(UIPlayerInk));
            try
            {
                root.layer = 5;
                RectTransform rect = root.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
                rect.sizeDelta = new Vector2(12f, 80f);
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = group.blocksRaycasts = false;
                root.GetComponent<InkMeterGraphic>().raycastTarget = false;
                meter = PrefabUtility.SaveAsPrefabAsset(root, MeterPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        var hudData = new SerializedObject(hud);
        hudData.FindProperty("inkMeterPrefab").objectReferenceValue = meter.GetComponent<UIPlayerInk>();
        hudData.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(ink);
        PrefabUtility.RecordPrefabInstancePropertyModifications(well);
        EditorUtility.SetDirty(bus);
        foreach (var pose in poses)
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(pose.Key.localToWorldMatrix[i] - pose.Value[i]) > 0.00001f)
                    throw new InvalidOperationException("Existing transform changed: " + pose.Key.name);
        foreach (var collider in colliders)
            if (EditorJsonUtility.ToJson(collider.Key) != collider.Value)
                throw new InvalidOperationException("Existing collider changed: " + collider.Key.name);
        foreach (var renderer in renderers)
            if (EditorJsonUtility.ToJson(renderer.Key) != renderer.Value)
                throw new InvalidOperationException("Existing renderer changed: " + renderer.Key.name);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("INK_SETUP PASS existing transforms, renderers and physical colliders unchanged; one ink well and meter connected.");
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use Connect for interactive scene setup.");
        EditorSceneManager.OpenScene("Assets/_DotLife/Scenes/desk.unity");
        Connect();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "B1DeskConnected.unity"),
            File.ReadAllText("Assets/_DotLife/Scenes/desk.unity"));
        EditorApplication.Exit(0);
    }

    private static Bounds ModelBounds(Transform root)
    {
        Bounds bounds = default;
        bool first = true;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            Bounds world = renderer.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = root.InverseTransformPoint(world.center + Vector3.Scale(world.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f)));
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
        }
        if (first) throw new InvalidOperationException("ink_low has no model renderers.");
        return bounds;
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }
}
