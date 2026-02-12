using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransitionHandler : MonoBehaviour
{
    [SerializeField]
    public DotDrawingHandler trigger;

    [SerializeField]
    private List<Texture> frames = new();

    public float frameRate = 0.05f;

    private Material material;
    private Coroutine animationCoroutine;

    void Start()
    {
        trigger.OnDrawingCompleted += StartTransition;
        material = GetComponent<Renderer>().material;
    }

    void Update()
    {
        
    }

    void StartTransition (string s)
    {
        Debug.Log("YuwenReceived");

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }
        animationCoroutine = StartCoroutine(AnimateMaterial());
    }

    IEnumerator AnimateMaterial()
    {
        for (int i = 0; i < frames.Count; i++)
        {
            material.SetTexture("_MainTex", frames[i]);
            yield return new WaitForSeconds(frameRate);
        }
    }
}
