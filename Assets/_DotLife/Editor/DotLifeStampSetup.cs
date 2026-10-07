using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DotLifeStampSetup
{
    private const string Folder = "Assets/_DotLife/Player/Ink";
    public const string SettingsPath = Folder + "/INK_Default.asset";

    [MenuItem("Tools/Dot Life/Connect Ink B2")]
    public static void Connect()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != "Assets/_DotLife/Scenes/desk.unity")
            throw new InvalidOperationException("Open desk and stop Play mode before connecting stamps.");
        var player = UnityEngine.Object.FindObjectOfType<BasicBehaviourScript>();
        var bus = UnityEngine.Object.FindObjectOfType<TriggerBus>();
        if (player == null || bus == null) throw new InvalidOperationException("Missing player or TriggerBus.");
        var poses = new Dictionary<Transform, Matrix4x4>();
        foreach (Transform t in UnityEngine.Object.FindObjectsOfType<Transform>()) poses.Add(t, t.localToWorldMatrix);
        var physics = new Dictionary<Collider, string>();
        foreach (Collider c in UnityEngine.Object.FindObjectsOfType<Collider>()) physics.Add(c, EditorJsonUtility.ToJson(c));
        var visuals = new Dictionary<Renderer, string>();
        foreach (Renderer r in UnityEngine.Object.FindObjectsOfType<Renderer>()) visuals.Add(r, EditorJsonUtility.ToJson(r));
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_DotLife/Player", "Ink");
        string texturePath = "Assets/Art/Environments/Desk/Textures/T_InkStamp.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) == null)
        {
            var pattern = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            try
            {
                for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / 64f - 1f, (y + 0.5f) / 64f - 1f);
                    float angle = Mathf.Atan2(p.y, p.x);
                    float radius = 0.82f + 0.025f * Mathf.Sin(angle * 3f + 1f)
                        + 0.018f * Mathf.Sin(angle * 7f) + 0.013f * Mathf.Cos(angle * 11f);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radius - p.magnitude) / 0.06f));
                    alpha *= 0.93f + 0.07f * Mathf.PerlinNoise(x * 0.17f, y * 0.17f);
                    pattern.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                pattern.Apply();
                File.WriteAllBytes(texturePath, pattern.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(pattern); }
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 128;
            importer.SaveAndReimport();
        }
        string materialPath = "Assets/Art/Environments/Desk/Materials/MAT_InkStamp.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_DotLife/Player/InkStamp.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Ink stamp shader failed.");
            material = new Material(shader);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", new Color(0.025f, 0.04f, 0.055f, 0.94f));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        var settings = AssetDatabase.LoadAssetAtPath<InkStampSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<InkStampSettings>();
            settings.material = material;
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        var stamping = player.GetComponent<InkStamping>();
        if (stamping == null) stamping = Undo.AddComponent<InkStamping>(player.gameObject);
        var data = new SerializedObject(stamping);
        data.FindProperty("settings").objectReferenceValue = settings;
        data.FindProperty("trigger").objectReferenceValue = bus;
        data.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(stamping);
        foreach (var pose in poses) for (int i = 0; i < 16; i++)
            if (Mathf.Abs(pose.Key.localToWorldMatrix[i] - pose.Value[i]) > 0.00001f)
                throw new InvalidOperationException("Existing transform changed: " + pose.Key.name);
        foreach (var item in physics) if (EditorJsonUtility.ToJson(item.Key) != item.Value)
            throw new InvalidOperationException("Existing collider changed: " + item.Key.name);
        foreach (var item in visuals) if (EditorJsonUtility.ToJson(item.Key) != item.Value)
            throw new InvalidOperationException("Existing renderer changed: " + item.Key.name);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("STAMP_SETUP PASS existing transforms, renderers and colliders unchanged; B2 connected.");
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use Connect in the interactive editor.");
        EditorSceneManager.OpenScene("Assets/_DotLife/Scenes/desk.unity");
        Connect();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }
}
