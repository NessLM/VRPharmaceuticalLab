using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
public class PerkamenSnapTarget : MonoBehaviour
{
    [Header("Snap Target")]
    [FormerlySerializedAs("panTransform")]
    [SerializeField] private Transform snapTransform;

    [FormerlySerializedAs("worldOffset")]
    [SerializeField] private Vector3 snapOffset = new Vector3(0f, 0.025f, 0f);

    [SerializeField] private bool useRendererTopSurface = true;
    [SerializeField] private bool alignZoneToSnapPoint = true;

    [Header("Zone Collider")]
    [FormerlySerializedAs("triggerCollider")]
    [SerializeField] private Collider triggerCollider;

    [FormerlySerializedAs("triggerSize")]
    [SerializeField] private Vector3 zoneSize = new Vector3(0.24f, 0.14f, 0.24f);

    [SerializeField] private bool overrideBoxColliderSize;

    [Header("Snap Behaviour")]
    [SerializeField] private bool parentToPanAfterSnap = true;
    [SerializeField] private bool disableGrabAfterSnap = true;
    [SerializeField] private bool lockRigidbodyWhileSnapped = true;
    [SerializeField] private bool disableCollidersAfterSnap = true;

    [SerializeField] private bool requireRecentRelease = true;
    [SerializeField] private float releaseSnapWindow = 0.8f;
    [SerializeField] private bool allowReplaceSnappedParchment;

    [Header("Events")]
    [FormerlySerializedAs("onPerkamenSnapped")]
    [SerializeField] private UnityEvent onParchmentSnapped = new UnityEvent();

    public UnityEvent<GameObject> onParchmentObjectSnapped = new UnityEvent<GameObject>();
    public UnityEvent<GameObject> onParchmentRemoved = new UnityEvent<GameObject>();

    private bool hasSnapped;
    private XRGrabInteractable snappedParchment;
    private Rigidbody snappedRigidbody;
    private Transform originalParent;

    private Collider[] snappedColliders;
    private bool[] snappedColliderStates;

    public bool HasSnapped => hasSnapped && snappedParchment != null && IsParchment(snappedParchment.gameObject);
    public GameObject SnappedParchment => snappedParchment != null ? snappedParchment.gameObject : null;
    public GameObject SnappedPerkamen => SnappedParchment;

    public void Configure(Transform targetPan, Vector3 offset, Vector3 size)
    {
        snapTransform = targetPan;
        snapOffset = offset;
        zoneSize = size;
        overrideBoxColliderSize = true;
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        ResolveSnapTransform();
        EnsureTriggerCollider();
        UpdateZonePose();
    }

    private void OnDisable()
    {
        UnsubscribeFromSnappedParchment();
    }

    private void LateUpdate()
    {
        ResolveSnapTransform();

        if (!HasSnapped)
            UpdateZonePose();

        if (hasSnapped && snappedParchment == null)
            ClearSnapState(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        TrySnap(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TrySnap(other);
    }

    private void TrySnap(Collider other)
    {
        if (other == null)
            return;

        if (HasSnapped && !allowReplaceSnappedParchment)
            return;

        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();

        if (grab == null || !IsParchment(grab.gameObject))
            return;

        if (grab == snappedParchment)
            return;

        if (grab.isSelected)
            return;

        if (requireRecentRelease)
        {
            PerkamenNoGravity noGravity = grab.GetComponent<PerkamenNoGravity>();

            if (noGravity == null || !noGravity.HasBeenGrabbed || !noGravity.WasRecentlyReleased(releaseSnapWindow))
                return;
        }

        SnapParchment(grab);
    }

    private void SnapParchment(XRGrabInteractable grab)
    {
        if (grab == null)
            return;

        if (HasSnapped && allowReplaceSnappedParchment)
            ClearSnapState(false);

        Transform parchment = grab.transform;
        Rigidbody rb = parchment.GetComponent<Rigidbody>();

        hasSnapped = true;
        snappedParchment = grab;
        snappedRigidbody = rb;
        originalParent = parchment.parent;

        Transform target = snapTransform != null ? snapTransform : transform;

        if (parentToPanAfterSnap && target != null)
            parchment.SetParent(target, true);

        parchment.position = GetSnapPosition();
        parchment.rotation = GetFlatRotation(target);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;

            if (lockRigidbodyWhileSnapped)
                rb.isKinematic = true;
        }

        PerkamenNoGravity physicsState = grab.GetComponent<PerkamenNoGravity>();

        if (physicsState != null)
            physicsState.ApplySnappedPhysics();

        if (disableCollidersAfterSnap)
            SetParchmentCollidersEnabled(grab, false);

        if (disableGrabAfterSnap)
        {
            grab.enabled = false;
        }
        else
        {
            grab.enabled = true;
            SubscribeToSnappedParchment();
        }

        onParchmentSnapped?.Invoke();
        onParchmentObjectSnapped?.Invoke(grab.gameObject);
    }

    public void ClearSnapState()
    {
        ClearSnapState(false);
    }

    private void ClearSnapState(bool prepareForGrab)
    {
        XRGrabInteractable oldGrab = snappedParchment;
        Rigidbody oldRb = snappedRigidbody;
        Transform oldParent = originalParent;

        UnsubscribeFromSnappedParchment();

        hasSnapped = false;
        snappedParchment = null;
        snappedRigidbody = null;
        originalParent = null;

        if (oldGrab == null)
            return;

        RestoreParchmentColliders();

        PerkamenNoGravity physicsState = oldGrab.GetComponent<PerkamenNoGravity>();

        if (prepareForGrab)
        {
            if (parentToPanAfterSnap && oldParent != null)
                oldGrab.transform.SetParent(oldParent, true);

            oldGrab.enabled = true;

            if (physicsState != null)
            {
                physicsState.ApplyHeldPhysics();
            }
            else if (oldRb != null)
            {
                oldRb.linearVelocity = Vector3.zero;
                oldRb.angularVelocity = Vector3.zero;
                oldRb.useGravity = false;
                oldRb.isKinematic = false;
            }
        }
        else
        {
            if (physicsState != null)
                physicsState.ApplyFreePhysics();
        }

        onParchmentRemoved?.Invoke(oldGrab.gameObject);
    }
    private void SetParchmentCollidersEnabled(XRGrabInteractable grab, bool enabled)
    {
        if (grab == null)
            return;

        snappedColliders = grab.GetComponentsInChildren<Collider>(true);
        snappedColliderStates = new bool[snappedColliders.Length];

        for (int i = 0; i < snappedColliders.Length; i++)
        {
            Collider col = snappedColliders[i];

            if (col == null)
                continue;

            snappedColliderStates[i] = col.enabled;
            col.enabled = enabled;
        }
    }

    private void RestoreParchmentColliders()
    {
        if (snappedColliders == null || snappedColliderStates == null)
            return;

        int count = Mathf.Min(snappedColliders.Length, snappedColliderStates.Length);

        for (int i = 0; i < count; i++)
        {
            if (snappedColliders[i] != null)
                snappedColliders[i].enabled = snappedColliderStates[i];
        }

        snappedColliders = null;
        snappedColliderStates = null;
    }

    private void OnSnappedParchmentSelected(SelectEnterEventArgs args)
    {
        ClearSnapState(true);
    }

    private void SubscribeToSnappedParchment()
    {
        if (snappedParchment == null)
            return;

        snappedParchment.selectEntered.RemoveListener(OnSnappedParchmentSelected);
        snappedParchment.selectEntered.AddListener(OnSnappedParchmentSelected);
    }

    private void UnsubscribeFromSnappedParchment()
    {
        if (snappedParchment == null)
            return;

        snappedParchment.selectEntered.RemoveListener(OnSnappedParchmentSelected);
    }

    private bool IsParchment(GameObject candidate)
    {
        if (candidate == null)
            return false;

        if (candidate.GetComponentInParent<StackPerkamenDispenser>() != null)
            return false;

        if (candidate.GetComponent<PerkamenNoGravity>() != null)
            return true;

        if (HasParchmentTag(candidate))
            return true;

        return IsSingleParchmentName(candidate.name);
    }

    private bool IsSingleParchmentName(string candidateName)
    {
        return !string.IsNullOrEmpty(candidateName) &&
               candidateName.IndexOf("singleperkamen", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool HasParchmentTag(GameObject candidate)
    {
        try
        {
            return candidate.CompareTag("Perkamen");
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private void EnsureTriggerCollider()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider == null)
            return;

        triggerCollider.isTrigger = true;

        if (overrideBoxColliderSize && triggerCollider is BoxCollider box)
        {
            box.size = zoneSize;
            box.center = Vector3.zero;
        }
    }

    private void ResolveSnapTransform()
    {
        if (snapTransform != null)
            return;

        bool isRightSide =
            name.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Kanan", System.StringComparison.OrdinalIgnoreCase) >= 0;

        string[] targetNames = isRightSide
            ? new[] { "Plate_Right_Target", "Balance_WeightRight" }
            : new[] { "Plate_Left_Target", "Balance_WeightLeft" };

        foreach (string targetName in targetNames)
        {
            snapTransform = FindSceneTransformByName(targetName);

            if (snapTransform != null)
                return;
        }
    }

    private Transform FindSceneTransformByName(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();

        foreach (Transform sceneTransform in transforms)
        {
            if (sceneTransform == null || sceneTransform.gameObject == null)
                continue;

            if (!sceneTransform.gameObject.scene.IsValid())
                continue;

            if (string.Equals(sceneTransform.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                return sceneTransform;
        }

        return null;
    }

    private Vector3 GetSnapPosition()
    {
        Transform target = snapTransform != null ? snapTransform : transform;

        if (useRendererTopSurface && TryGetRendererBounds(target, out Bounds bounds))
        {
            return new Vector3(
                bounds.center.x + snapOffset.x,
                bounds.max.y + snapOffset.y,
                bounds.center.z + snapOffset.z
            );
        }

        return target.position + snapOffset;
    }

    private void UpdateZonePose()
    {
        if (!alignZoneToSnapPoint || snapTransform == null)
            return;

        transform.position = GetSnapPosition();
        transform.rotation = Quaternion.identity;

        if (triggerCollider is BoxCollider box)
        {
            box.size = zoneSize;
            box.center = Vector3.zero;
        }
    }

    private bool TryGetRendererBounds(Transform target, out Bounds bounds)
    {
        bounds = new Bounds();

        if (target == null)
            return false;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null)
                continue;

            if (IsInParchmentHierarchy(targetRenderer.transform))
                continue;

            if (!hasBounds)
            {
                bounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(targetRenderer.bounds);
            }
        }

        return hasBounds;
    }

    private bool IsInParchmentHierarchy(Transform target)
    {
        Transform current = target;

        while (current != null)
        {
            if (current.name.IndexOf("perkamen", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (current == snapTransform)
                break;

            current = current.parent;
        }

        return false;
    }

    private Quaternion GetFlatRotation(Transform target)
    {
        if (target == null)
            return Quaternion.identity;

        Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }
}