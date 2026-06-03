using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
public class LiquidSnapStation : MonoBehaviour
{
    [Header("Snap Target")]
    [SerializeField] private Transform snapAnchor;
    [SerializeField] private Vector3 snapPositionOffset = new Vector3(0f, -0.03f, 0f);
    [SerializeField] private Vector3 snapEulerOffset = Vector3.zero;
    [SerializeField] private bool preserveContainerRotation = false;
    [SerializeField] private bool alignContainerFillAxisToSnapUp = true;
    [SerializeField] private bool onlyAcceptEmptyContainers = true;
    [SerializeField] private bool snapOnRelease = true;

    [Header("Marker")]
    [SerializeField] private GameObject markerObject;
    [SerializeField] private Renderer markerRenderer;
    [SerializeField] private bool showMarkerWhenReady = false;
    [SerializeField] private bool showMarkerOnlyWhenEmptyContainerNearby = false;

    [Header("Motion")]
    [SerializeField] private float snapSpeed = 14f;
    [SerializeField] private float rotationSpeed = 16f;
    [SerializeField] private bool makeKinematicWhileSnapped = true;

    [Header("Water")]
    [SerializeField] private WaterSource waterSource;
    [SerializeField] private bool turnWaterOnAfterSnap = false;
    [SerializeField] private bool fillSnappedContainerDirectly = false;
    [SerializeField] private bool allowDirectFillWithoutFillZone = false;
    [SerializeField] private bool turnWaterOffWhenFull = false;
    [SerializeField] private bool turnWaterOffWhenRemoved = false;

    private readonly HashSet<LiquidContainer> candidates = new HashSet<LiquidContainer>();
    private LiquidContainer snappedContainer;
    private Coroutine snapRoutine;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        snapAnchor = transform;
    }

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        if (snapAnchor == null)
            snapAnchor = transform;

        if (markerRenderer == null && markerObject != null)
            markerRenderer = markerObject.GetComponent<Renderer>();

        UpdateMarker();
    }

    private void OnValidate()
    {
        snapSpeed = Mathf.Max(0.01f, snapSpeed);
        rotationSpeed = Mathf.Max(0.01f, rotationSpeed);

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Update()
    {
        CleanupCandidates();

        if (snappedContainer != null)
        {
            XRGrabInteractable snappedGrab = FindGrabInteractable(snappedContainer);
            if (snappedGrab != null && snappedGrab.isSelected)
            {
                ReleaseSnappedContainer();
            }
            else if (turnWaterOffWhenFull && snappedContainer.IsFull && waterSource != null)
            {
                waterSource.TurnOff();
            }
            else
            {
                FillSnappedContainer();
            }

            UpdateMarker();
            return;
        }

        LiquidContainer candidate = FindBestCandidate();
        if (candidate != null && ShouldSnap(candidate))
            SnapContainer(candidate);

        UpdateMarker();
    }

    private void OnTriggerEnter(Collider other)
    {
        LiquidContainer container = FindContainer(other);
        if (container != null)
            candidates.Add(container);
    }

    private void OnTriggerStay(Collider other)
    {
        LiquidContainer container = FindContainer(other);
        if (container != null)
            candidates.Add(container);
    }

    private void OnTriggerExit(Collider other)
    {
        LiquidContainer container = FindContainer(other);
        if (container != null)
            candidates.Remove(container);
    }

    public void SnapContainer(LiquidContainer container)
    {
        if (container == null || !CanAccept(container))
            return;

        if (snapRoutine != null)
            StopCoroutine(snapRoutine);

        snappedContainer = container;
        snapRoutine = StartCoroutine(SnapRoutine(container));
    }

    public void ClearSnappedContainer()
    {
        ReleaseSnappedContainer();
    }

    private IEnumerator SnapRoutine(LiquidContainer container)
    {
        Transform target = snapAnchor != null ? snapAnchor : transform;
        Quaternion preservedRotation = container.transform.rotation;
        Rigidbody rb = FindRigidbody(container);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;

            if (makeKinematicWhileSnapped)
                rb.isKinematic = true;
        }

        while (container != null)
        {
            Vector3 targetPosition = GetSnapPosition(target);
            Quaternion targetRotation = GetSnapRotation(target, container, preservedRotation);

            XRGrabInteractable grab = FindGrabInteractable(container);
            if (grab != null && grab.isSelected)
            {
                ReleaseSnappedContainer();
                yield break;
            }

            container.transform.position = Vector3.Lerp(
                container.transform.position,
                targetPosition,
                Time.deltaTime * snapSpeed
            );

            container.transform.rotation = Quaternion.Slerp(
                container.transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );

            float posDistance = Vector3.Distance(container.transform.position, targetPosition);
            float rotDistance = preserveContainerRotation ? 0f : Quaternion.Angle(container.transform.rotation, targetRotation);

            if (posDistance < 0.003f && rotDistance < 0.5f)
                break;

            yield return null;
        }

        if (container != null)
            container.transform.SetPositionAndRotation(GetSnapPosition(target), GetSnapRotation(target, container, preservedRotation));

        if (turnWaterOnAfterSnap && waterSource != null)
            waterSource.TurnOn();

        snapRoutine = null;
        UpdateMarker();
    }

    private void FillSnappedContainer()
    {
        if (!fillSnappedContainerDirectly)
            return;

        if (snappedContainer == null)
            return;

        if (!allowDirectFillWithoutFillZone || HasFillZone(snappedContainer))
            return;

        if (waterSource == null || !waterSource.IsFlowing)
            return;

        float amount = waterSource.FlowRateMlPerSecond * Time.deltaTime;
        snappedContainer.AddLiquid(amount, waterSource.LiquidData);
    }

    private bool HasFillZone(LiquidContainer container)
    {
        return container != null && container.GetComponentInChildren<WasherFillZone>() != null;
    }

    private void ReleaseSnappedContainer()
    {
        if (turnWaterOffWhenRemoved && waterSource != null)
            waterSource.TurnOff();

        snappedContainer = null;

        if (snapRoutine != null)
        {
            StopCoroutine(snapRoutine);
            snapRoutine = null;
        }

        UpdateMarker();
    }

    private bool ShouldSnap(LiquidContainer container)
    {
        if (!CanAccept(container))
            return false;

        if (!snapOnRelease)
            return true;

        XRGrabInteractable grab = FindGrabInteractable(container);
        return grab == null || !grab.isSelected;
    }

    private bool CanAccept(LiquidContainer container)
    {
        if (container == null)
            return false;

        if (onlyAcceptEmptyContainers && !container.IsEmpty)
            return false;

        return true;
    }

    private LiquidContainer FindBestCandidate()
    {
        LiquidContainer best = null;
        float bestDistance = float.MaxValue;
        Vector3 targetPosition = snapAnchor != null ? snapAnchor.position : transform.position;

        foreach (LiquidContainer candidate in candidates)
        {
            if (!CanAccept(candidate))
                continue;

            float distance = Vector3.Distance(candidate.transform.position, targetPosition);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void CleanupCandidates()
    {
        candidates.RemoveWhere(container => container == null);
    }

    private void UpdateMarker()
    {
        bool visible = showMarkerWhenReady && snappedContainer == null;

        if (visible && showMarkerOnlyWhenEmptyContainerNearby)
            visible = FindBestCandidate() != null;

        if (markerObject != null && markerObject != gameObject)
            markerObject.SetActive(visible);

        if (markerRenderer != null)
            markerRenderer.enabled = visible;
    }

    private LiquidContainer FindContainer(Collider other)
    {
        LiquidContainer container = other.GetComponentInParent<LiquidContainer>();
        if (container == null)
            container = other.GetComponentInChildren<LiquidContainer>();

        return container;
    }

    private Rigidbody FindRigidbody(LiquidContainer container)
    {
        Rigidbody rb = container.GetComponent<Rigidbody>();
        if (rb == null)
            rb = container.GetComponentInParent<Rigidbody>();

        return rb;
    }

    private XRGrabInteractable FindGrabInteractable(LiquidContainer container)
    {
        XRGrabInteractable grab = container.GetComponent<XRGrabInteractable>();
        if (grab == null)
            grab = container.GetComponentInParent<XRGrabInteractable>();
        if (grab == null)
            grab = container.GetComponentInChildren<XRGrabInteractable>();

        return grab;
    }

    private Vector3 GetSnapPosition(Transform target)
    {
        return target.position + snapPositionOffset;
    }

    private Quaternion GetSnapRotation(Transform target, LiquidContainer container, Quaternion preservedRotation)
    {
        if (preserveContainerRotation)
            return preservedRotation;

        Quaternion targetRotation = target.rotation * Quaternion.Euler(snapEulerOffset);

        if (!alignContainerFillAxisToSnapUp || container == null)
            return targetRotation;

        Vector3 fillAxis = container.FillAxisLocal;
        Vector3 snappedFillAxis = targetRotation * fillAxis;
        if (snappedFillAxis.sqrMagnitude < 0.0001f)
            return targetRotation;

        return Quaternion.FromToRotation(snappedFillAxis, target.up) * targetRotation;
    }
}
