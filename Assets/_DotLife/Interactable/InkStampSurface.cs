using UnityEngine;

[DisallowMultipleComponent]
public class InkStampSurface : MonoBehaviour
{
    public bool allowStamping = true;
    [Range(-0.02f, 0.02f)] public float additionalOffset;
}
