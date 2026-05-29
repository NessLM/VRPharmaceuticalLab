using UnityEngine;

public class BillboardToCamera : MonoBehaviour
{
    [Header("Target Camera")]
    public Transform targetCamera;

    [Header("Settings")]
    public bool lockYRotation = true;
    public bool flipDirection = false;

    private void Start()
    {
        if (targetCamera == null && Camera.main != null)
        {
            targetCamera = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            return;

        Vector3 direction;

        if (flipDirection)
        {
            direction = targetCamera.position - transform.position;
        }
        else
        {
            direction = transform.position - targetCamera.position;
        }

        if (lockYRotation)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction);
    }
}