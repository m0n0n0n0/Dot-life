using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BasicBehaviourScript))]
[DefaultExecutionOrder(50)]
public class PlayerEntrance : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [Header("Appearance")]
    [SerializeField, Min(0.1f)] private float emergeDuration = 1.25f;
    [SerializeField, Min(0f)] private float jumpHeight = 0.75f;
    [SerializeField, Range(0.001f, 1f)] private float initialScale = 0.02f;
    [SerializeField, Min(0f)] private float settleDuration = 0.25f;
    [SerializeField, Range(0f, 0.2f)] private float settleAmount = 0.06f;

    public bool IsPlaying { get; private set; }
    private BasicBehaviourScript motor;
    private GameState runtime;
    private Renderer[] renderers;
    private bool[] previousHidden;
    private bool hidden;
    private bool hasPlayed;
    private bool started;
    private float elapsed;

    private void Awake()
    {
        motor = GetComponent<BasicBehaviourScript>();
        runtime = motor.runtime;
        renderers = GetComponentsInChildren<Renderer>(true);
        previousHidden = new bool[renderers.Length];
    }

    private void OnEnable()
    {
        if (runtime == null) return;
        runtime.OnGameStart += Begin;
        if (started && runtime.Phase == GameSessionPhase.Waiting && !hasPlayed) Hide();
    }

    private void Start()
    {
        started = true;
        if (runtime == null) return;
        if (runtime.Phase == GameSessionPhase.Waiting) Hide();
        else if (runtime.Phase == GameSessionPhase.Playing) Begin();
    }

    private void Hide()
    {
        if (hidden) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            previousHidden[i] = renderers[i].forceRenderingOff;
            renderers[i].forceRenderingOff = true;
        }
        hidden = true;
    }

    private void Show()
    {
        if (!hidden) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].forceRenderingOff = previousHidden[i];
        hidden = false;
    }

    private void Begin()
    {
        if (hasPlayed || !isActiveAndEnabled || motor == null) return;
        if (!motor.TryBeginAppearance()) { Show(); return; }
        hasPlayed = true;
        IsPlaying = true;
        elapsed = 0f;
        if (spawnPoint != null)
        {
            Bounds body = motor.Controller.bounds;
            Vector3 feet = new Vector3(body.center.x, body.min.y, body.center.z);
            motor.Teleport(spawnPoint.position + (motor.transform.position - feet));
        }
        motor.SetAppearanceVisual(initialScale, 0f);
        Show();
    }

    private void Update()
    {
        if (!IsPlaying) return;
        if (motor == null || !motor.isActiveAndEnabled || !motor.IsAppearing
            || runtime == null || runtime.Phase != GameSessionPhase.Playing)
        { Finish(); return; }
        elapsed += Time.deltaTime;
        float duration = Mathf.Max(0.1f, emergeDuration);
        if (elapsed < duration)
        {
            float t = elapsed / duration;
            float scale = Mathf.Lerp(initialScale, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.75f)));
            motor.SetAppearanceVisual(scale, Mathf.Max(0f, jumpHeight) * 4f * t * (1f - t));
        }
        else if (settleDuration > 0f && elapsed < duration + settleDuration)
        {
            float t = (elapsed - duration) / settleDuration;
            motor.SetAppearanceVisual(1f - settleAmount * Mathf.Sin(t * Mathf.PI), 0f);
        }
        else Finish();
    }

    private void Finish()
    {
        IsPlaying = false;
        if (motor != null) motor.EndAppearance();
        Show();
    }

    private void OnDisable()
    {
        if (runtime != null) runtime.OnGameStart -= Begin;
        Finish();
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(spawnPoint.position, 0.15f);
        Gizmos.DrawLine(spawnPoint.position, spawnPoint.position + Vector3.up * jumpHeight);
    }
}
