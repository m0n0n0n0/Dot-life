using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Renderer))]
public class TextureSurface : MonoBehaviour
{
    private Renderer surface;
    private Material original;
    private Material instance;
    public Texture CurrentTexture => instance != null ? instance.mainTexture : null;
    public bool IsReady { get { EnsureInitialized(); return instance != null; } }

    void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (instance != null) return;
        surface = GetComponent<Renderer>();
        original = surface.sharedMaterial;
        if (original == null) return;
        // Drawing and its outro share one owned instance per renderer.
        instance = new Material(original);
        instance.name = original.name + " (DotLifeRuntime)";
        surface.sharedMaterial = instance;
    }
    public bool SetTexture(Texture texture)
    {
        if (instance == null || texture == null) return false;
        instance.mainTexture = texture;
        return true;
    }
    void OnDestroy()
    {
        if (surface != null && surface.sharedMaterial == instance) surface.sharedMaterial = original;
        if (instance != null) Destroy(instance);
    }
}
