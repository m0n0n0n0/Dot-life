using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicBehaviourScript : MonoBehaviour
{
    [Header("References")]
    public GameState runtime;

    [Header("Movement")]
    public float walkSpeed = 5.0f;
    public float acceleration = 20.0f;
    public float deceleration = 15.0f;

    [Header("Rotation")]
    public float rotateSpeed = 720.0f;

    [Header("Ground Detection")]
    public float groundRayLength = 5.0f;
    public float groundSnapOffset = 0.05f;
    public float gravity = 40.0f; // 增大了重力以加快下落
    public LayerMask groundLayer = ~0; // 默认检测所有层

    private Animator animator;
    private Rigidbody rb;
    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 inputDirection = Vector3.zero;
    private Vector3 groundNormal = Vector3.up;
    private bool isGrounded = false;

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false;

        // 获取或添加 Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        // 关键物理设置：不能是 Kinematic，锁定旋转防止摔倒
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Update()
    {
        HandleInput();
        HandleRotation();
        UpdateAnimation();
    }

    void FixedUpdate()
    {
        DetectGround();
        HandleMovement();
    }

    void HandleInput()
    {
        if (!runtime.isGameRunning)
        {
            inputDirection = Vector3.zero;
            return;
        }

        float h = 0f;
        float v = 0f;

        if (Input.GetKey(KeyCode.W)) v -= 1f;
        if (Input.GetKey(KeyCode.S)) v += 1f;
        if (Input.GetKey(KeyCode.A)) h += 1f;
        if (Input.GetKey(KeyCode.D)) h -= 1f;

        inputDirection = new Vector3(h, 0f, v).normalized;
    }

    void HandleRotation()
    {
        if (inputDirection.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(inputDirection);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            rotateSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// 定向射线检测地面法线，用于坡面投影（FixedUpdate 调用）
    /// </summary>
    void DetectGround()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 1.5f; 
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, groundRayLength + 1.5f, groundLayer);

        bool foundGround = false;
        RaycastHit closestHit = new RaycastHit();
        float minDist = Mathf.Infinity;

        foreach (var hit in hits)
        {
            // 忽略自身以及子物体的碰撞体
            if (hit.collider.transform.IsChildOf(transform)) continue;
            // 忽略 Trigger (如任务区域点)
            if (hit.collider.isTrigger) continue;

            if (hit.distance < minDist)
            {
                minDist = hit.distance;
                closestHit = hit;
                foundGround = true;
            }
        }

        if (foundGround)
        {
            isGrounded = true;
            groundNormal = closestHit.normal;
            
            // 如果离地面非常近，我们消除额外的垂直速度以稳定在坡面上
            float distToGround = closestHit.point.y - transform.position.y;
            if (distToGround > -groundSnapOffset && distToGround < groundSnapOffset)
            {
                 // 物理引擎会自动处理轻微的贴地
            }
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
        }
    }

    [Header("Step Handling")]
    public float maxStepHeight = 0.3f; // 能跨过去的最大台阶高度
    public float stepCheckDist = 0.5f; // 检测前方障碍物的距离

    void HandleMovement()
    {
        float targetSpeed = inputDirection.magnitude * walkSpeed;
        
        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.velocity, groundNormal);
        float currentSpeed = planarVelocity.magnitude;

        if (targetSpeed > 0.01f)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);
            Vector3 targetDir = Vector3.ProjectOnPlane(inputDirection, groundNormal).normalized;
            planarVelocity = targetDir * currentSpeed;

            // 如果正在移动且在地面上，检测是否需要跨台阶
            if (isGrounded)
            {
                HandleStepHandling(targetDir);
            }
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.fixedDeltaTime);
            planarVelocity = planarVelocity.normalized * currentSpeed;
        }

        if (isGrounded)
        {
            // 在地面时，严格控制速度，但不强行控制向下的巨大速度
            Vector3 newVelocity = planarVelocity;
            
            // 如果是在下坡或刚接触地面，保持一定的重力吸附速度避免抖动
            if (rb.velocity.y < 0)
            {
                newVelocity.y = rb.velocity.y; 
            }
            
            rb.velocity = newVelocity;
        }
        else
        {
            // 在空中时，不要重置垂直速度，让物理引擎和自己加的重力积累
            Vector3 vel = rb.velocity;
            vel.x = planarVelocity.x;
            vel.z = planarVelocity.z;
            vel.y -= gravity * Time.fixedDeltaTime; // 额外重力，物理系统自己也会加
            rb.velocity = vel;
        }
    }

    /// <summary>
    /// 检测前方是否有矮台阶，如果有，给刚体一个向上的微小推力跨过去
    /// </summary>
    void HandleStepHandling(Vector3 moveDir)
    {
        // 从脚底偏上一点点发射射线检测前方是否有阻挡
        Vector3 lowerOrigin = transform.position + Vector3.up * 0.05f;
        
        RaycastHit[] lowerHits = Physics.RaycastAll(lowerOrigin, moveDir, stepCheckDist, groundLayer);
        bool hasLowerObstacle = false;

        foreach (var hit in lowerHits)
        {
             // 忽略自身和Trigger(比如打点区域)
             if (hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform)) continue;
             hasLowerObstacle = true;
             break;
        }

        if (hasLowerObstacle)
        {
            // 在能跨越的最大高度发射第二条射线
            Vector3 upperOrigin = transform.position + Vector3.up * maxStepHeight;
            RaycastHit[] upperHits = Physics.RaycastAll(upperOrigin, moveDir, stepCheckDist + 0.1f, groundLayer);
            bool hasUpperObstacle = false;

            foreach (var hit in upperHits)
            {
                 if (hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform)) continue;
                 hasUpperObstacle = true;
                 break;
            }
            
            // 如果上面没挡住（说明这是一个矮台阶而不是一堵高墙）
            if (!hasUpperObstacle)
            {
                // 给一个强制的位置提升来跨越台阶
                rb.position += Vector3.up * (maxStepHeight * Time.fixedDeltaTime * 10f);
            }
        }
    }

    void UpdateAnimation()
    {
        Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        bool isWalking = horizontalVelocity.magnitude > 0.1f;
        animator.SetBool("isWalking", isWalking);
    }
}
