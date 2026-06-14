using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

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
    [SerializeField] private bool isParchment = false;
    [SerializeField] private bool countParchmentMass = false;

    [Header("Physics")]
    [Tooltip("Rigidbody mass kept constant for stable VR grab feel regardless of gramValue.")]
    [SerializeField] private float physicsMassKg = 0.05f;
    [SerializeField] private bool useGravityWhenFree = true;
    [SerializeField] private bool keepKinematicWhenReleased = false;

    [Header("Grab Setup")]
    [SerializeField] private bool autoConfigureGrabColliders = true;
    [SerializeField] private bool createGrabColliderWhenNeeded = true;
    [Tooltip("Smallest world-space side length for the invisible helper grab collider.")]
    [SerializeField] private float minimumGrabColliderWorldSize = 0.04f;
    [SerializeField] private Vector3 fallbackGrabColliderSize = new Vector3(0.05f, 0.05f, 0.05f);

    [Header("Events")]
    public UnityEvent onPickedUp;
    public UnityEvent onPlaced;

    private Rigidbody rb;
    private XRGrabInteractable grabInteractable;

    /// <summary>Logical mass in grams for balance calculation.</summary>
    public float GramValue => gramValue;
    public float Grams
    {
        get => gramValue;
        set => gramValue = Mathf.Max(0f, value);
    }

    public bool IsParchment => isParchment;
    public bool ShouldContributeMass => !isParchment || countParchmentMass;

    /// <summary>True while this weight is held by an XR controller.</summary>
    public bool IsHeld => grabInteractable != null && grabInteractable.isSelected;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();

        EnsureGrabColliderSetup();

        if (rb != null)
        {
            rb.mass = physicsMassKg;
            ApplyReleasedPhysics();
        }
    }

    private void Start()
    {
        if (grabInteractable == null)
            return;

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
        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.useGravity = false; // XRI tracks movement; gravity causes unwanted drops
        onPickedUp?.Invoke();
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (rb == null)
            return;

        // Zero velocity to prevent unexpected sliding when placed on pan
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        ApplyReleasedPhysics();
        onPlaced?.Invoke();
    }

    /// <summary>Forces the weight to settle at its current position (kinematic, zero velocity).</summary>
    public void Settle()
    {
        if (rb == null)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    public void ConfigureReleasedPhysics(bool useGravity, bool keepKinematic)
    {
        useGravityWhenFree = useGravity;
        keepKinematicWhenReleased = keepKinematic;
        if (rb != null && !IsHeld)
            ApplyReleasedPhysics();
    }

    private void ApplyReleasedPhysics()
    {
        if (rb == null)
            return;

        rb.isKinematic = keepKinematicWhenReleased;
        rb.useGravity = useGravityWhenFree && !keepKinematicWhenReleased;
    }

    [ContextMenu("InferGramsFromName")]
    public void InferGramsFromName()
    {
        if (TryInferGramsFromName(name, out float inferredGrams))
            Grams = inferredGrams;
    }

    private void EnsureGrabColliderSetup()
    {
        if (!autoConfigureGrabColliders || grabInteractable == null)
            return;

        List<Collider> usableColliders = CollectUsableGrabColliders();

        if (createGrabColliderWhenNeeded && NeedsHelperGrabCollider(usableColliders))
        {
            BoxCollider helper = GetOrCreateHelperGrabCollider();
            if (helper != null && !usableColliders.Contains(helper))
                usableColliders.Add(helper);
        }

        if (usableColliders.Count == 0)
        {
            Debug.LogWarning($"[WeightItem] {name} has no usable non-trigger collider for XR grab.", this);
            return;
        }

        if (HasValidColliderSetup(usableColliders))
            return;

        grabInteractable.colliders.Clear();
        foreach (Collider grabCollider in usableColliders)
            grabInteractable.colliders.Add(grabCollider);
    }

    private List<Collider> CollectUsableGrabColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        List<Collider> usableColliders = new List<Collider>();

        foreach (Collider candidate in colliders)
        {
            if (candidate == null || !candidate.enabled)
                continue;

            usableColliders.Add(candidate);
        }

        return usableColliders;
    }

    private bool HasValidColliderSetup(List<Collider> usableColliders)
    {
        if (grabInteractable.colliders == null || grabInteractable.colliders.Count == 0)
            return false;

        foreach (Collider configuredCollider in grabInteractable.colliders)
        {
            if (configuredCollider == null || !usableColliders.Contains(configuredCollider))
                return false;
        }

        return true;
    }

    private bool NeedsHelperGrabCollider(List<Collider> usableColliders)
    {
        if (usableColliders == null || usableColliders.Count == 0)
            return true;

        float minSize = Mathf.Max(0.005f, minimumGrabColliderWorldSize);

        foreach (Collider candidate in usableColliders)
        {
            if (candidate == null)
                continue;

            Vector3 size = candidate.bounds.size;
            if (size.x >= minSize && size.y >= minSize && size.z >= minSize)
                return false;
        }

        return true;
    }

    private BoxCollider GetOrCreateHelperGrabCollider()
    {
        Transform helperTransform = transform.Find("GrabCollider");

        if (helperTransform == null)
        {
            GameObject helper = new GameObject("GrabCollider");
            helper.layer = gameObject.layer;
            helper.transform.SetParent(transform, false);
            helperTransform = helper.transform;
        }

        BoxCollider box = helperTransform.GetComponent<BoxCollider>();
        if (box == null)
            box = helperTransform.gameObject.AddComponent<BoxCollider>();

        box.isTrigger = true;
        box.enabled = true;
        ResizeHelperGrabCollider(helperTransform, box);
        return box;
    }

    private void ResizeHelperGrabCollider(Transform helperTransform, BoxCollider box)
    {
        helperTransform.localPosition = Vector3.zero;
        helperTransform.localRotation = Quaternion.identity;
        helperTransform.localScale = Vector3.one;

        if (TryGetRendererBounds(out Bounds rendererBounds))
        {
            Vector3 minWorldSize = Vector3.one * Mathf.Max(0.005f, minimumGrabColliderWorldSize);
            Vector3 worldSize = Vector3.Max(rendererBounds.size, minWorldSize);
            Vector3 scale = transform.lossyScale;

            box.center = helperTransform.InverseTransformPoint(rendererBounds.center);
            box.size = new Vector3(
                SafeDivide(worldSize.x, Mathf.Abs(scale.x)),
                SafeDivide(worldSize.y, Mathf.Abs(scale.y)),
                SafeDivide(worldSize.z, Mathf.Abs(scale.z)));
        }
        else
        {
            box.center = Vector3.zero;
            box.size = fallbackGrabColliderSize;
        }
    }

    private bool TryGetRendererBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        foreach (Renderer itemRenderer in renderers)
        {
            if (itemRenderer == null ||
                itemRenderer.transform.name.Contains("GrabCollider") ||
                itemRenderer.transform.name.Contains("AttachPoint") ||
                itemRenderer.transform.name.Contains("PhysicsCollider"))
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

    private float SafeDivide(float value, float divisor)
    {
        return divisor > 0.0001f ? value / divisor : value;
    }

    private bool TryInferGramsFromName(string objectName, out float inferredGrams)
    {
        inferredGrams = 0f;

        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        Match match = Regex.Match(objectName, @"(\d+(?:[\.,]\d+)?)");
        if (!match.Success)
            return false;

        string number = match.Groups[1].Value.Replace(',', '.');
        if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            return false;

        bool milligrams =
            objectName.IndexOf("anakTimbangan", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("mg", System.StringComparison.OrdinalIgnoreCase) >= 0;

        inferredGrams = milligrams ? parsed / 1000f : parsed;
        return inferredGrams > 0f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (gramValue <= 0f && !isParchment && TryInferGramsFromName(name, out float inferredGrams))
            gramValue = inferredGrams;

        gramValue = Mathf.Max(0f, gramValue);
        physicsMassKg = Mathf.Max(0.001f, physicsMassKg);
        minimumGrabColliderWorldSize = Mathf.Max(0.005f, minimumGrabColliderWorldSize);
        fallbackGrabColliderSize = Vector3.Max(fallbackGrabColliderSize, Vector3.one * 0.005f);

        if (!isParchment && gramValue <= 0f)
            Debug.LogWarning($"[WeightItem] {name} has Grams <= 0 and will not add useful mass.", this);
    }
#endif
}
