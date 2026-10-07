using System.Collections;
using UnityEngine;

public class CameraFollowingScript : MonoBehaviour
{
    [UnityEngine.Serialization.FormerlySerializedAs("camera")]
    public Camera followCamera;
    public Transform target;
    [Header("Farthest Framing")]
    public float distance = 5f;
    public float height = 5f;
    public Vector3 aimOffset;
    [Min(0.001f)] public float horizontalSmoothTime = 0.15f;
    [Min(0.001f)] public float verticalSmoothTime = 0.25f;
    [Header("Wheel Zoom")]
    [Min(0f)] public float zoomRange = 4f;
    [Min(0f)] public float zoomPerScrollUnit = 1f;
    [Min(0.001f)] public float zoomSmoothTime = 0.12f;
    [Header("Horizontal Orbit")]
    [Min(0f)] public float rotationSensitivity = 3f;
    [Min(0.001f)] public float rotationSmoothTime = 0.08f;
    [Header("Ending")]
    public Vector3 finalPos;
    public Quaternion finalRotation = Quaternion.Euler(30f, -90f, 0f);
    [Min(0f)] public float finalMoveDuration = 5f;
    [SerializeField] private GameState gameState;

    public bool IsMovingToFinal { get; private set; }
    public float TargetZoomOffset { get; private set; }
    public float CurrentZoomOffset { get; private set; }
    public float TargetYawOffset { get; private set; }
    public float CurrentYawOffset { get; private set; }
    public float MaxFollowDistance => ViewingOffset.magnitude;
    public float MinFollowDistance => MaxFollowDistance - AvailableZoom;
    public float CurrentFollowDistance => MaxFollowDistance - CurrentZoomOffset;

    private Vector3 followAnchor;
    private Vector3 followVelocity;
    private float zoomVelocity;
    private float yawVelocity;
    private Coroutine finalCoroutine;
    private Vector3 ViewingOffset
    {
        get
        {
            // This base offset fixes the original pitch; orbit rotates it only around world up.
            Vector3 offset = new Vector3(distance * 0.5f, height, -distance) - aimOffset;
            return offset.sqrMagnitude > 0.001f ? offset : Vector3.up * 0.5f;
        }
    }
    private float AvailableZoom => Mathf.Min(Mathf.Max(0f, zoomRange), Mathf.Max(0f, MaxFollowDistance - 0.5f));
    private bool CanAdjustView => isActiveAndEnabled && followCamera != null && target != null
        && !IsMovingToFinal && Time.timeScale > 0f
        && (gameState == null || gameState.Phase != GameSessionPhase.Finished);

    void Awake()
    {
        if (target == null) target = transform;
        if (followCamera == null) followCamera = Camera.main;
        if (gameState == null) gameState = FindObjectOfType<GameState>();
        if (Quaternion.Dot(finalRotation, finalRotation) < 0.001f)
            finalRotation = Quaternion.Euler(30f, -90f, 0f);
    }

    void OnEnable()
    {
        if (gameState != null) gameState.OnGameOver += MoveToFinal;
        if (followCamera == null || target == null) return;
        followAnchor = target.position + aimOffset;
        followVelocity = Vector3.zero;
        zoomVelocity = 0f;
        yawVelocity = 0f;
        if (gameState != null && gameState.Phase == GameSessionPhase.Finished)
        { MoveToFinal(); return; }
        TargetZoomOffset = Mathf.Clamp(TargetZoomOffset, 0f, AvailableZoom);
        CurrentZoomOffset = TargetZoomOffset;
        CurrentYawOffset = TargetYawOffset;
        ApplyFollowPose();
    }

    public void ApplyZoomInput(float scroll)
    {
        if (!CanAdjustView || float.IsNaN(scroll) || float.IsInfinity(scroll)) return;
        TargetZoomOffset = Mathf.Clamp(TargetZoomOffset + scroll * Mathf.Max(0f, zoomPerScrollUnit), 0f, AvailableZoom);
    }

    public void ApplyRotationInput(float horizontalDelta)
    {
        if (!CanAdjustView || float.IsNaN(horizontalDelta) || float.IsInfinity(horizontalDelta)) return;
        // Mouse axes already represent frame displacement, so do not multiply by delta time.
        TargetYawOffset = Mathf.DeltaAngle(0f, TargetYawOffset + horizontalDelta * Mathf.Max(0f, rotationSensitivity));
    }

    void LateUpdate() => TickCamera(Time.deltaTime);

    public void TickCamera(float deltaTime)
    {
        if (!isActiveAndEnabled || followCamera == null || target == null || IsMovingToFinal || deltaTime <= 0f
            || (gameState != null && gameState.Phase == GameSessionPhase.Finished)) return;
        Vector3 desired = target.position + aimOffset;
        float horizontalTime = Mathf.Max(0.001f, horizontalSmoothTime);
        float x = Mathf.SmoothDamp(followAnchor.x, desired.x, ref followVelocity.x, horizontalTime, Mathf.Infinity, deltaTime);
        float y = Mathf.SmoothDamp(followAnchor.y, desired.y, ref followVelocity.y,
            Mathf.Max(0.001f, verticalSmoothTime), Mathf.Infinity, deltaTime);
        float z = Mathf.SmoothDamp(followAnchor.z, desired.z, ref followVelocity.z, horizontalTime, Mathf.Infinity, deltaTime);
        followAnchor = new Vector3(x, y, z);
        TargetZoomOffset = Mathf.Clamp(TargetZoomOffset, 0f, AvailableZoom);
        CurrentZoomOffset = Mathf.SmoothDamp(CurrentZoomOffset, TargetZoomOffset, ref zoomVelocity,
            Mathf.Max(0.001f, zoomSmoothTime), Mathf.Infinity, deltaTime);
        CurrentZoomOffset = Mathf.Clamp(CurrentZoomOffset, 0f, AvailableZoom);
        CurrentYawOffset = Mathf.SmoothDampAngle(CurrentYawOffset, TargetYawOffset, ref yawVelocity,
            Mathf.Max(0.001f, rotationSmoothTime), Mathf.Infinity, deltaTime);
        CurrentYawOffset = Mathf.DeltaAngle(0f, CurrentYawOffset);
        ApplyFollowPose();
    }

    private void ApplyFollowPose()
    {
        Quaternion orbit = Quaternion.AngleAxis(CurrentYawOffset, Vector3.up);
        Vector3 offset = orbit * ViewingOffset;
        Vector3 look = -offset.normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(look, Vector3.up)) > 0.999f ? orbit * Vector3.forward : Vector3.up;
        followCamera.transform.SetPositionAndRotation(followAnchor + offset.normalized * CurrentFollowDistance,
            Quaternion.LookRotation(look, up));
    }

    private void MoveToFinal()
    {
        if (followCamera == null || finalCoroutine != null) return;
        IsMovingToFinal = true;
        finalCoroutine = StartCoroutine(MoveToFinalCoroutine());
    }

    private IEnumerator MoveToFinalCoroutine()
    {
        Vector3 start = followCamera.transform.position;
        Quaternion rotation = followCamera.transform.rotation;
        float elapsed = 0f;
        float duration = Mathf.Max(0.001f, finalMoveDuration);
        do
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            followCamera.transform.SetPositionAndRotation(Vector3.Lerp(start, finalPos, t), Quaternion.Slerp(rotation, finalRotation, t));
            yield return null;
        } while (elapsed < duration);
        followCamera.transform.SetPositionAndRotation(finalPos, finalRotation);
        finalCoroutine = null;
    }

    void OnDisable()
    {
        if (gameState != null) gameState.OnGameOver -= MoveToFinal;
        if (finalCoroutine != null) StopCoroutine(finalCoroutine);
        finalCoroutine = null;
        IsMovingToFinal = false;
    }
}
