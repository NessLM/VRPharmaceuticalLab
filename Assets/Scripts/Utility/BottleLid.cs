using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// VRChat-style removable lid/cap for bottles and jars (botol CTM, toples difenhidramin, dll).
///
/// Setup:
///   1. Lid GameObject: add XRGrabInteractable + Rigidbody + BottleLid.
///   2. Create an empty child on the bottle mouth → assign as closedAnchor.
///   3. Set bottleRoot to the bottle's root Transform.
///   4. Wire onOpened / onClosed events as needed (e.g., to PillBottleController).
///
/// Behavior:
///   - Grab lid → lid detaches from bottle (IsOpen = true).
///   - Release lid near bottle mouth (within snapRadius) → auto-snaps back (IsOpen = false).
///   - Release lid far from bottle → drops with gravity (pick it up and bring back to close again).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public class BottleLid : MonoBehaviour
{
    [Header("Bottle Reference")]
    [Tooltip("Root Transform of the bottle. Lid re-parents here when closed.")]
    [SerializeField] private Transform bottleRoot;

    [Header("Closed Anchor")]
    [Tooltip("Empty Transform placed at the bottle mouth (world position where lid sits when closed).")]
    [SerializeField] private Transform closedAnchor;
    [Tooltip("Distance (world units) within which releasing the lid triggers auto-snap back to bottle.")]
    [SerializeField] private float snapRadius = 0.08f;

    [Header("Physics")]
    [Tooltip("When dropped far from bottle, lid falls with gravity.")]
    [SerializeField] private bool useGravityWhenDropped = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    [Header("Events")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    /// <summary>True when the lid has been removed from the bottle.</summary>
    public bool IsOpen { get; private set; }

    private XRGrabInteractable _grab;
    private Rigidbody _rb;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        _grab.selectEntered.AddListener(OnGrabbed);
        _grab.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        _grab.selectEntered.RemoveListener(OnGrabbed);
        _grab.selectExited.RemoveListener(OnReleased);
    }

    private void Start()
    {
        // Ensure lid is closed at start
        SnapToClosed(fireEvent: false);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        // Detach from bottle so it can be freely moved
        transform.SetParent(null, true);
        IsOpen = true;
        _rb.useGravity = false; // XRI controls position while held
        onOpened?.Invoke();
        Log($"{gameObject.name} opened");
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (closedAnchor != null)
        {
            float dist = Vector3.Distance(transform.position, closedAnchor.position);
            if (dist <= snapRadius)
            {
                SnapToClosed(fireEvent: true);
                return;
            }
        }

        DropLid();
    }

    private void SnapToClosed(bool fireEvent)
    {
        IsOpen = false;

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;
        _rb.useGravity = false;

        if (bottleRoot != null)
            transform.SetParent(bottleRoot, true);

        if (closedAnchor != null)
        {
            transform.SetPositionAndRotation(closedAnchor.position, closedAnchor.rotation);
        }

        if (fireEvent)
        {
            onClosed?.Invoke();
            Log($"{gameObject.name} closed (snapped)");
        }
    }

    private void DropLid()
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        _rb.useGravity = false;
        _rb.isKinematic = true;

        Log($"{gameObject.name} dropped and frozen in air");
    }
    /// <summary>Programmatically force-closes the lid (re-snaps to bottle).</summary>
    public void ForceClose()
    {
        SnapToClosed(fireEvent: true);
    }

    /// <summary>Programmatically force-opens the lid (detaches without grab).</summary>
    public void ForceOpen()
    {
        transform.SetParent(null, true);
        IsOpen = true;
        _rb.isKinematic = false;
        _rb.useGravity = useGravityWhenDropped;
        onOpened?.Invoke();
    }

    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"[BottleLid] {message}", this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (closedAnchor == null) return;

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawSphere(closedAnchor.position, snapRadius);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.85f);
        Gizmos.DrawWireSphere(closedAnchor.position, snapRadius);
        Gizmos.DrawLine(transform.position, closedAnchor.position);
    }
#endif
}
