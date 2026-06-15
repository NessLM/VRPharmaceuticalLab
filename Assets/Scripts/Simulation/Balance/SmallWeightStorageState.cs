using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Keeps small stored weights quiet in their tray until the player grabs them once.
/// After the first grab/release, normal gravity-driven physics is restored.
/// </summary>
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
public class SmallWeightStorageState : MonoBehaviour
{
    [SerializeField] private bool startsLockedInTray = true;
    [SerializeField] private bool useGravityAfterRelease = true;

    private Rigidbody rb;
    private XRGrabInteractable grabInteractable;
    private bool hasBeenPickedUp;

    public bool StartsLockedInTray => startsLockedInTray;
    public bool HasBeenPickedUp => hasBeenPickedUp;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (startsLockedInTray && !hasBeenPickedUp)
            LockInTray();
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
        }

        if (startsLockedInTray && !hasBeenPickedUp)
            LockInTray();
    }

    private void OnDisable()
    {
        if (grabInteractable == null)
            return;

        grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
        grabInteractable.selectExited.RemoveListener(OnSelectExited);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        hasBeenPickedUp = true;

        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.useGravity = false;
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (rb == null || !hasBeenPickedUp)
            return;

        rb.isKinematic = false;
        rb.useGravity = useGravityAfterRelease;
    }

    [ContextMenu("Lock In Tray")]
    public void LockInTray()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (rb == null)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;
    }
}
