using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class InkMark : MonoBehaviour
{
    private Mesh mesh;
    public Mesh Geometry => mesh;

    public void Apply(InkStampPlacement placement, Material material, int order)
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "DotLifeInkMark" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }
        mesh.Clear();
        mesh.vertices = placement.vertices;
        mesh.normals = placement.normals;
        mesh.uv = placement.uv;
        mesh.triangles = placement.triangles;
        mesh.RecalculateBounds();
        transform.SetParent(placement.receiver.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        gameObject.layer = placement.receiver.gameObject.layer;
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.sortingOrder = order;
        gameObject.SetActive(true);
    }

    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
