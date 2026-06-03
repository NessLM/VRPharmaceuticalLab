using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody))]
public class ToolSurfaceSnap : MonoBehaviour
{
    [Header("Snap Rotation")]
    [SerializeField] private bool resetRotationOnSnap = true;

    [Header("Snap Movement")]
    [SerializeField] private float snapSpeed = 20f;
    [SerializeField] private float rotationSpeed = 20f;

    [Header("Stick To Surface")]
    [SerializeField] private bool stickAfterSnap = true;

    private Rigidbody rb;
    private XRGrabInteractable grabInteractable;

    private SurfaceSnapZone currentSnapZone;

    private Quaternion startRotation;
    private Vector3 targetPosition;
    private Quaternion targetRotation;

    private bool isHeld;
    private bool isSnapping;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();

        startRotation = transform.rotation;
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrab);
            grabInteractable.selectExited.AddListener(OnRelease);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrab);
            grabInteractable.selectExited.RemoveListener(OnRelease);
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        isHeld = true;
        isSnapping = false;

        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Kinematic XRI movement type requires isKinematic=true at all times for
        // MovePosition/MoveRotation. Only switch to non-kinematic for VelocityTracking
        // or Instantaneous modes where XRI drives the Rigidbody via physics directly.
        bool requiresNonKinematic = grabInteractable != null
            && grabInteractable.movementType != XRBaseInteractable.MovementType.Kinematic;
        if (requiresNonKinematic)
            rb.isKinematic = false;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        isHeld = false;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (currentSnapZone != null)
        {
            SnapToSurface(currentSnapZone);
        }
        else
        {
            if (stickAfterSnap)
                rb.isKinematic = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        SurfaceSnapZone zone = other.GetComponent<SurfaceSnapZone>();

        if (zone != null)
        {
            currentSnapZone = zone;

            if (!isHeld)
                SnapToSurface(zone);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        SurfaceSnapZone zone = other.GetComponent<SurfaceSnapZone>();

        if (zone != null && zone == currentSnapZone)
        {
            currentSnapZone = null;
        }
    }

    private void SnapToSurface(SurfaceSnapZone zone)
    {
        Vector3 pos = transform.position;

        targetPosition = new Vector3(
            pos.x,
            zone.SurfaceY,
            pos.z
        );

        targetRotation = resetRotationOnSnap ? startRotation : transform.rotation;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;

        isSnapping = true;
    }

    private void Update()
    {
        if (!isSnapping)
            return;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * snapSpeed
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSpeed
        );

        float posDistance = Vector3.Distance(transform.position, targetPosition);
        float rotDistance = Quaternion.Angle(transform.rotation, targetRotation);

        if (posDistance < 0.003f && rotDistance < 0.5f)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (stickAfterSnap)
                rb.isKinematic = true;

            isSnapping = false;
        }
    }
}