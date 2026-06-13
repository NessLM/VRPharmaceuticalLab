using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class PerkamenNoGravity : MonoBehaviour
{
    private Rigidbody rb;
    private XRGrabInteractable grab;

    public bool HasBeenGrabbed { get; private set; }
    public float LastReleasedTime { get; private set; } = -999f;

    public bool WasRecentlyReleased(float seconds)
    {
        return LastReleasedTime > 0f && Time.time - LastReleasedTime <= seconds;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();

        ApplyNoGravity();
    }

    private void OnEnable()
    {
        if (grab == null)
            grab = GetComponent<XRGrabInteractable>();

        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
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
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        LastReleasedTime = Time.time;
        ApplyNoGravity();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void ApplyNoGravity()
    {
        rb.useGravity = false;
        rb.linearDamping = 8f;
        rb.angularDamping = 8f;
    }
}
