using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering.Universal;

public static class DotLifeToonSetup
{
    public const string DotOriginal = "Assets/Art/Characters/Dot/Materials/MAT_Dot.mat";
    public const string InkOriginal = "Assets/Art/Environments/Desk/Materials/MAT_Ink.mat";
    public const string DotToon = "Assets/Art/Characters/Dot/Materials/MAT_Dot_Toon.mat";
    public const string InkToon = "Assets/Art/Environments/Desk/Materials/MAT_Ink_Toon.mat";
    public const string BottleOriginal = "Assets/Art/Environments/Desk/Materials/MAT_Bottle.mat";
    public const string BottleToon = "Assets/Art/Environments/Desk/Materials/MAT_Bottle_Toon.mat";
    public const string RendererPath = "Assets/Settings/URP_DotLife_Renderer.asset";
    public const string OutlineFeatureName = "DotLife_ToonOutline";
    private const string Desk = "Assets/_DotLife/Scenes/desk.unity";

    [MenuItem("Tools/Dot Life/Toon Trial/Apply Toon To Open Desk")]
    public static void Apply() { Switch(true); }

    [MenuItem("Tools/Dot Life/Toon Trial/Restore Lit In Open Desk")]
    public static void Restore() { Switch(false); }

    [MenuItem("Tools/Dot Life/Toon Trial/Enable Character And Bottle Outline")]
    public static void EnableOutline()
    {
        Apply();
        EnsureOutlineFeature();
        SetOutline(true);
    }

    [MenuItem("Tools/Dot Life/Toon Trial/Disable Character And Bottle Outline")]
    public static void DisableOutline() { CheckDesk(); SetOutline(false); }

    private static void CheckDesk()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling
            || EditorSceneManager.GetActiveScene().path != Desk)
            throw new InvalidOperationException("Open desk, stop Play and wait for compilation before switching materials.");
    }

    private static void Switch(bool toon)
    {
        CheckDesk();
        Material dot = Load(DotOriginal), ink = Load(InkOriginal), bottle = Load(BottleOriginal);
        Material dotToon = toon ? CreateSample(DotOriginal, DotToon, false) : AssetDatabase.LoadAssetAtPath<Material>(DotToon);
        Material inkToon = toon ? CreateSample(InkOriginal, InkToon, true) : AssetDatabase.LoadAssetAtPath<Material>(InkToon);
        Material bottleToon = toon ? CreateSample(BottleOriginal, BottleToon, false) : AssetDatabase.LoadAssetAtPath<Material>(BottleToon);
        var targets = new HashSet<Renderer>();
        var scene = EditorSceneManager.GetActiveScene();
        foreach (BasicBehaviourScript player in UnityEngine.Object.FindObjectsOfType<BasicBehaviourScript>(true))
            if (player.gameObject.scene == scene)
                foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true)) targets.Add(renderer);
        foreach (InkWell well in UnityEngine.Object.FindObjectsOfType<InkWell>(true))
            if (well.gameObject.scene == scene)
                foreach (Renderer renderer in well.GetComponentsInChildren<Renderer>(true)) targets.Add(renderer);
        foreach (Transform item in UnityEngine.Object.FindObjectsOfType<Transform>(true))
            if (item.gameObject.scene == scene && item.name == "bottle_low")
                foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true)) targets.Add(renderer);
        int changed = 0;
        foreach (Renderer renderer in targets)
        {
            Material[] materials = renderer.sharedMaterials;
            bool dirty = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material replacement = null;
                if (toon && materials[i] == dot) replacement = dotToon;
                else if (toon && materials[i] == ink) replacement = inkToon;
                else if (toon && materials[i] == bottle) replacement = bottleToon;
                else if (!toon && dotToon != null && materials[i] == dotToon) replacement = dot;
                else if (!toon && inkToon != null && materials[i] == inkToon) replacement = ink;
                else if (!toon && bottleToon != null && materials[i] == bottleToon) replacement = bottle;
                if (replacement == null) continue;
                materials[i] = replacement;
                dirty = true;
                changed++;
            }
            if (!dirty) continue;
            Undo.RecordObject(renderer, "Switch Dot Life Toon Trial");
            renderer.sharedMaterials = materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        if (changed > 0) EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("TOON_SETUP " + (toon ? "Toon" : "Lit") + "; material slots changed=" + changed
            + "; transforms, lights, colliders and original materials untouched. Save desk to keep the selection.");
    }

    private static void SetOutline(bool enabled)
    {
        foreach (string path in new[] { DotToon, BottleToon })
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) continue;
            Undo.RecordObject(material, "Toggle Dot Life Outline");
            material.SetFloat("_OutlineEnabled", enabled ? 1f : 0f);
            material.DisableKeyword("_OUTLINEENABLED_ON");
            material.DisableKeyword("_THREEBANDS_ON");
            material.DisableKeyword("_ALPHACLIP_ON");
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
    }

    public static void EnsureOutlineFeature()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (data == null) throw new InvalidOperationException("Dot Life renderer asset is missing.");
        foreach (ScriptableRendererFeature existing in data.rendererFeatures)
            if (existing != null && existing.name == OutlineFeatureName)
            {
                if (!(existing is RenderObjects)) throw new InvalidOperationException("Outline feature name is already used.");
                StoreFeatureMap(data);
                if (!existing.isActive)
                {
                    Undo.RecordObject(existing, "Enable Dot Life Outline Feature");
                    existing.SetActive(true);
                    data.SetDirty();
                    EditorUtility.SetDirty(existing);
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                }
                return;
            }
        var feature = ScriptableObject.CreateInstance<RenderObjects>();
        feature.name = OutlineFeatureName;
        feature.settings.passTag = OutlineFeatureName;
        feature.settings.Event = RenderPassEvent.AfterRenderingSkybox;
        feature.settings.filterSettings.LayerMask = ~0;
        feature.settings.filterSettings.PassNames = new[] { "DotLifeOutline" };
        feature.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
        Undo.RecordObject(data, "Add Dot Life Outline Feature");
        AssetDatabase.AddObjectToAsset(feature, data);
        Undo.RegisterCreatedObjectUndo(feature, "Add Dot Life Outline Feature");
        data.rendererFeatures.Add(feature);
        StoreFeatureMap(data);
        feature.Create();
        data.SetDirty();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
    }

    internal static void StoreFeatureMap(UniversalRendererData data)
    {
        var serialized = new SerializedObject(data);
        SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
        map.arraySize = data.rendererFeatures.Count;
        for (int i = 0; i < data.rendererFeatures.Count; i++)
        {
            long id = 0;
            if (data.rendererFeatures[i] != null)
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string _, out id);
            map.GetArrayElementAtIndex(i).longValue = id;
        }
        if (serialized.ApplyModifiedProperties())
        {
            data.SetDirty();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }
    }

    private static Material CreateSample(string source, string destination, bool ink)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(destination);
        if (existing != null) return existing;
        Shader shader = Shader.Find("DotLife/Toon Lit");
        if (shader == null) throw new InvalidOperationException("DotLife/Toon Lit shader is missing.");
        var material = new Material(Load(source)) { shader = shader, name = System.IO.Path.GetFileNameWithoutExtension(destination) };
        material.shaderKeywords = Array.Empty<string>();
        material.SetOverrideTag("RenderType", "Opaque");
        material.renderQueue = -1;
        bool bottle = destination == BottleToon;
        material.SetFloat("_BumpScale", ink || bottle ? 0.15f : 0.25f);
        material.SetFloat("_ThreeBands", 1f);
        material.SetColor("_ShadeColor", new Color(0.35f, 0.4f, 0.55f));
        material.SetColor("_MidtoneColor", new Color(0.7f, 0.75f, 0.85f));
        material.SetColor("_LitColor", Color.white);
        material.SetFloat("_ShadeThreshold", 0.3f);
        material.SetFloat("_LightThreshold", 0.7f);
        material.SetFloat("_BandSoftness", 0.035f);
        material.SetFloat("_DirectStrength", 1.2f);
        material.SetFloat("_AmbientStrength", 0.55f);
        material.SetFloat("_FillStrength", 0.45f);
        material.SetColor("_SpecColor", new Color(1f, 0.98f, 0.9f));
        material.SetFloat("_SpecularStrength", ink ? 0.08f : bottle ? 0.06f : 0.04f);
        material.SetFloat("_SpecularThreshold", ink ? 0.94f : bottle ? 0.95f : 0.96f);
        material.SetFloat("_OutlineEnabled", 0f);
        material.SetFloat("_OutlineWidth", 2.5f);
        material.SetColor("_OutlineColor", new Color(0.035f, 0.035f, 0.045f));
        AssetDatabase.CreateAsset(material, destination);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Material Load(string path)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) throw new InvalidOperationException("Missing original material: " + path);
        return material;
    }
}
