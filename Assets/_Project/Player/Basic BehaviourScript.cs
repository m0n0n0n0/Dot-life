using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicBehaviourScript : MonoBehaviour
{
    public float unitLength = 3.0f; 
    public float rotateSpeed = 500.0f;
    public float doublePressThreshold = 0.25f;
    public float rotateError = 1.0f;
    public float moveError = 0.05f;
    public float walkSpeed = 2.0f;
    public float rollSpeed = 4.0f;

    public Vector3 endPos;
    public Quaternion endRot;

    private Animator animator;
    private bool isMoving = false;
    private float lastPressTimeW, lastPressTimeA, lastPressTimeS, lastPressTimeD;
    private Vector3 lastMoveDirection = Vector3.zero;

    private Coroutine inputHandlingCoroutine;
    private KeyCode lastKeyCode = KeyCode.None;

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
    }

    void Update()
    {
        if (animator.GetBool("isRolling"))
        {
            Vector3 oppositeDirection = Vector3.zero;
            if (Input.GetKeyDown(KeyCode.S) && lastMoveDirection == Vector3.forward) oppositeDirection = Vector3.back;
            else if (Input.GetKeyDown(KeyCode.W) && lastMoveDirection == Vector3.back) oppositeDirection = Vector3.forward;
            else if (Input.GetKeyDown(KeyCode.D) && lastMoveDirection == Vector3.left) oppositeDirection = Vector3.right;
            else if (Input.GetKeyDown(KeyCode.A) && lastMoveDirection == Vector3.right) oppositeDirection = Vector3.left;

            if (oppositeDirection != Vector3.zero)
            {
                StopAllCoroutines();
                StartCoroutine(Move(oppositeDirection, true, true));
                return;
            }
        }

        if (isMoving)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            HandleInput(KeyCode.W, Vector3.forward, ref lastPressTimeW);
        }
        else if (Input.GetKeyDown(KeyCode.S))
        {
            HandleInput(KeyCode.S, Vector3.back, ref lastPressTimeS);
        }
        else if (Input.GetKeyDown(KeyCode.A))
        {
            HandleInput(KeyCode.A, Vector3.left, ref lastPressTimeA);
        }
        else if (Input.GetKeyDown(KeyCode.D))
        {
            HandleInput(KeyCode.D, Vector3.right, ref lastPressTimeD);
        }
    }

    void HandleInput(KeyCode key, Vector3 direction, ref float lastPressTime)
    {
        if (inputHandlingCoroutine != null)
        {
            StopCoroutine(inputHandlingCoroutine);
        }

        bool isDoublePress = (Time.time - lastPressTime < doublePressThreshold) && (lastKeyCode == key);
        lastPressTime = Time.time;
        lastKeyCode = key;

        if (isDoublePress)
        {
            StartCoroutine(Move(direction, true));
        }
        else
        {
            inputHandlingCoroutine = StartCoroutine(WaitForNextInput(direction));
        }
    }

    IEnumerator WaitForNextInput(Vector3 direction)
    {
        yield return new WaitForSeconds(doublePressThreshold);
        StartCoroutine(Move(direction, false));
        inputHandlingCoroutine = null;
    }

    IEnumerator Move(Vector3 direction, bool isRolling, bool isOppositeMove = false)
    {
        if (!isOppositeMove)
        {
            endRot = Quaternion.LookRotation(direction);

            while (Quaternion.Angle(transform.rotation, endRot) > rotateError)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, endRot, rotateSpeed * Time.deltaTime);
                yield return null;
            }
            transform.rotation = endRot;
        }

        isMoving = true;
        lastMoveDirection = direction;

        Vector3 startPos = transform.position;

        if (isOppositeMove)
        {
            endPos = new Vector3(
                Mathf.Round(startPos.x / unitLength) * unitLength - direction.x * unitLength,
                startPos.y,
                Mathf.Round(startPos.z / unitLength) * unitLength - direction.z * unitLength
            );
        }
        else
        {
            endPos = startPos + direction * unitLength;
        }

        animator.SetBool("isRolling", isRolling);
        animator.SetBool("isWalking", !isRolling);

        float currentSpeed = isRolling ? rollSpeed : walkSpeed;
        while (Vector3.Distance(transform.position, endPos) > moveError)
        {
            transform.position = Vector3.MoveTowards(transform.position, endPos, currentSpeed * Time.deltaTime);
            yield return null;
        }
        //transform.position = endPos;

        if (!isRolling)
        {
            animator.SetBool("isWalking", false);
        }
        
        isMoving = false;
    }
}
