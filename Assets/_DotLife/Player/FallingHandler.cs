using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BasicBehaviourScript))]
public class FallingHandler : MonoBehaviour
{
    [Min(0f)] public float jumpHeight = 2f;
    [Min(0f)] public float fallDuration = 4f;
    [Min(0f)] public float landDuration = 1f;
    public float bias = 0.05f;
    private Animator animator;
    private BasicBehaviourScript movement;
    private Coroutine fallingCoroutine;

    void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<BasicBehaviourScript>();
    }

    public bool TryFall(Vector3 endPos)
    {
        if (!isActiveAndEnabled || fallingCoroutine != null || movement == null
            || !movement.TryBeginScriptedMovement()) return false;
        if (animator != null)
        {
            animator.SetBool("isFalling", true);
            animator.CrossFadeInFixedTime("Base Layer.Fall.TakeOff", 0.08f, 0, 0f);
        }
        fallingCoroutine = StartCoroutine(FallCoroutine(endPos));
        return true;
    }

    private IEnumerator FallCoroutine(Vector3 endPos)
    {
        Vector3 startPos = transform.position + Vector3.up * bias;
        Vector3 peakPos = (startPos + endPos) * 0.5f + Vector3.up * Mathf.Max(0f, jumpHeight);
        float duration = Mathf.Max(fallDuration, 0.001f);
        float landingTime = duration - Mathf.Clamp(landDuration, 0f, duration);
        float elapsed = 0f;
        bool landTriggered = false;
        while (elapsed < duration)
        {
            if (!movement.isActiveAndEnabled || !movement.IsScriptedMovement) { FinishFall(); yield break; }
            elapsed = Mathf.Min(elapsed + Time.deltaTime, duration);
            float t = elapsed / duration;
            movement.SetScriptedPosition((1f - t) * (1f - t) * startPos + 2f * (1f - t) * t * peakPos + t * t * endPos);
            if (!landTriggered && elapsed >= landingTime)
            {
                if (animator != null) animator.CrossFadeInFixedTime("Base Layer.Fall.Land", 0.08f, 0, 0f);
                landTriggered = true;
            }
            yield return null;
        }
        movement.SetScriptedPosition(endPos);
        FinishFall();
    }

    private void FinishFall()
    {
        if (animator != null)
        {
            animator.SetBool("isFalling", false);
            animator.ResetTrigger("Land");
            if (animator.isActiveAndEnabled) animator.CrossFadeInFixedTime("Base Layer.Idle", 0.08f);
        }
        if (movement != null) movement.EndScriptedMovement();
        fallingCoroutine = null;
    }

    void OnDisable()
    {
        if (fallingCoroutine == null) return;
        StopCoroutine(fallingCoroutine);
        FinishFall();
    }
}
