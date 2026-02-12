using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractingHandler : MonoBehaviour
{
    [SerializeField]
    private TriggerBus trigger;

    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;

        trigger.onAnimationStarted += PerformAction;
    }

    void Update()
    {
        
    }

    void PerformAction(int n)
    {
        Debug.Log(n);
        if (n == 0) 
        {
            animator.SetTrigger("Tread");
        }
        if (n == 1)
        {
            animator.SetTrigger("Hit");
        }
    }
}
