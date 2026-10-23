using System;
using UnityEngine;

public enum PlayerActionState { Locomotion, Interacting, Falling, Appearing }

[RequireComponent(typeof(CharacterController))]
public class BasicBehaviourScript : MonoBehaviour
{
    [Header("References")]
    public GameState runtime;
    public Transform movementCamera;
    [Header("Movement")]
    [Min(0f)] public float walkSpeed = 3f;
    [Min(0f)] public float acceleration = 20f;
    [Min(0f)] public float deceleration = 15f;
    [Range(0f, 1f)] public float airControl = 0.25f;
    [Header("Rotation")]
    [Min(0f)] public float rotateSpeed = 500f;
    [Header("Ground Contact")]
    [Min(0.001f)] public float groundRayLength = 0.12f;
    [Min(0.001f)] public float groundSnapOffset = 0.05f;
    [Min(0f)] public float gravity = 20f;
    [Min(0f)] public float groundedStickSpeed = 2f;
    public LayerMask groundLayer = ~0;
    [Header("Controller Traversal")]
    [Range(0f, 80f)] public float maxSlopeAngle = 50f;
    [Min(0f)] public float maxStepHeight = 0.3f;
    [Header("Visual Grounding")]
    [SerializeField] private Transform visualRoot;
    [Range(0f, 0.1f)] public float visualGroundOffset = 0.02f;

    public CharacterController Controller { get; private set; }
    public PlayerActionState ActionState { get; private set; }
    public bool IsScriptedMovement => ActionState == PlayerActionState.Falling;
    public bool IsAppearing => ActionState == PlayerActionState.Appearing;
    public bool IsGrounded { get; private set; }
    public RaycastHit GroundContact { get; private set; }
    public Vector3 Velocity { get; private set; }
    public bool CanMove => isActiveAndEnabled && Controller != null && Controller.enabled
        && runtime != null && runtime.isActiveAndEnabled
        && runtime.Phase == GameSessionPhase.Playing && ActionState == PlayerActionState.Locomotion;
    public bool CanInteract => CanMove && IsGrounded;
    public event Action<PlayerActionState> OnActionStateChanged;
    public event Action OnContactsInvalidated;

    private Animator animator;
    private Vector3 inputDirection;
    private Vector3 planarVelocity;
    private float verticalVelocity;
    private RaycastHit[] hits = new RaycastHit[32];
    private bool controllerWasEnabled;
    private Vector3 appliedVisualOffset;
    private float appearanceScale = 1f;
    private float appearanceLift;
    private Vector3 appearanceFootPivot;
    private Vector3 originalVisualScale;
    private bool visualScaleApplied;
    private static readonly int Walking = Animator.StringToHash("isWalking");

    void Awake()
    {
        Controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        if (runtime == null) runtime = FindObjectOfType<GameState>();
        if (movementCamera == null && Camera.main != null) movementCamera = Camera.main.transform;
        if (animator != null) animator.applyRootMotion = false;
        if (visualRoot == null)
        {
            SkinnedMeshRenderer skin = GetComponentInChildren<SkinnedMeshRenderer>();
            Transform bone = skin != null ? skin.rootBone : null;
            if (bone != null && bone != transform && bone.IsChildOf(transform))
            {
                while (bone.parent != transform) bone = bone.parent;
                visualRoot = bone;
            }
        }
        if (GetComponent<Rigidbody>() != null)
            Debug.LogError("Player must use CharacterController without a Rigidbody.", this);
        ApplyTraversalSettings();
        RefreshGroundContact();
    }

    public void SetMoveInput(Vector2 input)
    {
        if (!CanMove) { inputDirection = Vector3.zero; return; }
        input = Vector2.ClampMagnitude(input, 1f);
        Vector3 forward = movementCamera != null
            ? Vector3.ProjectOnPlane(movementCamera.forward, Vector3.up).normalized : Vector3.forward;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        inputDirection = forward * input.y + right * input.x;
    }

    void Update()
    {
        RestoreVisualOffset();
        TickMovement(Time.deltaTime);
        if (animator != null)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(Velocity, Vector3.up);
            animator.SetBool(Walking, CanMove && IsGrounded && horizontal.sqrMagnitude > 0.01f);
        }
    }

    void LateUpdate()
    {
        RestoreVisualOffset();
        if (visualRoot == null || visualRoot == transform || !visualRoot.IsChildOf(transform)) return;
        // Apply after animation, undo before the next frame; neither bones nor idle poses accumulate drift.
        Vector3 visualOffset = Vector3.down * Mathf.Clamp(visualGroundOffset, 0f, 0.1f);
        if (IsAppearing)
        {
            Vector3 footBeforeScaling = visualRoot.TransformPoint(appearanceFootPivot);
            originalVisualScale = visualRoot.localScale;
            visualRoot.localScale = originalVisualScale * appearanceScale;
            visualScaleApplied = true;
            visualOffset += footBeforeScaling - visualRoot.TransformPoint(appearanceFootPivot)
                + Vector3.up * appearanceLift;
        }
        appliedVisualOffset = visualRoot.parent.InverseTransformVector(visualOffset);
        visualRoot.localPosition += appliedVisualOffset;
    }

    private void RestoreVisualOffset()
    {
        if (visualRoot != null) visualRoot.localPosition -= appliedVisualOffset;
        if (visualRoot != null && visualScaleApplied) visualRoot.localScale = originalVisualScale;
        visualScaleApplied = false;
        appliedVisualOffset = Vector3.zero;
    }

    public void TickMovement(float deltaTime)
    {
        if (!isActiveAndEnabled || Controller == null || !Controller.enabled
            || IsScriptedMovement || IsAppearing || deltaTime <= 0f) return;
        RefreshGroundContact();
        ApplyTraversalSettings();
        if (!CanMove)
        {
            inputDirection = planarVelocity = Vector3.zero;
        }
        else
        {
            Vector3 desired = inputDirection * Mathf.Max(0f, walkSpeed);
            float rate = inputDirection.sqrMagnitude > 0.001f ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desired,
                Mathf.Max(0f, rate) * (IsGrounded ? 1f : airControl) * deltaTime);
            if (inputDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(inputDirection), Mathf.Max(0f, rotateSpeed) * deltaTime);
        }
        if (IsGrounded && verticalVelocity <= 0f)
            verticalVelocity = -Mathf.Max(0f, groundedStickSpeed);
        else
            verticalVelocity -= Mathf.Max(0f, gravity) * deltaTime;

        Vector3 start = transform.position;
        CollisionFlags collisions = Controller.Move((planarVelocity + Vector3.up * verticalVelocity) * deltaTime);
        Velocity = (transform.position - start) / deltaTime;
        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
        if ((collisions & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            verticalVelocity = -Mathf.Max(0f, groundedStickSpeed);
        RefreshGroundContact();
    }

    private void ApplyTraversalSettings()
    {
        if (Controller == null) return;
        float slope = Mathf.Clamp(maxSlopeAngle, 0f, 80f);
        float step = Mathf.Clamp(maxStepHeight, 0f, Controller.height);
        if (!Mathf.Approximately(Controller.slopeLimit, slope)) Controller.slopeLimit = slope;
        if (!Mathf.Approximately(Controller.stepOffset, step)) Controller.stepOffset = step;
    }

    public bool TryBeginAppearance()
    {
        if (!CanMove || visualRoot == null || visualRoot == transform || !visualRoot.IsChildOf(transform)) return false;
        RestoreVisualOffset();
        Bounds body = Controller.bounds;
        appearanceFootPivot = visualRoot.InverseTransformPoint(new Vector3(body.center.x, body.min.y, body.center.z));
        ResetMotion();
        IsGrounded = false;
        GroundContact = default;
        SetActionState(PlayerActionState.Appearing);
        OnContactsInvalidated?.Invoke();
        return true;
    }

    public void SetAppearanceVisual(float scale, float lift)
    {
        if (!IsAppearing) return;
        appearanceScale = Mathf.Max(0.001f, scale);
        appearanceLift = Mathf.Max(0f, lift);
    }

    public void EndAppearance()
    {
        if (!IsAppearing) return;
        RestoreVisualOffset();
        appearanceScale = 1f;
        appearanceLift = 0f;
        ResetMotion();
        SetActionState(PlayerActionState.Locomotion);
        RefreshGroundContact();
    }

    public bool TryBeginInteraction()
    {
        if (!CanInteract) return false;
        ResetMotion();
        SetActionState(PlayerActionState.Interacting);
        return true;
    }

    public void EndInteraction()
    {
        if (ActionState == PlayerActionState.Interacting) SetActionState(PlayerActionState.Locomotion);
    }

    public bool TryBeginScriptedMovement()
    {
        if (!CanInteract) return false;
        controllerWasEnabled = Controller.enabled;
        // Authored jumps retain their existing collision-free trajectory; ordinary movement has no ownership here.
        Controller.enabled = false;
        OnContactsInvalidated?.Invoke();
        ResetMotion();
        IsGrounded = false;
        GroundContact = default;
        SetActionState(PlayerActionState.Falling);
        return true;
    }

    public void SetScriptedPosition(Vector3 position)
    {
        if (IsScriptedMovement) transform.position = position;
    }

    public void EndScriptedMovement()
    {
        if (!IsScriptedMovement || Controller == null) return;
        Controller.enabled = controllerWasEnabled;
        ResetMotion();
        SetActionState(PlayerActionState.Locomotion);
        RefreshGroundContact();
    }

    public void Teleport(Vector3 position)
    {
        if (Controller == null || IsScriptedMovement) return;
        bool wasEnabled = Controller.enabled;
        Controller.enabled = false;
        OnContactsInvalidated?.Invoke();
        transform.position = position;
        Controller.enabled = wasEnabled;
        ResetMotion();
        Physics.SyncTransforms();
        RefreshGroundContact();
    }

    private void ResetMotion()
    {
        inputDirection = planarVelocity = Velocity = Vector3.zero;
        verticalVelocity = 0f;
    }

    private void SetActionState(PlayerActionState state)
    {
        inputDirection = Vector3.zero;
        ActionState = state;
        if (animator != null) animator.SetBool(Walking, false);
        OnActionStateChanged?.Invoke(state);
    }

    public void RefreshGroundContact()
    {
        IsGrounded = false;
        GroundContact = default;
        if (Controller == null || !Controller.enabled || IsScriptedMovement || IsAppearing) return;
        Bounds bounds = Controller.bounds;
        Vector3 foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        float bestDistance = float.PositiveInfinity;
        float offset = Mathf.Max(0.01f, groundSnapOffset + Controller.skinWidth);
        for (int i = 0; i < 5; i++)
        {
            Vector3 origin = foot + Vector3.up * offset;
            if (i == 1) origin.x += bounds.extents.x * 0.65f;
            if (i == 2) origin.x -= bounds.extents.x * 0.65f;
            if (i == 3) origin.z += bounds.extents.z * 0.65f;
            if (i == 4) origin.z -= bounds.extents.z * 0.65f;
            if (!CastNearest(origin, offset + Mathf.Max(0.001f, groundRayLength), out RaycastHit hit)) continue;
            if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle || hit.distance >= bestDistance) continue;
            bestDistance = hit.distance;
            GroundContact = hit;
            IsGrounded = verticalVelocity <= 0f;
        }
    }

    private bool CastNearest(Vector3 origin, float length, out RaycastHit closest)
    {
        int count;
        while (true)
        {
            count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, length, groundLayer, QueryTriggerInteraction.Ignore);
            if (count < hits.Length) break;
            if (hits.Length >= 1024) { closest = default; return false; }
            Array.Resize(ref hits, hits.Length * 2);
        }
        closest = default;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider == null || hits[i].collider.transform.IsChildOf(transform)
                || hits[i].distance >= distance) continue;
            distance = hits[i].distance;
            closest = hits[i];
        }
        return distance < float.PositiveInfinity;
    }

    void OnDisable()
    {
        RestoreVisualOffset();
        EndAppearance();
        EndScriptedMovement();
        EndInteraction();
        ResetMotion();
        if (animator != null) animator.SetBool(Walking, false);
    }

    void OnValidate()
    {
        if (Controller == null) Controller = GetComponent<CharacterController>();
        ApplyTraversalSettings();
    }
}
