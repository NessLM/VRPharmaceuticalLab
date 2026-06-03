using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody))]
public class NoGravityReleaseStabilizer : MonoBehaviour
{
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private bool freezeOnRelease = true;

    private Rigidbody body;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        StabilizeBody(freezeOnRelease);
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }

        if (body != null)
            body.useGravity = false;
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (body == null)
            return;

        // Kinematic movement type requires isKinematic=true at all times.
        // Only switch to non-kinematic for VelocityTracking or Instantaneous modes
        // where XRI uses the physics engine directly.
        bool requiresNonKinematic = grabInteractable != null
            && grabInteractable.movementType != XRBaseInteractable.MovementType.Kinematic;

        body.useGravity = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        if (requiresNonKinematic)
            body.isKinematic = false;
        // For Kinematic movement type: leave isKinematic as-is (true), XRI uses MovePosition/MoveRotation.
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        StabilizeBody(freezeOnRelease);
    }

    /// <summary>Zeroes velocities and optionally makes the body kinematic to freeze it in place.</summary>
    private void StabilizeBody(bool makeKinematic)
    {
        if (body == null)
            return;

        body.useGravity = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = makeKinematic;
    }
}
