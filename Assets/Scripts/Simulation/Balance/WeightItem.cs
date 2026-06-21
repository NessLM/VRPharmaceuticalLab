using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;

/// <summary>
/// Weight item for analytical balance.
/// - Gram Value is set manually in Inspector.
/// - Rigidbody.mass is only VR physics feel.
/// - Optional tray lock keeps small weights quiet before first grab.
/// - GrabCollider is only a trigger helper for easy grabbing, not physical collision.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
public class WeightItem : MonoBehaviour
{
    [Header("Weight Data")]
    [Tooltip("Logical mass in grams. Example: 0.005 = 5 mg, 0.1 = 100 mg, 1 = 1 gram.")]
    [SerializeField] private float gramValue = 1f;

    [SerializeField] private bool isParchment = false;
    [SerializeField] private bool countParchmentMass = false;

    [Header("Physics")]
    [Tooltip("Only for VR physics feel. This is NOT the balance gram value.")]
    [SerializeField] private float physicsMassKg = 0.05f;

    [SerializeField] private bool useGravityWhenFree = true;
    [SerializeField] private bool keepKinematicWhenReleased = false;

    [Header("Tray Storage")]
    [Tooltip("Keeps the weight frozen in the tray until grabbed once.")]
    [SerializeField] private bool startsLockedInTray = true;

    [Tooltip("After the first release, object returns to normal gravity physics.")]
    [SerializeField] private bool useGravityAfterFirstRelease = true;

    [Header("Grab Helper Collider")]
    [Tooltip("Creates or configures child object named GrabCollider.")]
    [SerializeField] private bool useGrabHelperCollider = true;

    [Tooltip("The helper collider must be trigger so it does not push the scale pan.")]
    [SerializeField] private bool forceGrabHelperAsTrigger = true;

    [Tooltip("Disable big GrabCollider while selected. Main collider remains active.")]
    [SerializeField] private bool disableGrabHelperWhileHeld = true;

    [Tooltip("Smallest world-space side length for easy XR grabbing.")]
    [SerializeField] private float minimumGrabColliderWorldSize = 0.04f;

    [SerializeField] private Vector3 fallbackGrabColliderSize = new Vector3(0.05f, 0.05f, 0.05f);

    [Header("Events")]
    public UnityEvent onPickedUp;
    public UnityEvent onPlaced;

    private Rigidbody rb;
    private XRGrabInteractable grabInteractable;
    private BoxCollider grabHelperCollider;
    private bool hasBeenPickedUp;

    public float GramValue => gramValue;

    public float Grams
    {
        get => gramValue;
        set => gramValue = Mathf.Max(0f, value);
    }

    public bool IsParchment => isParchment;
    public bool ShouldContributeMass => !isParchment || countParchmentMass;
    public bool IsHeld => grabInteractable != null && grabInteractable.isSelected;
    public bool HasBeenPickedUp => hasBeenPickedUp;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();

        ConfigureRigidbody();
        ConfigureGrabHelperCollider();

        if (startsLockedInTray && !hasBeenPickedUp)
            LockInTray();
        else
            ApplyReleasedPhysics();
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }

        if (useGrabHelperCollider)
            ConfigureGrabHelperCollider();

        if (startsLockedInTray && !hasBeenPickedUp)
            LockInTray();
    }

    private void OnDisable()
    {
        if (grabInteractable == null)
            return;

        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    private void ConfigureRigidbody()
    {
        if (rb == null)
            return;

        rb.mass = physicsMassKg;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        hasBeenPickedUp = true;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        SetGrabHelperEnabled(!disableGrabHelperWhileHeld);

        onPickedUp?.Invoke();
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            keepKinematicWhenReleased = false;
            useGravityWhenFree = useGravityAfterFirstRelease;

            ApplyReleasedPhysics();
        }

        SetGrabHelperEnabled(true);

        onPlaced?.Invoke();
    }

    private void ApplyReleasedPhysics()
    {
        if (rb == null)
            return;

        rb.isKinematic = keepKinematicWhenReleased;
        rb.useGravity = useGravityWhenFree && !keepKinematicWhenReleased;
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

        SetGrabHelperEnabled(true);
    }

    [ContextMenu("Unlock Physics")]
    public void UnlockPhysics()
    {
        hasBeenPickedUp = true;
        keepKinematicWhenReleased = false;
        useGravityWhenFree = useGravityAfterFirstRelease;
        ApplyReleasedPhysics();
        SetGrabHelperEnabled(true);
    }

    public void Settle()
    {
        if (rb == null)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public void ConfigureReleasedPhysics(bool useGravity, bool keepKinematic)
    {
        useGravityWhenFree = useGravity;
        keepKinematicWhenReleased = keepKinematic;

        if (rb != null && !IsHeld)
            ApplyReleasedPhysics();
    }

    public void ResetInteractionState()
    {
        hasBeenPickedUp = false;

        if (startsLockedInTray)
            LockInTray();
        else
            ApplyReleasedPhysics();

        SetGrabHelperEnabled(true);
    }

    private void ConfigureGrabHelperCollider()
    {
        if (!useGrabHelperCollider || grabInteractable == null)
            return;

        grabHelperCollider = GetOrCreateGrabHelperCollider();

        if (grabHelperCollider == null)
            return;

        if (forceGrabHelperAsTrigger)
            grabHelperCollider.isTrigger = true;

        grabHelperCollider.enabled = true;

        AddUsableCollidersToXRGrab();
    }

    private BoxCollider GetOrCreateGrabHelperCollider()
    {
        Transform helperTransform = transform.Find("GrabCollider");

        if (helperTransform == null)
        {
            GameObject helper = new GameObject("GrabCollider");
            helper.transform.SetParent(transform, false);
            helper.layer = gameObject.layer;
            helperTransform = helper.transform;
        }

        BoxCollider box = helperTransform.GetComponent<BoxCollider>();

        if (box == null)
            box = helperTransform.gameObject.AddComponent<BoxCollider>();

        helperTransform.localPosition = Vector3.zero;
        helperTransform.localRotation = Quaternion.identity;
        helperTransform.localScale = Vector3.one;

        ResizeGrabHelperCollider(helperTransform, box);

        return box;
    }

    private void ResizeGrabHelperCollider(Transform helperTransform, BoxCollider box)
    {
        if (TryGetRendererBounds(out Bounds rendererBounds))
        {
            Vector3 minWorldSize = Vector3.one * Mathf.Max(0.005f, minimumGrabColliderWorldSize);
            Vector3 worldSize = Vector3.Max(rendererBounds.size, minWorldSize);
            Vector3 scale = transform.lossyScale;

            box.center = helperTransform.InverseTransformPoint(rendererBounds.center);

            box.size = new Vector3(
                SafeDivide(worldSize.x, Mathf.Abs(scale.x)),
                SafeDivide(worldSize.y, Mathf.Abs(scale.y)),
                SafeDivide(worldSize.z, Mathf.Abs(scale.z))
            );
        }
        else
        {
            box.center = Vector3.zero;
            box.size = fallbackGrabColliderSize;
        }

        box.isTrigger = true;
    }

    private void AddUsableCollidersToXRGrab()
    {
        if (grabInteractable == null)
            return;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        grabInteractable.colliders.Clear();

        foreach (Collider col in colliders)
        {
            if (col == null || !col.enabled)
                continue;

            grabInteractable.colliders.Add(col);
        }
    }

    private void SetGrabHelperEnabled(bool enabled)
    {
        if (grabHelperCollider == null)
        {
            Transform helper = transform.Find("GrabCollider");
            if (helper != null)
                grabHelperCollider = helper.GetComponent<BoxCollider>();
        }

        if (grabHelperCollider != null)
        {
            grabHelperCollider.isTrigger = true;
            grabHelperCollider.enabled = enabled;
        }
    }

    private bool TryGetRendererBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        foreach (Renderer itemRenderer in renderers)
        {
            if (itemRenderer == null || IsIgnoredHelperObject(itemRenderer.transform))
                continue;

            if (!hasBounds)
            {
                bounds = itemRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(itemRenderer.bounds);
            }
        }

        return hasBounds;
    }

    private bool IsIgnoredHelperObject(Transform candidate)
    {
        Transform current = candidate;

        while (current != null && current != transform.parent)
        {
            string objectName = current.name;

            if (objectName.Contains("GrabCollider") ||
                objectName.Contains("AttachPoint") ||
                objectName.Contains("PhysicsCollider"))
                return true;

            if (current == transform)
                break;

            current = current.parent;
        }

        return false;
    }

    private float SafeDivide(float value, float divisor)
    {
        return divisor > 0.0001f ? value / divisor : value;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        gramValue = Mathf.Max(0f, gramValue);
        physicsMassKg = Mathf.Max(0.001f, physicsMassKg);
        minimumGrabColliderWorldSize = Mathf.Max(0.005f, minimumGrabColliderWorldSize);
        fallbackGrabColliderSize = Vector3.Max(fallbackGrabColliderSize, Vector3.one * 0.005f);

        if (!isParchment && gramValue <= 0f)
            Debug.LogWarning($"[WeightItem] {name} has Gram Value <= 0.", this);

        BoxCollider existingGrabCollider = null;

        Transform helper = transform.Find("GrabCollider");
        if (helper != null)
            existingGrabCollider = helper.GetComponent<BoxCollider>();

        if (existingGrabCollider != null && forceGrabHelperAsTrigger)
            existingGrabCollider.isTrigger = true;
    }
#endif
}
