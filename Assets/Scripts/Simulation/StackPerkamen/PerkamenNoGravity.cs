using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class PerkamenNoGravity : MonoBehaviour
{
    [Header("Free Physics")]
    [SerializeField] private bool useGravityWhenFree = true;
    [SerializeField] private float freeLinearDamping = 1f;
    [SerializeField] private float freeAngularDamping = 1f;

    [Header("Held Physics")]
    [SerializeField] private bool disableGravityWhileHeld = true;

    [Header("Snap Physics")]
    [SerializeField] private float snappedLinearDamping = 8f;
    [SerializeField] private float snappedAngularDamping = 8f;

    private Rigidbody rb;
    private XRGrabInteractable grab;

    public bool HasBeenGrabbed { get; private set; }
    public float LastReleasedTime { get; private set; } = -999f;
    public bool IsSnapped { get; private set; }

    public bool WasRecentlyReleased(float seconds)
    {
        return LastReleasedTime > 0f && Time.time - LastReleasedTime <= seconds;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();

        ApplyFreePhysics();
    }

    private void OnEnable()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (grab == null)
            grab = GetComponent<XRGrabInteractable>();

        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);

            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }
    }

    private void OnDisable()
    {
        if (grab == null)
            return;

        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        HasBeenGrabbed = true;
        IsSnapped = false;

        if (rb == null)
            return;

        rb.isKinematic = false;

        if (disableGravityWhileHeld)
            rb.useGravity = false;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        LastReleasedTime = Time.time;

        if (IsSnapped)
            return;

        ApplyFreePhysics();

        if (rb == null)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public void ApplyFreePhysics()
    {
        IsSnapped = false;

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.useGravity = useGravityWhenFree;
        rb.linearDamping = freeLinearDamping;
        rb.angularDamping = freeAngularDamping;
    }

    public void ApplyHeldPhysics()
    {
        IsSnapped = false;

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public void ApplySnappedPhysics()
    {
        IsSnapped = true;

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb == null)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.linearDamping = snappedLinearDamping;
        rb.angularDamping = snappedAngularDamping;
    }
}