using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicBehaviourScript : MonoBehaviour
{
    public float unitLength = 3.0f; 
    public float rotateSpeed = 500.0f;
    public float rotateError = 1.0f;
    public float moveError = 0.05f;
    public float walkSpeed = 2.0f;

    public Vector3 endPos;
    public Quaternion endRot;

    private Animator animator;
    private bool isMoving = false;
    private Vector3 lastMoveDirection = Vector3.zero;
    private Vector3? cachedDirection = null;

    private float movePercent = 0f;

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        Vector3? direction = null;
        if (Input.GetKeyDown(KeyCode.W)) direction = Vector3.back;
        else if (Input.GetKeyDown(KeyCode.S)) direction = Vector3.forward;
        else if (Input.GetKeyDown(KeyCode.A)) direction = Vector3.right;
        else if (Input.GetKeyDown(KeyCode.D)) direction = Vector3.left;

        if (direction.HasValue)
        {
            if (isMoving)
            {
                cachedDirection = direction;
            }
            else
            {
                StartCoroutine(Move(direction.Value));
            }
            return;
        }

        if (movePercent > 0.1f && isMoving)
        {
            if (Input.GetKey(KeyCode.W) && lastMoveDirection == Vector3.back) cachedDirection = Vector3.back;
            else if (Input.GetKey(KeyCode.S) && lastMoveDirection == Vector3.forward) cachedDirection = Vector3.forward;
            else if (Input.GetKey(KeyCode.A) && lastMoveDirection == Vector3.right) cachedDirection = Vector3.right;
            else if (Input.GetKey(KeyCode.D) && lastMoveDirection == Vector3.left) cachedDirection = Vector3.left;
        }
    }

    IEnumerator Move(Vector3 direction)
    {
        isMoving = true;
        lastMoveDirection = direction;
        cachedDirection = null;
        movePercent = 0f;

        endRot = Quaternion.LookRotation(direction);
        while (Quaternion.Angle(transform.rotation, endRot) > rotateError)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, endRot, rotateSpeed * Time.deltaTime);
            yield return null;
        }
        transform.rotation = endRot;

        animator.SetBool("isWalking", true);

        float duration = unitLength / walkSpeed;
        float timeElapsed = 0f;
        bool isChainingMove = false;
        bool hasChecked = false;

        Vector3 startPos = transform.position;
        endPos = startPos + direction;
        while (timeElapsed < duration)
        {
            movePercent = timeElapsed / duration;
            transform.position = Vector3.MoveTowards(transform.position, endPos, walkSpeed * Time.deltaTime);
            timeElapsed += Time.deltaTime;

            if (!hasChecked && timeElapsed >= duration / 2f)
            {
                if (cachedDirection.HasValue && cachedDirection.Value == lastMoveDirection)
                { 
                    isChainingMove = true; 
                }
                hasChecked = true;
            }

            yield return null;
        }

        if (isChainingMove)
        {
            StartCoroutine(Move(cachedDirection.Value));
        }
        else
        {
            animator.SetBool("isWalking", false);
            isMoving = false;
            movePercent = 0f;
        }
    }
}
