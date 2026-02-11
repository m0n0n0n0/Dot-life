using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DotDrawing : MonoBehaviour
{
    public int dotIndex = 0;
    public int texIndex = 0;

    public event Action<int, int> OnDrawing;

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    void OnTriggerEnter(Collider other)
    {
        OnDrawing?.Invoke(dotIndex, texIndex);
    }
}
