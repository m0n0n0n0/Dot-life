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

    public event Action<string> OnDrawingCompleted;
    public string message = "";

    private int curDotIndex = 0;
    private int curTexIndex = 0;
    private Material material;
    private Coroutine animationCoroutine;

    public float frameDuration = 0.2f;

    public event Action<bool> OnPlayerEnterExit;
    private int dotsEnteredCount = 0;

    void Start()
    {
        foreach(var dot in dotTriggers)
        {
            dot.OnDrawing += CheckDot;
            dot.OnEnteringAndExiting += CheckPlayerEnterExit;
        }
        material = GetComponent<Renderer>().material;
    }

    void CheckPlayerEnterExit(bool isEnter) 
    {
        if (isEnter)
        {
            dotsEnteredCount++;
            if (dotsEnteredCount == 1)
            {
                OnPlayerEnterExit?.Invoke(true);
            }
        }
        else
        {
            dotsEnteredCount--;
            if (dotsEnteredCount == 0)
            {
                OnPlayerEnterExit?.Invoke(false);
            }
        }
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

        if (curTexIndex + 1 == frames.Count)
        {
            Debug.Log(message);
            OnDrawingCompleted?.Invoke(message);
        }
    }
}
