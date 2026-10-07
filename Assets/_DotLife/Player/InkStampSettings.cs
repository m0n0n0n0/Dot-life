using UnityEngine;

[CreateAssetMenu(menuName = "Dot Life/Ink Stamp Settings")]
public class InkStampSettings : ScriptableObject
{
    public Material material;
    public LayerMask surfaceLayers = ~0;
    [Min(0.01f)] public float diameter = 0.34f;
    [Tooltip("World-space distance along the player's horizontal facing; positive moves the free stamp forward.")]
    [Range(-0.5f, 0.5f)] public float forwardOffset = 0.2f;
    [Range(2, 12)] public int subdivisions = 6;
    [Min(0.001f)] public float sampleDepth = 0.06f;
    [Range(1f, 60f)] public float normalTolerance = 30f;
    [Min(0f)] public float surfaceOffset = 0.0025f;
    [Range(1, 512)] public int maximumMarks = 128;
    [Min(0.01f)] public float maximumActionDrift = 0.12f;
    public int Capacity => Mathf.Clamp(maximumMarks, 1, 512);
    public int Resolution => Mathf.Clamp(subdivisions, 2, 12);
    public float ForwardOffset => float.IsNaN(forwardOffset) || float.IsInfinity(forwardOffset)
        ? 0f : Mathf.Clamp(forwardOffset, -0.5f, 0.5f);
}
