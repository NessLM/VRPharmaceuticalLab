using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
public class PerkamenSnapTarget : MonoBehaviour
{
    [Header("Snap References")]
    [SerializeField] private Transform plateTarget;
    [SerializeField] private Collider solidPlateCollider;
    [SerializeField] private Collider triggerCollider;

    [Header("Snap Position")]
    [SerializeField] private bool useSolidColliderCenterXZ = true;
    [SerializeField] private float surfacePadding = 0.004f;
    [SerializeField] private Vector3 extraWorldOffset = Vector3.zero;

    [Header("Snap Rotation")]
    [SerializeField] private bool usePlateTargetRotation = true;
    [SerializeField] private Vector3 extraEulerOffset = Vector3.zero;

    [Header("Snap Behaviour")]
    [SerializeField] private bool parentToPlateTarget = true;
    [SerializeField] private bool lockRigidbodyWhileSnapped = true;
    [SerializeField] private bool disableGrabAfterSnap = true;
    [SerializeField] private bool disableCollidersAfterSnap = false;

    [Header("Validation")]
    [SerializeField] private bool requireRecentRelease = true;
    [SerializeField] private float releaseSnapWindow = 0.8f;
    [SerializeField] private bool allowReplaceSnappedParchment = false;

    [Header("Events")]
    public UnityEvent onParchmentSnapped;
    public UnityEvent<GameObject> onParchmentObjectSnapped;
    public UnityEvent<GameObject> onParchmentRemoved;

    private XRGrabInteractable snappedGrab;
    private Rigidbody snappedRigidbody;
    private Transform originalParent;

    private Collider[] snappedColliders;
    private bool[] snappedColliderStates;

    public bool HasSnapped => snappedGrab != null;
    public GameObject SnappedParchment => snappedGrab != null ? snappedGrab.gameObject : null;
    public GameObject SnappedPerkamen => SnappedParchment;

    private void Awake()
    {
        ResolveReferences();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnDisable()
    {
        if (snappedGrab != null)
            snappedGrab.selectEntered.RemoveListener(OnSnappedGrabbed);
    }

    private void OnTriggerEnter(Collider other)
    {
        TrySnap(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TrySnap(other);
    }

    private void ResolveReferences()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (plateTarget == null)
        {
            if (name.ToLower().Contains("kiri"))
            {
                GameObject obj = GameObject.Find("Plate_Left_Target");
                if (obj != null)
                    plateTarget = obj.transform;
            }
            else if (name.ToLower().Contains("kanan"))
            {
                GameObject obj = GameObject.Find("Plate_Right_Target");
                if (obj != null)
                    plateTarget = obj.transform;
            }
        }

        if (solidPlateCollider == null)
        {
            if (name.ToLower().Contains("kiri"))
            {
                GameObject obj = GameObject.Find("Collider_Piring_kiri_solid");
                if (obj != null)
                    solidPlateCollider = obj.GetComponent<Collider>();
            }
            else if (name.ToLower().Contains("kanan"))
            {
                GameObject obj = GameObject.Find("Collider_Piring_Kanan_solid");
                if (obj != null)
                    solidPlateCollider = obj.GetComponent<Collider>();
            }
        }
    }

    private void TrySnap(Collider other)
    {
        if (other == null)
            return;

        if (HasSnapped && !allowReplaceSnappedParchment)
            return;

        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();

        if (grab == null)
            return;

        if (!IsParchment(grab.gameObject))
            return;

        if (grab == snappedGrab)
            return;

        if (grab.isSelected)
            return;

        if (requireRecentRelease)
        {
            PerkamenNoGravity state = grab.GetComponent<PerkamenNoGravity>();

            if (state == null)
                return;

            if (!state.HasBeenGrabbed)
                return;

            if (!state.WasRecentlyReleased(releaseSnapWindow))
                return;
        }

        SnapParchment(grab);
    }

    private bool IsParchment(GameObject obj)
    {
        if (obj == null)
            return false;

        if (obj.GetComponent<PerkamenNoGravity>() != null)
            return true;

        if (obj.CompareTag("Perkamen"))
            return true;

        string lower = obj.name.ToLowerInvariant();

        return lower.Contains("perkamen") || lower.Contains("parchment");
    }

    private void SnapParchment(XRGrabInteractable grab)
    {
        if (grab == null)
            return;

        if (HasSnapped && allowReplaceSnappedParchment)
            ClearSnapState(false);

        ResolveReferences();

        Transform parchment = grab.transform;
        Rigidbody rb = parchment.GetComponent<Rigidbody>();

        snappedGrab = grab;
        snappedRigidbody = rb;
        originalParent = parchment.parent;

        CacheColliderStates(parchment);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = false;
        }

        Transform parentTarget = plateTarget != null ? plateTarget : transform;

        if (parentToPlateTarget && parentTarget != null)
            parchment.SetParent(parentTarget, true);

        parchment.rotation = GetSnapRotation(parentTarget);
        parchment.position = GetInitialSnapPosition(parentTarget);

        CorrectParchmentBottomToSolidSurface(parchment);

        PerkamenNoGravity state = grab.GetComponent<PerkamenNoGravity>();

        if (state != null)
        {
            state.ApplySnappedPhysics();
        }
        else if (rb != null && lockRigidbodyWhileSnapped)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (disableCollidersAfterSnap)
            SetSnappedCollidersEnabled(false);

        grab.selectEntered.RemoveListener(OnSnappedGrabbed);
        grab.selectEntered.AddListener(OnSnappedGrabbed);

        if (disableGrabAfterSnap)
            grab.enabled = false;

        onParchmentSnapped?.Invoke();
        onParchmentObjectSnapped?.Invoke(parchment.gameObject);
    }

    private Vector3 GetInitialSnapPosition(Transform parentTarget)
    {
        Vector3 pos;

        if (solidPlateCollider != null && useSolidColliderCenterXZ)
        {
            Bounds b = solidPlateCollider.bounds;
            pos = new Vector3(b.center.x, b.max.y + surfacePadding, b.center.z);
        }
        else if (parentTarget != null)
        {
            pos = parentTarget.position + Vector3.up * surfacePadding;
        }
        else
        {
            pos = transform.position + Vector3.up * surfacePadding;
        }

        return pos + extraWorldOffset;
    }

    private Quaternion GetSnapRotation(Transform parentTarget)
    {
        Quaternion baseRotation = Quaternion.identity;

        if (usePlateTargetRotation && parentTarget != null)
            baseRotation = parentTarget.rotation;

        return baseRotation * Quaternion.Euler(extraEulerOffset);
    }

    private void CorrectParchmentBottomToSolidSurface(Transform parchment)
    {
        if (parchment == null)
            return;

        float surfaceTopY = GetSurfaceTopY();

        if (!TryGetWorldBounds(parchment, out Bounds parchmentBounds))
            return;

        float targetBottomY = surfaceTopY + surfacePadding;
        float deltaY = targetBottomY - parchmentBounds.min.y;

        parchment.position += Vector3.up * deltaY;
    }

    private float GetSurfaceTopY()
    {
        if (solidPlateCollider != null)
            return solidPlateCollider.bounds.max.y;

        if (triggerCollider != null)
            return triggerCollider.bounds.max.y;

        return transform.position.y;
    }

    private bool TryGetWorldBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds(root.position, Vector3.zero);
        bool initialized = false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled)
                continue;

            if (r.name.ToLowerInvariant().Contains("grab"))
                continue;

            if (!initialized)
            {
                bounds = r.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (initialized)
            return true;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        foreach (Collider c in colliders)
        {
            if (c == null || !c.enabled || c.isTrigger)
                continue;

            if (c.name.ToLowerInvariant().Contains("grab"))
                continue;

            if (!initialized)
            {
                bounds = c.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(c.bounds);
            }
        }

        return initialized;
    }

    private void CacheColliderStates(Transform root)
    {
        snappedColliders = root.GetComponentsInChildren<Collider>(true);
        snappedColliderStates = new bool[snappedColliders.Length];

        for (int i = 0; i < snappedColliders.Length; i++)
        {
            snappedColliderStates[i] = snappedColliders[i] != null && snappedColliders[i].enabled;
        }
    }

    private void SetSnappedCollidersEnabled(bool value)
    {
        if (snappedColliders == null)
            return;

        for (int i = 0; i < snappedColliders.Length; i++)
        {
            if (snappedColliders[i] != null)
                snappedColliders[i].enabled = value;
        }
    }

    private void RestoreSnappedColliderStates()
    {
        if (snappedColliders == null || snappedColliderStates == null)
            return;

        int count = Mathf.Min(snappedColliders.Length, snappedColliderStates.Length);

        for (int i = 0; i < count; i++)
        {
            if (snappedColliders[i] != null)
                snappedColliders[i].enabled = snappedColliderStates[i];
        }
    }

    private void OnSnappedGrabbed(SelectEnterEventArgs args)
    {
        if (snappedGrab == null)
            return;

        ClearSnapState(true);
    }

    public void ClearSnapState(bool restorePhysics)
    {
        GameObject oldObject = SnappedParchment;

        if (snappedGrab != null)
            snappedGrab.selectEntered.RemoveListener(OnSnappedGrabbed);

        RestoreSnappedColliderStates();

        if (restorePhysics && snappedGrab != null)
        {
            snappedGrab.enabled = true;

            Transform t = snappedGrab.transform;

            if (originalParent != null)
                t.SetParent(originalParent, true);

            if (snappedRigidbody != null)
            {
                snappedRigidbody.isKinematic = false;
                snappedRigidbody.useGravity = true;
                snappedRigidbody.linearVelocity = Vector3.zero;
                snappedRigidbody.angularVelocity = Vector3.zero;
            }

            PerkamenNoGravity state = snappedGrab.GetComponent<PerkamenNoGravity>();

            if (state != null)
                state.ApplyFreePhysics();
        }

        snappedGrab = null;
        snappedRigidbody = null;
        originalParent = null;
        snappedColliders = null;
        snappedColliderStates = null;

        if (oldObject != null)
            onParchmentRemoved?.Invoke(oldObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        surfacePadding = Mathf.Max(0f, surfacePadding);
        releaseSnapWindow = Mathf.Max(0.01f, releaseSnapWindow);

        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }
#endif
}