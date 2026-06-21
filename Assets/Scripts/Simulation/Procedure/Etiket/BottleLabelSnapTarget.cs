using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public sealed class BottleLabelSnapTarget : MonoBehaviour
{
    [Header("Bottle")]
    [SerializeField] private Transform bottleTransform;
    [SerializeField] private BottleLid bottleLid;

    [Header("Label Snap")]
    [SerializeField] private Transform labelAnchor;
    [SerializeField] private Collider snapZone;
    [SerializeField] private float fallbackSnapDistance = 0.14f;

    [Header("Attached Pose")]
    [Tooltip("Final position relative to BottleLabelAnchor.")]
    [SerializeField] private Vector3 attachedLocalPosition = Vector3.zero;
    [Tooltip("Final rotation relative to BottleLabelAnchor. Tune this instead of rotating the spawned label.")]
    [SerializeField] private Vector3 attachedLocalEuler = new Vector3(0f, -90f, 0f);
    [Tooltip("Positive local scale that compensates for the bottle model scale.")]
    [SerializeField] private Vector3 attachedLocalScale =
        new Vector3(0.1152386f, 0.1152386f, 0.1152386f);

    [Header("Events")]
    [SerializeField] private UnityEvent onLabelSnapped;
    [SerializeField] private UnityEvent onBottleNotClosed;

    public Transform LabelAnchor => labelAnchor;
    public Transform BottleTransform => bottleTransform;
    public BottleLid BottleLid => bottleLid;
    public bool IsBottleClosed => bottleLid == null || !bottleLid.IsOpen;

    private void Awake()
    {
        ResolveReferences();
    }

    public bool IsLabelInsideSnapArea(Transform label)
    {
        if (label == null)
            return false;

        ResolveReferences();

        if (snapZone != null)
            return snapZone.bounds.SqrDistance(label.position) <= 0.000001f;

        Transform target = labelAnchor != null ? labelAnchor : transform;
        return Vector3.Distance(label.position, target.position) <= fallbackSnapDistance;
    }

    public bool TrySnapLabel(
        Transform label,
        XRGrabInteractable labelGrab,
        Rigidbody labelRigidbody,
        Collider[] labelColliders)
    {
        if (label == null || !IsLabelInsideSnapArea(label))
            return false;

        if (!IsBottleClosed)
        {
            onBottleNotClosed?.Invoke();
            return false;
        }

        Transform target = labelAnchor != null ? labelAnchor : transform;

        if (labelRigidbody != null)
        {
            labelRigidbody.linearVelocity = Vector3.zero;
            labelRigidbody.angularVelocity = Vector3.zero;
            labelRigidbody.isKinematic = true;
            labelRigidbody.useGravity = false;
            labelRigidbody.detectCollisions = false;
        }

        label.SetParent(target, false);
        label.localPosition = attachedLocalPosition;
        label.localRotation = Quaternion.Euler(attachedLocalEuler);
        label.localScale = SanitizeScale(attachedLocalScale);

        Transform cardCanvas = FindDeepChild(label, "EtiketCardCanvas");
        if (cardCanvas != null)
        {
            cardCanvas.localPosition = Vector3.zero;
            cardCanvas.localRotation = Quaternion.identity;
            cardCanvas.localScale = AbsScale(cardCanvas.localScale);
        }

        if (labelGrab != null)
            labelGrab.enabled = false;

        if (labelColliders != null)
        {
            foreach (Collider labelCollider in labelColliders)
            {
                if (labelCollider != null)
                    labelCollider.enabled = false;
            }
        }

        onLabelSnapped?.Invoke();
        return true;
    }

    public void NotifyBottleNotClosed()
    {
        onBottleNotClosed?.Invoke();
    }

    private void ResolveReferences()
    {
        if (bottleTransform == null)
            bottleTransform = transform.parent != null ? transform.parent : transform;

        if (labelAnchor == null)
            labelAnchor = FindDeepChild(bottleTransform, "BottleLabelAnchor");

        if (snapZone == null)
            snapZone = GetComponent<Collider>();

        if (bottleLid == null && bottleTransform != null)
            bottleLid = bottleTransform.GetComponentInChildren<BottleLid>(true);
    }

    private static Transform FindDeepChild(Transform root, string targetName)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == targetName)
                return child;
        }

        return null;
    }

    private static Vector3 SanitizeScale(Vector3 scale)
    {
        Vector3 positive = AbsScale(scale);
        positive.x = Mathf.Max(0.0001f, positive.x);
        positive.y = Mathf.Max(0.0001f, positive.y);
        positive.z = Mathf.Max(0.0001f, positive.z);
        return positive;
    }

    private static Vector3 AbsScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fallbackSnapDistance = Mathf.Max(0.03f, fallbackSnapDistance);
        attachedLocalScale = SanitizeScale(attachedLocalScale);
        ResolveReferences();
    }
#endif
}
