using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DotDrawingHandler : MonoBehaviour
{
    [SerializeField]
    private List<DotDrawing> dotTriggers = new();

    [SerializeField]
    private List<Texture> frames = new();

    private int curDotIndex = 0;
    private int curTexIndex = 0;
    private Material material;
    private Coroutine animationCoroutine;

    public float frameDuration = 0.2f;

    void Start()
    {
        foreach(var dot in dotTriggers)
        {
            dot.OnDrawing += CheckDot;
        }
        material = GetComponent<Renderer>().material;
    }

    void Update()
    {
        
    }

    void CheckDot(int dotIndex, int texIndex)
    {
        if (curDotIndex != dotIndex)
        {
            return;
        }

        UpdateMaterial(texIndex);
        curDotIndex++;
    }

    void UpdateMaterial(int texIndex)
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }
        animationCoroutine = StartCoroutine(AnimateMaterial(texIndex));
    }

    IEnumerator AnimateMaterial(int toTexIndex)
    {
        for (int i = curTexIndex + 1; i <= toTexIndex; i++)
        {
            if (i < frames.Count)
            {
                material.SetTexture("_MainTex", frames[i]);
                curTexIndex = i;
                yield return new WaitForSeconds(frameDuration);
            }
        }
    }
}
