using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Dipasang langsung di Collider_Piring_Kanan / Collider_Piring_Kiri.
/// Tidak perlu RightPayloadAnchorZone.
/// 
/// Alur:
/// 1. Anak timbangan dilepas.
/// 2. Dia jatuh normal pakai gravity.
/// 3. Setelah berada di area piring dan sudah agak diam, dia dikunci ke Plate_Target.
/// 4. Saat Plate_Target naik-turun, anak timbangan ikut.
/// 5. Saat digrab lagi, dia lepas dari Plate_Target.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BalancePanPayloadAnchor : MonoBehaviour
{
    [Header("Anchor")]
    [Tooltip("Isi dengan Plate_Right_Target atau Plate_Left_Target.")]
    [SerializeField] private Transform payloadAnchor;

    [Header("Attach Rules")]
    [SerializeField] private bool attachReleasedWeights = true;
    [SerializeField] private bool requirePickedUpBeforeAttach = false;
    [SerializeField] private bool preserveWorldPoseOnAttach = true;

    [Header("Fall Before Attach")]
    [Tooltip("Berapa lama anak timbangan dibiarkan jatuh dulu sebelum dikunci.")]
    [SerializeField] private float fallDelay = 0.20f;

    [Tooltip("Tunggu sampai velocity kecil supaya tidak dikunci saat masih mental.")]
    [SerializeField] private bool waitUntilNearlyStill = true;

    [SerializeField] private float maxVelocityToAttach = 0.35f;
    [SerializeField] private float maxWaitToAttach = 0.80f;

    [Header("Attach Area Check")]
    [Tooltip("ON supaya anak timbangan tidak dikunci saat masih terlalu tinggi/terlalu jauh.")]
    [SerializeField] private bool requireNearAnchor = true;

    [Tooltip("Area cek dalam local space Plate_Target. Besarkan kalau susah nempel.")]
    [SerializeField] private Vector3 localAttachBoxCenter = new Vector3(0f, 0.08f, 0f);

    [SerializeField] private Vector3 localAttachBoxSize = new Vector3(0.40f, 0.35f, 0.40f);

    [Header("Attached Physics")]
    [SerializeField] private bool forceLockEveryFrame = true;
    [SerializeField] private bool makeKinematicWhileAttached = true;
    [SerializeField] private bool disableGravityWhileAttached = true;
    [SerializeField] private float attachedLinearDamping = 8f;
    [SerializeField] private float attachedAngularDamping = 8f;

    [Header("Detach Physics")]
    [Tooltip("Saat digrab lagi, gravity dimatikan agar XR Grab tidak berat/aneh.")]
    [SerializeField] private bool disableGravityWhileGrabbedAgain = true;

    [Header("Debug")]
    [SerializeField] private int debugAttachedCount;
    [SerializeField] private bool debugLogs;

    private readonly Dictionary<XRGrabInteractable, Transform> originalParents = new Dictionary<XRGrabInteractable, Transform>();
    private readonly HashSet<XRGrabInteractable> attached = new HashSet<XRGrabInteractable>();
    private readonly HashSet<XRGrabInteractable> inside = new HashSet<XRGrabInteractable>();
    private readonly Dictionary<XRGrabInteractable, Coroutine> pendingAttach = new Dictionary<XRGrabInteractable, Coroutine>();

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        if (payloadAnchor == null)
            payloadAnchor = transform;
    }

    private void OnDisable()
    {
        foreach (XRGrabInteractable grab in attached)
        {
            if (grab != null)
                grab.selectEntered.RemoveListener(OnAttachedObjectGrabbed);
        }

        foreach (var pair in pendingAttach)
        {
            if (pair.Value != null)
                StopCoroutine(pair.Value);
        }

        attached.Clear();
        inside.Clear();
        pendingAttach.Clear();
        originalParents.Clear();
        debugAttachedCount = 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterPayload(other);
    }

    private void OnTriggerStay(Collider other)
    {
        RegisterPayload(other);
    }

    private void OnTriggerExit(Collider other)
    {
        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();
        if (grab == null)
            return;

        inside.Remove(grab);

        if (pendingAttach.TryGetValue(grab, out Coroutine routine))
        {
            if (routine != null)
                StopCoroutine(routine);

            pendingAttach.Remove(grab);
        }
    }

    private void LateUpdate()
    {
        if (!forceLockEveryFrame)
            return;

        foreach (XRGrabInteractable grab in attached)
        {
            if (grab == null || grab.isSelected)
                continue;

            if (payloadAnchor != null && grab.transform.parent != payloadAnchor)
                grab.transform.SetParent(payloadAnchor, true);

            Rigidbody rb = grab.GetComponent<Rigidbody>();
            ApplyAttachedPhysics(rb);
        }

        debugAttachedCount = attached.Count;
    }

    private void RegisterPayload(Collider other)
    {
        if (!attachReleasedWeights || other == null || payloadAnchor == null)
            return;

        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();
        if (grab == null)
            return;

        if (!IsValidWeight(grab))
            return;

        inside.Add(grab);

        if (grab.isSelected)
            return;

        if (attached.Contains(grab))
            return;

        if (pendingAttach.ContainsKey(grab))
            return;

        if (requirePickedUpBeforeAttach && !HasBeenPickedUp(grab))
            return;

        Coroutine routine = StartCoroutine(AttachAfterFallRoutine(grab));
        pendingAttach.Add(grab, routine);
    }

    private IEnumerator AttachAfterFallRoutine(XRGrabInteractable grab)
    {
        Rigidbody rb = grab != null ? grab.GetComponent<Rigidbody>() : null;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        yield return new WaitForSeconds(Mathf.Max(0f, fallDelay));

        float timer = 0f;

        while (timer < maxWaitToAttach)
        {
            if (grab == null || grab.isSelected || !inside.Contains(grab))
            {
                pendingAttach.Remove(grab);
                yield break;
            }

            rb = grab.GetComponent<Rigidbody>();

            bool velocityOk = true;
            if (waitUntilNearlyStill && rb != null)
                velocityOk = rb.linearVelocity.magnitude <= maxVelocityToAttach;

            bool positionOk = !requireNearAnchor || IsNearAnchor(grab.transform);

            if (velocityOk && positionOk)
                break;

            timer += Time.deltaTime;
            yield return null;
        }

        pendingAttach.Remove(grab);

        if (grab == null || grab.isSelected || attached.Contains(grab))
            yield break;

        if (!inside.Contains(grab))
            yield break;

        if (requireNearAnchor && !IsNearAnchor(grab.transform))
            yield break;

        Attach(grab);
    }

    private bool IsValidWeight(XRGrabInteractable grab)
    {
        if (grab == null)
            return false;

        WeightItem weightItem = grab.GetComponent<WeightItem>();
        BalanceMassSource massSource = grab.GetComponent<BalanceMassSource>();

        if (weightItem == null && massSource == null)
            return false;

        if (weightItem != null && weightItem.IsParchment)
            return false;

        return true;
    }

    private bool HasBeenPickedUp(XRGrabInteractable grab)
    {
        WeightItem item = grab.GetComponent<WeightItem>();
        if (item != null)
            return item.HasBeenPickedUp;

        return true;
    }

    private bool IsNearAnchor(Transform payload)
    {
        if (payload == null || payloadAnchor == null)
            return false;

        Vector3 local = payloadAnchor.InverseTransformPoint(payload.position);
        Vector3 diff = local - localAttachBoxCenter;

        return Mathf.Abs(diff.x) <= localAttachBoxSize.x * 0.5f &&
               Mathf.Abs(diff.y) <= localAttachBoxSize.y * 0.5f &&
               Mathf.Abs(diff.z) <= localAttachBoxSize.z * 0.5f;
    }

    private void Attach(XRGrabInteractable grab)
    {
        if (grab == null || payloadAnchor == null)
            return;

        Transform itemTransform = grab.transform;

        if (!originalParents.ContainsKey(grab))
            originalParents.Add(grab, itemTransform.parent);

        itemTransform.SetParent(payloadAnchor, preserveWorldPoseOnAttach);

        Rigidbody rb = grab.GetComponent<Rigidbody>();
        ApplyAttachedPhysics(rb);

        WeightItem item = grab.GetComponent<WeightItem>();
        if (item != null)
            item.Settle();

        attached.Add(grab);
        debugAttachedCount = attached.Count;

        grab.selectEntered.RemoveListener(OnAttachedObjectGrabbed);
        grab.selectEntered.AddListener(OnAttachedObjectGrabbed);

        if (debugLogs)
            Debug.Log($"[BalancePanPayloadAnchor] Attached {grab.name} to {payloadAnchor.name}", this);
    }

    private void ApplyAttachedPhysics(Rigidbody rb)
    {
        if (rb == null)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (disableGravityWhileAttached)
            rb.useGravity = false;

        if (makeKinematicWhileAttached)
            rb.isKinematic = true;

        rb.linearDamping = attachedLinearDamping;
        rb.angularDamping = attachedAngularDamping;
    }

    private void OnAttachedObjectGrabbed(SelectEnterEventArgs args)
    {
        if (args == null)
            return;

        XRGrabInteractable grab = args.interactableObject as XRGrabInteractable;
        if (grab == null)
            return;

        DetachForGrab(grab);
    }

    private void DetachForGrab(XRGrabInteractable grab)
    {
        if (grab == null)
            return;

        grab.selectEntered.RemoveListener(OnAttachedObjectGrabbed);

        attached.Remove(grab);
        inside.Remove(grab);
        debugAttachedCount = attached.Count;

        if (pendingAttach.TryGetValue(grab, out Coroutine routine))
        {
            if (routine != null)
                StopCoroutine(routine);

            pendingAttach.Remove(grab);
        }

        if (originalParents.TryGetValue(grab, out Transform oldParent))
        {
            grab.transform.SetParent(oldParent, true);
            originalParents.Remove(grab);
        }
        else
        {
            grab.transform.SetParent(null, true);
        }

        Rigidbody rb = grab.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.useGravity = !disableGravityWhileGrabbedAgain;
        }

        if (debugLogs)
            Debug.Log($"[BalancePanPayloadAnchor] Detached {grab.name}", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (payloadAnchor == null || !requireNearAnchor)
            return;

        Gizmos.color = Color.cyan;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = payloadAnchor.localToWorldMatrix;
        Gizmos.DrawWireCube(localAttachBoxCenter, localAttachBoxSize);
        Gizmos.matrix = oldMatrix;
    }
}