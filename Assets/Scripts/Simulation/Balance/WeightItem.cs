using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Represents a grabbable standard weight for the MG analytical balance.
/// Exposes a logical gram value used by WeightingZone for balance calculation.
/// Rigidbody.mass is kept constant for stable VR handling and does NOT equal gramValue.
/// Attach to: WeightItem prefab root alongside XRGrabInteractable and a Collider.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
public class WeightItem : MonoBehaviour
{
    [Header("Weight Data")]
    [Tooltip("Logical mass in grams used for balance calculation.")]
    [SerializeField] private float gramValue = 1f;

    [Header("Physics")]
    [Tooltip("Rigidbody mass kept constant for stable VR grab feel regardless of gramValue.")]
    [SerializeField] private float physicsMassKg = 0.05f;

    [Header("Events")]
    public UnityEvent onPickedUp;
    public UnityEvent onPlaced;

    private Rigidbody rb;
    private XRGrabInteractable grabInteractable;

    /// <summary>Logical mass in grams for balance calculation.</summary>
    public float GramValue => gramValue;

    /// <summary>True while this weight is held by an XR controller.</summary>
    public bool IsHeld => grabInteractable != null && grabInteractable.isSelected;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();

        rb.mass = physicsMassKg;
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void Start()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDestroy()
    {
        if (grabInteractable == null) return;
        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        rb.isKinematic = false;
        rb.useGravity = false; // XRI tracks movement; gravity causes unwanted drops
        onPickedUp?.Invoke();
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // Zero velocity to prevent unexpected sliding when placed on pan
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;
        onPlaced?.Invoke();
    }

    /// <summary>Forces the weight to settle at its current position (kinematic, zero velocity).</summary>
    public void Settle()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        gramValue = Mathf.Max(0f, gramValue);
        physicsMassKg = Mathf.Max(0.001f, physicsMassKg);
    }
#endif
}
