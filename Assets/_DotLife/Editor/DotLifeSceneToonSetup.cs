using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class DotLifeSceneToonSetup
{
    public const string OutlineMaterialPath = "Assets/Settings/MAT_SceneOutline.mat";
    public const string SceneFeatureName = "DotLife_SceneEdges";
    public const string DefaultOriginal = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Lit.mat";
    public const string DefaultToon = "Assets/Art/Environments/Desk/Materials/MAT_DefaultSurface_Toon.mat";
    private const string ToonShader = "DotLife/Toon Lit";
    private const string Desk = "Assets/_DotLife/Scenes/desk.unity";

    [MenuItem("Tools/Dot Life/Scene Style/Apply Toon And Outlines")]
    public static void Apply()
    {
        Switch(true);
        EnableOutlines();
    }

    [MenuItem("Tools/Dot Life/Scene Style/Apply Toon Only")]
    public static void ApplyToonOnly() { Switch(true); DisableOutlines(); }

    [MenuItem("Tools/Dot Life/Scene Style/Restore Original Lit")]
    public static void Restore() { Switch(false); SetFeatures(false); }

    [MenuItem("Tools/Dot Life/Scene Style/Enable Outlines")]
    public static void EnableOutlines()
    {
        CheckDesk();
        DotLifeToonSetup.EnsureOutlineFeature();
        EnsureSceneFeature();
        SetMaterialOutlines(true);
        SetFeatures(true);
    }

    [MenuItem("Tools/Dot Life/Scene Style/Disable Outlines")]
    public static void DisableOutlines() { CheckDesk(); SetMaterialOutlines(false); SetFeatures(false); }

    private static void CheckDesk()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorSceneManager.GetActiveScene().path != Desk)
            throw new InvalidOperationException("Open desk, stop Play and wait for compilation before changing the scene style.");
    }

    public static string OriginalPath(Material material)
    {
        string path = AssetDatabase.GetAssetPath(material);
        if (path == DefaultToon) return DefaultOriginal;
        return path.EndsWith("_Toon.mat", StringComparison.Ordinal) ? path.Substring(0, path.Length - 9) + ".mat" : path;
    }

    private static void Switch(bool toon)
    {
        CheckDesk();
        var scene = EditorSceneManager.GetActiveScene();
        int slots = 0;
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsOfType<Renderer>(true))
        {
            if (renderer.gameObject.scene != scene || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
            Material[] materials = renderer.sharedMaterials;
            bool dirty = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null) throw new InvalidOperationException("Missing scene material: " + renderer.name);
                string path = AssetDatabase.GetAssetPath(material);
                if (material.shader.name == "DotLife/Ink Stamp") continue;
                if (!path.StartsWith("Assets/Art/", StringComparison.Ordinal) && path != DefaultOriginal)
                    throw new InvalidOperationException("Review non-Art scene material before conversion: " + renderer.name + ": " + path);
                Material replacement = material;
                if (toon && material.shader.name == "Universal Render Pipeline/Lit") replacement = Create(material, path);
                else if (!toon && material.shader.name == ToonShader)
                {
                    replacement = AssetDatabase.LoadAssetAtPath<Material>(OriginalPath(material));
                    if (replacement == null) throw new InvalidOperationException("Original material missing: " + path);
                }
                else if (material.shader.name != ToonShader && material.shader.name != "Universal Render Pipeline/Lit")
                    throw new InvalidOperationException("Unsupported scene shader: " + path + ": " + material.shader.name);
                if (replacement == material) continue;
                materials[i] = replacement;
                slots++; dirty = true;
            }
            if (!dirty) continue;
            Undo.RecordObject(renderer, "Change Dot Life Scene Style");
            renderer.sharedMaterials = materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        if (slots > 0) EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("SCENE_TOON_SETUP " + (toon ? "Toon" : "Original Lit") + "; slots changed=" + slots
            + "; original assets, transforms, colliders, lights and gameplay unchanged. Save desk to keep the selection.");
    }

    private static Material Create(Material original, string source)
    {
        string destination = source == DefaultOriginal ? DefaultToon : Path.ChangeExtension(source, null) + "_Toon.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(destination);
        if (existing != null)
        {
            if (existing.shader.name != ToonShader) throw new InvalidOperationException("Toon material path already occupied: " + destination);
            return existing;
        }
        Shader shader = Shader.Find(ToonShader);
        if (shader == null) throw new InvalidOperationException("Toon shader is missing.");
        var material = new Material(original) { shader = shader, name = Path.GetFileNameWithoutExtension(destination) };
        material.shaderKeywords = Array.Empty<string>();
        bool transparent = original.HasProperty("_Surface") && original.GetFloat("_Surface") > 0.5f;
        material.SetFloat("_Surface", transparent ? 1 : 0);
        material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
        material.renderQueue = original.renderQueue;
        material.SetFloat("_PremultiplyAlpha", transparent && original.GetFloat("_SrcBlend") == (float)BlendMode.One ? 1 : 0);
        material.SetShaderPassEnabled("ShadowCaster", !transparent && original.GetShaderPassEnabled("ShadowCaster"));
        material.SetShaderPassEnabled("DepthOnly", !transparent && original.GetShaderPassEnabled("DepthOnly"));
        material.SetShaderPassEnabled("DepthNormals", !transparent);
        material.SetFloat("_ThreeBands", 1);
        material.SetFloat("_ShadeThreshold", 0.3f);
        material.SetFloat("_LightThreshold", 0.7f);
        material.SetFloat("_BandSoftness", 0.035f);
        material.SetColor("_LitColor", Color.white);
        material.SetFloat("_DirectStrength", 1.2f);
        material.SetFloat("_AmbientStrength", 0.55f);
        material.SetFloat("_FillStrength", 0.45f);
        material.SetColor("_SpecColor", new Color(1, 0.98f, 0.9f));
        material.SetFloat("_SpecularThreshold", 0.95f);
        material.SetColor("_OutlineColor", new Color(0.035f, 0.035f, 0.045f));
        material.SetFloat("_OutlineEnabled", 1);

        string name = original.name;
        bool room = source.StartsWith("Assets/Art/Environments/Room/", StringComparison.Ordinal);
        bool desk = name == "MAT_Deskwood" || name == "MAT_Deskwhite";
        bool paper = source == DefaultOriginal || name.Contains("book") || name.Contains("Diary") || name.Contains("Math")
            || name.Contains("Yuwen") || name.Contains("Photo") || name.Contains("Paper");
        bool metal = name.Contains("Jiandao") || name.Contains("Tablelamp") || name.Contains("Phone");
        if (room || desk || paper)
        {
            material.SetColor("_ShadeColor", new Color(0.62f, 0.65f, 0.72f));
            material.SetColor("_MidtoneColor", new Color(0.83f, 0.86f, 0.9f));
            material.SetFloat("_BumpScale", room ? 0 : 0.08f);
            material.SetFloat("_SpecularStrength", 0.015f);
            // Single-sided room meshes use scene edges; inverted hulls can cover the view from their back side.
            material.SetFloat("_OutlineWidth", room ? 0 : desk ? 0.85f : 1.1f);
        }
        else
        {
            material.SetColor("_ShadeColor", new Color(0.35f, 0.4f, 0.55f));
            material.SetColor("_MidtoneColor", new Color(0.7f, 0.75f, 0.85f));
            material.SetFloat("_BumpScale", metal ? 0.1f : 0.15f);
            material.SetFloat("_SpecularStrength", metal ? 0.14f : 0.06f);
            material.SetFloat("_SpecularThreshold", metal ? 0.92f : 0.95f);
            material.SetFloat("_OutlineWidth", metal ? 1.6f : 2f);
        }
        AssetDatabase.CreateAsset(material, destination);
        return material;
    }

    private static void SetMaterialOutlines(bool enabled)
    {
        // Fresh imports can rebuild prefab renderer proxies; address the shared asset rather than that transient list.
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Art" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith("_Toon.mat", StringComparison.Ordinal)) continue;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader.name != ToonShader) continue;
            Undo.RecordObject(material, "Toggle Dot Life Scene Outlines");
            material.SetFloat("_OutlineEnabled", enabled ? 1 : 0);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
    }

    private static void EnsureSceneFeature()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(DotLifeToonSetup.RendererPath);
        foreach (ScriptableRendererFeature feature in data.rendererFeatures)
            if (feature != null && feature.name == SceneFeatureName)
            {
                if (!(feature is FullScreenPassRendererFeature)) throw new InvalidOperationException("Scene outline feature name is occupied.");
                return;
            }
        Material material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("DotLife/Scene Outline");
            if (shader == null) throw new InvalidOperationException("Scene outline shader is missing.");
            material = new Material(shader) { name = "MAT_SceneOutline" };
            AssetDatabase.CreateAsset(material, OutlineMaterialPath);
        }
        var edges = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
        edges.name = SceneFeatureName;
        edges.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
        edges.fetchColorBuffer = false;
        edges.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
        edges.passMaterial = material;
        Undo.RecordObject(data, "Add Dot Life Scene Outline Feature");
        AssetDatabase.AddObjectToAsset(edges, data);
        Undo.RegisterCreatedObjectUndo(edges, "Add Dot Life Scene Outline Feature");
        data.rendererFeatures.Add(edges);
        DotLifeToonSetup.StoreFeatureMap(data);
        edges.Create(); data.SetDirty(); EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
    }

    private static void SetFeatures(bool enabled)
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(DotLifeToonSetup.RendererPath);
        foreach (ScriptableRendererFeature feature in data.rendererFeatures)
            if (feature != null && (feature.name == SceneFeatureName || feature.name == DotLifeToonSetup.OutlineFeatureName))
            {
                Undo.RecordObject(feature, "Toggle Dot Life Scene Style Feature");
                feature.SetActive(enabled); EditorUtility.SetDirty(feature);
            }
        data.SetDirty(); EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
    }
}
