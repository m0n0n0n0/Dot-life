using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollowingScript : MonoBehaviour
{
    public Camera camera = null;
    public Transform target = null;
    public float distance = 5.0f;
    public float height = 5.0f;
    public float rotationDamping = 3.0f;

    public Vector3 finalPos;
    public Quaternion finalRotation;
    public float finalMoveDuration = 5.0f;

    [SerializeField]
    private GameState gameState;

    private bool isMovingToFinal = false;

    void Start()
    {
        gameState.OnGameOver += moveToFinal;

        finalRotation = Quaternion.Euler(30f, -90f, 0f);

        if (target == null)
        {
            target = this.transform;
        }

        Quaternion wantedRotation = Quaternion.LookRotation(target.position - (transform.position + Vector3.up * height), Vector3.up);
        Vector3 wantedPosition = target.position - (Vector3.forward * distance) + (Vector3.up * height) + (Vector3.right * distance / 2);

        camera.transform.position = Vector3.Lerp(camera.transform.position, wantedPosition, Time.deltaTime * rotationDamping);
        camera.transform.rotation = Quaternion.Lerp(camera.transform.rotation, wantedRotation, Time.deltaTime * rotationDamping);

        camera.transform.LookAt(target.position);
    }

    void LateUpdate()
    {
        if (target == null || camera == null || isMovingToFinal)
        {
            return;
        }

        Vector3 wantedPosition = target.position - (Vector3.forward * distance) + (Vector3.up * height) + (Vector3.right * distance / 2);

        camera.transform.position = Vector3.Lerp(camera.transform.position, wantedPosition, Time.deltaTime * rotationDamping);

        camera.transform.LookAt(target.position);
    }

    void moveToFinal()
    {
        isMovingToFinal = true;
        StartCoroutine(MoveToFinalCoroutine());
    }

    IEnumerator MoveToFinalCoroutine()
    {
        float elapsedTime = 0;
        Vector3 startingPos = camera.transform.position;
        Quaternion startingRot = camera.transform.rotation;

        while (elapsedTime < finalMoveDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / finalMoveDuration);

            camera.transform.position = Vector3.Lerp(startingPos, finalPos, t);
            camera.transform.rotation = Quaternion.Slerp(startingRot, finalRotation, t);

            yield return null;
        }

        camera.transform.position = finalPos;
        camera.transform.rotation = finalRotation;
    }
}
