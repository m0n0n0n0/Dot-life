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


    void Start()
    {
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
        if (target == null || camera == null)
        {
            return;
        }

        Vector3 wantedPosition = target.position - (Vector3.forward * distance) + (Vector3.up * height) + (Vector3.right * distance / 2);

        camera.transform.position = Vector3.Lerp(camera.transform.position, wantedPosition, Time.deltaTime * rotationDamping);

        camera.transform.LookAt(target.position);
    }
}
