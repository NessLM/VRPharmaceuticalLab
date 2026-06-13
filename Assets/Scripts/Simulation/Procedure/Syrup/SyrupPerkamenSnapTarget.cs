using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class SyrupPerkamenSnapTarget : MonoBehaviour
{
    [SerializeField] private Transform panTransform;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.025f, 0f);
    [SerializeField] private Vector3 triggerSize = new Vector3(0.24f, 0.14f, 0.24f);
    [SerializeField] private bool disableGrabAfterSnap = true;
    [SerializeField] private bool parentToPanAfterSnap = true;
    [SerializeField] private bool requireRecentRelease = true;
    [SerializeField] private float releaseSnapWindow = 0.8f;
    [SerializeField] private BoxCollider triggerCollider;
    [SerializeField] private UnityEvent onPerkamenSnapped;

    private bool hasSnapped;
    private XRGrabInteractable snappedPerkamen;

    public bool HasSnapped => hasSnapped;
    public GameObject SnappedPerkamen => snappedPerkamen != null ? snappedPerkamen.gameObject : null;

    public void Configure(Transform targetPan, Vector3 offset, Vector3 size)
    {
        panTransform = targetPan;
        worldOffset = offset;
        triggerSize = size;
        EnsureTriggerCollider();
        UpdatePose();
    }

    private void Awake()
    {
        ResolvePanTransform();
        EnsureTriggerCollider();
        UpdatePose();
    }

    private void LateUpdate()
    {
        if (!hasSnapped)
            UpdatePose();
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
        if (hasSnapped || other == null)
            return;

        ResolvePanTransform();
        if (panTransform == null)
            return;

        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();
        if (grab == null || !IsPerkamen(grab.gameObject))
            return;

        // Tunggu pemain benar-benar melepas perkamen sebelum dikunci ke piring.
        if (grab.isSelected)
            return;

        if (requireRecentRelease)
        {
            PerkamenNoGravity noGravity = grab.GetComponent<PerkamenNoGravity>();
            if (noGravity == null || !noGravity.HasBeenGrabbed || !noGravity.WasRecentlyReleased(releaseSnapWindow))
                return;
        }

        Transform perkamen = grab.transform;
        Rigidbody rb = perkamen.GetComponent<Rigidbody>();

        hasSnapped = true;
        snappedPerkamen = grab;

        if (parentToPanAfterSnap)
            perkamen.SetParent(panTransform, true);

        perkamen.position = GetSnapPosition();
        perkamen.rotation = GetFlatRotation(panTransform);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (disableGrabAfterSnap)
            grab.enabled = false;

        onPerkamenSnapped?.Invoke();
        Debug.Log($"[SyrupPerkamenSnap] {perkamen.name} snapped to {panTransform.name}.", this);
    }

    public void ClearSnapState()
    {
        hasSnapped = false;
        snappedPerkamen = null;
    }

    private bool IsPerkamen(GameObject candidate)
    {
        if (candidate == null)
            return false;

        if (HasPerkamenTag(candidate))
            return true;

        return candidate.name.IndexOf("perkamen", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool HasPerkamenTag(GameObject candidate)
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

    private void ResolvePanTransform()
    {
        if (panTransform != null)
            return;

        string targetName = name.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("Kanan", System.StringComparison.OrdinalIgnoreCase) >= 0
            ? "Balance_WeightRight"
            : "Balance_WeightLeft";

        panTransform = FindSceneTransformByName(targetName);
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

    private void EnsureTriggerCollider()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<BoxCollider>();

        if (triggerCollider == null)
        {
            Debug.LogWarning($"[SyrupPerkamenSnap] {name} belum punya BoxCollider. Tambahkan komponennya di scene supaya snap target bisa diedit dari Inspector.", this);
            return;
        }

        triggerCollider.isTrigger = true;
        triggerCollider.size = triggerSize;
        triggerCollider.center = Vector3.zero;
    }

    private void UpdatePose()
    {
        if (panTransform == null)
            return;

        transform.position = GetSnapPosition();
        transform.rotation = Quaternion.identity;

        if (triggerCollider != null)
            triggerCollider.size = triggerSize;
    }

    private Vector3 GetSnapPosition()
    {
        if (TryGetRendererBounds(out Bounds bounds))
            return new Vector3(bounds.center.x + worldOffset.x, bounds.min.y + worldOffset.y, bounds.center.z + worldOffset.z);

        return panTransform.position + worldOffset;
    }

    private bool TryGetRendererBounds(out Bounds bounds)
    {
        bounds = new Bounds();

        if (panTransform == null)
            return false;

        Renderer[] renderers = panTransform.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null)
                continue;

            if (IsInPerkamenHierarchy(targetRenderer.transform))
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

    private bool IsInPerkamenHierarchy(Transform target)
    {
        Transform current = target;

        while (current != null)
        {
            if (current.name.IndexOf("perkamen", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (current == panTransform)
                break;

            current = current.parent;
        }

        return false;
    }

    private Quaternion GetFlatRotation(Transform target)
    {
        Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }
}
