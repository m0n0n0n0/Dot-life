using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerSfx))]
public class PlayerSfxEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        using (new EditorGUI.DisabledScope(Application.isPlaying))
            DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        var settings = serializedObject.FindProperty("settings").objectReferenceValue as PlayerSfxSettings;
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Assign a Player SFX settings asset to configure sounds.", MessageType.Info);
            return;
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sound Settings", EditorStyles.boldLabel);
        using (var data = new SerializedObject(settings))
        {
            data.Update();
            DrawPropertiesExcluding(data, "m_Script");
            data.ApplyModifiedProperties();
        }
    }
}
