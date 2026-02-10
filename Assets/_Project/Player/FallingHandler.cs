using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallingHandler : MonoBehaviour
{
    [SerializeField]
    private List<EdgeFalling> edgeTriggers = new();

    public float jumpHeight = 2.0f;
    public float fallDuration = 4.0f; 
    public float landDuration = 1.0f;

    private Animator animator;
    private Coroutine fallingCoroutine;

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;

        foreach (var edge in edgeTriggers)
        {
            edge.OnEdgeFalling += Fall;
        }
    }

    void Fall(Vector3 endPos)
    {
        if (fallingCoroutine != null)
        {
            StopCoroutine(fallingCoroutine);
        }
        fallingCoroutine = StartCoroutine(FallCoroutine(endPos));
    }

    private IEnumerator FallCoroutine(Vector3 endPos)
    {
        animator.SetBool("isFalling", true);

        Vector3 startPos = transform.position;
        Vector3 peakPos = (startPos + endPos) / 2f + Vector3.up * jumpHeight;

        float timeElapsed = 0f;
        bool landTriggered = false;
        while (timeElapsed < fallDuration)
        {
            float t = timeElapsed / fallDuration;
            transform.position = (1 - t) * (1 - t) * startPos + 2 * (1 - t) * t * peakPos + t * t * endPos;

            timeElapsed += Time.deltaTime;
            if (!landTriggered && timeElapsed > fallDuration - landDuration)
            {
                animator.SetTrigger("Land");
                landTriggered = true;
            }
            yield return null;
        }

        animator.SetBool("isFalling", false);
        animator.ResetTrigger("Land");
        fallingCoroutine = null;
    }
}
