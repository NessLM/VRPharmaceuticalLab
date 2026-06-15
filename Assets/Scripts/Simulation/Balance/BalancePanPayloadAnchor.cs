using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attaches released WeightItem objects to a moving pan anchor so payloads follow pan motion.
/// Objects detach immediately when grabbed again.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BalancePanPayloadAnchor : MonoBehaviour
{
    [SerializeField] private Transform payloadAnchor;
    [SerializeField] private bool requirePickedUpBeforeAttach = true;
    [SerializeField] private bool preserveWorldPoseOnAttach = true;
    [SerializeField] private bool makeKinematicWhileAttached = true;

    private readonly Dictionary<XRGrabInteractable, Transform> originalParents = new Dictionary<XRGrabInteractable, Transform>();
    private readonly HashSet<XRGrabInteractable> attached = new HashSet<XRGrabInteractable>();

    private void Awake()
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;

        if (payloadAnchor == null)
            payloadAnchor = transform;
    }

    private void OnDisable()
    {
        foreach (XRGrabInteractable grab in attached)
        {
            if (grab != null)
                grab.selectEntered.RemoveListener(OnAttachedObjectSelected);
        }

        attached.Clear();
        originalParents.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryAttach(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryAttach(other);
    }

    private void TryAttach(Collider other)
    {
        if (other == null || payloadAnchor == null)
            return;

        WeightItem item = other.GetComponentInParent<WeightItem>();
        if (item == null || item.IsParchment)
            return;

        SmallWeightStorageState storageState = item.GetComponent<SmallWeightStorageState>();
        if (requirePickedUpBeforeAttach && storageState != null && !storageState.HasBeenPickedUp)
            return;

        XRGrabInteractable grab = item.GetComponent<XRGrabInteractable>();
        if (grab == null || grab.isSelected || attached.Contains(grab))
            return;

        Attach(grab);
    }

    private void Attach(XRGrabInteractable grab)
    {
        if (grab == null || payloadAnchor == null)
            return;

        Transform itemTransform = grab.transform;
        if (!originalParents.ContainsKey(grab))
            originalParents.Add(grab, itemTransform.parent);

        Rigidbody rb = grab.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            if (makeKinematicWhileAttached)
                rb.isKinematic = true;
        }

        itemTransform.SetParent(payloadAnchor, preserveWorldPoseOnAttach);
        attached.Add(grab);

        grab.selectEntered.RemoveListener(OnAttachedObjectSelected);
        grab.selectEntered.AddListener(OnAttachedObjectSelected);
    }

    private void OnAttachedObjectSelected(SelectEnterEventArgs args)
    {
        if (args == null)
            return;

        XRGrabInteractable grab = args.interactableObject as XRGrabInteractable;
        if (grab != null)
            DetachForGrab(grab);
    }

    private void DetachForGrab(XRGrabInteractable grab)
    {
        if (grab == null)
            return;

        grab.selectEntered.RemoveListener(OnAttachedObjectSelected);
        attached.Remove(grab);

        Transform oldParent;
        if (originalParents.TryGetValue(grab, out oldParent))
        {
            grab.transform.SetParent(oldParent, true);
            originalParents.Remove(grab);
        }

        Rigidbody rb = grab.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.useGravity = false;
        }
    }
}
