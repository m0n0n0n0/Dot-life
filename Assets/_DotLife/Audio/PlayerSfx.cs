using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class PlayerSfx : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BasicBehaviourScript motor;
    [SerializeField] private PlayerInk ink;
    [SerializeField] private PlayerSfxSettings settings;
    private GameObject audioRoot;
    private AudioSource footsteps;
    private AudioSource stamp;
    private AudioSource refill;
    private Vector3 previousPosition;
    private float distanceSinceStep;
    private float lastStepTime = float.NegativeInfinity;
    private bool skipDisplacement;
    private bool focused;
    private AudioClip lastFootstep;
    private AudioClip lastStamp;
    private AudioClip lastRefill;
    // Keep sound variation independent of gameplay's UnityEngine.Random sequence.
    private readonly System.Random random = new System.Random();

    void Awake()
    {
        if (motor == null || ink == null || ink.gameObject != motor.gameObject)
        {
            Debug.LogError("PlayerSfx requires motor and ink references to the same player.", this);
            enabled = false;
            return;
        }
        focused = Application.isFocused;
        audioRoot = new GameObject("_PlayerSfx");
        audioRoot.transform.SetParent(transform, false);
        footsteps = CreateSource("Footsteps");
        stamp = CreateSource("Stamp");
        refill = CreateSource("Refill");
    }

    void OnEnable()
    {
        if (motor == null || ink == null) return;
        ink.InkChanged += OnInkChanged;
        ink.Refilled += OnRefilled;
        motor.OnContactsInvalidated += InvalidateDisplacement;
        motor.OnActionStateChanged += OnActionStateChanged;
        InvalidateDisplacement();
    }

    void LateUpdate()
    {
        Vector3 displacement = PlayerPosition - previousPosition;
        previousPosition = PlayerPosition;
        if (!CanHear || Time.deltaTime <= 0f)
        {
            StopSources();
            ResetSteps();
            return;
        }
        if (!IsWalking)
        {
            ResetSteps();
            return;
        }
        if (skipDisplacement)
        {
            skipDisplacement = false;
            return;
        }
        if (settings.footstepMode != FootstepPlaybackMode.Distance)
        {
            distanceSinceStep = 0f;
            return;
        }
        float distance = Vector3.ProjectOnPlane(displacement, Vector3.up).magnitude;
        float stride = Positive(settings.stepDistance, 1f);
        // Ignore teleports and long stalls instead of emitting a burst of catch-up sounds.
        if (Time.deltaTime > 0.25f || distance > Mathf.Max(stride * 2f, motor.walkSpeed * Time.deltaTime * 2f))
        {
            ResetSteps();
            return;
        }
        distanceSinceStep += distance;
        if (distanceSinceStep < stride || !StepIntervalElapsed) return;
        distanceSinceStep %= stride;
        PlayFootstep();
    }

    // Optional Animation Event. Distance mode deliberately ignores this to prevent double playback.
    public void OnFootstep()
    {
        if (CanHear && IsWalking && settings.footstepMode == FootstepPlaybackMode.AnimationEvents
            && StepIntervalElapsed) PlayFootstep();
    }

    private Vector3 PlayerPosition => motor != null ? motor.transform.position : Vector3.zero;
    private bool CanHear => isActiveAndEnabled && focused && Time.timeScale > 0f
        && settings != null && motor != null && ink != null;
    private bool IsWalking => settings != null && motor != null && motor.CanMove && motor.IsGrounded
        && Vector3.ProjectOnPlane(motor.Velocity, Vector3.up).sqrMagnitude
            > Mathf.Pow(Mathf.Max(0.01f, Finite(settings.minimumMoveSpeed, 0.1f)), 2f);
    private bool StepIntervalElapsed => Time.time - lastStepTime >= Positive(settings.minimumStepInterval, 0.12f);

    private void PlayFootstep()
    {
        lastStepTime = Time.time;
        Play(footsteps, settings.footstepClips, settings.footstepVolume, settings.footstepPitch, ref lastFootstep);
    }

    private void OnInkChanged(float previous, float current)
    {
        // The final valid stamp can finish the game inside its commit, before this notification.
        if (current < previous && CanHear)
            Play(stamp, settings.stampClips, settings.stampVolume, settings.stampPitch, ref lastStamp);
    }

    private AudioSource CreateSource(string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(audioRoot.transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        return source;
    }

    private void OnRefilled()
    {
        if (CanHear)
            Play(refill, settings.refillClips, settings.refillVolume, settings.refillPitch, ref lastRefill);
    }

    private void Play(AudioSource source, AudioClip[] clips, float volume, Vector2 pitch, ref AudioClip previous)
    {
        if (source == null || clips == null) return;
        int validCount = 0;
        int alternativeCount = 0;
        foreach (AudioClip clip in clips)
        {
            if (clip == null) continue;
            validCount++;
            if (clip != previous) alternativeCount++;
        }
        if (validCount == 0) return;
        int selected = random.Next(alternativeCount > 0 ? alternativeCount : validCount);
        foreach (AudioClip clip in clips)
        {
            if (clip == null || (alternativeCount > 0 && clip == previous)) continue;
            if (selected-- > 0) continue;
            previous = clip;
            source.outputAudioMixerGroup = settings.output;
            source.volume = Mathf.Clamp01(Finite(volume, 0f));
            float low = Mathf.Clamp(Finite(pitch.x, 1f), 0.1f, 3f);
            float high = Mathf.Clamp(Finite(pitch.y, 1f), 0.1f, 3f);
            source.pitch = Mathf.Lerp(Mathf.Min(low, high), Mathf.Max(low, high), (float)random.NextDouble());
            // One voice per action channel: fast repeats do not accumulate overlapping sound tails.
            source.clip = clip;
            source.Play();
            return;
        }
    }

    private void OnActionStateChanged(PlayerActionState state)
    {
        if (state == PlayerActionState.Locomotion) return;
        if (footsteps != null) footsteps.Stop();
        InvalidateDisplacement();
    }

    private void InvalidateDisplacement()
    {
        ResetSteps();
        skipDisplacement = true;
    }

    private void ResetSteps()
    {
        previousPosition = PlayerPosition;
        distanceSinceStep = 0f;
    }

    private void StopSources()
    {
        if (footsteps != null) footsteps.Stop();
        if (stamp != null) stamp.Stop();
        if (refill != null) refill.Stop();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        focused = hasFocus;
        if (!hasFocus) StopSources();
        InvalidateDisplacement();
    }

    void OnDisable()
    {
        if (ink != null) ink.InkChanged -= OnInkChanged;
        if (ink != null) ink.Refilled -= OnRefilled;
        if (motor != null)
        {
            motor.OnContactsInvalidated -= InvalidateDisplacement;
            motor.OnActionStateChanged -= OnActionStateChanged;
        }
        StopSources();
        InvalidateDisplacement();
    }

    void OnDestroy()
    {
        if (audioRoot != null) Destroy(audioRoot);
    }

    private static float Finite(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    private static float Positive(float value, float fallback) => Mathf.Max(0.05f, Finite(value, fallback));
}
