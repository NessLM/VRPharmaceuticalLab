using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(BoxCollider))]
public class LabBoundsManager : MonoBehaviour
{
    [Header("Target Tools")]
    [SerializeField] private string toolTag = "ToolObject";

    [Header("Boundary Settings")]
    [SerializeField] private float margin = 0.15f;
    [SerializeField] private float scanInterval = 0.75f;

    [Header("Held Object Behavior")]
    [SerializeField] private bool clampWhileHeld = true;
    [SerializeField] private bool clampUsingObjectSize = true;

    [Header("Return Settings")]
    [SerializeField] private bool returnToLastSafePosition = true;
    [SerializeField] private bool resetVelocity = true;

    private BoxCollider boundsCollider;

    private readonly List<Rigidbody> trackedTools = new List<Rigidbody>();
    private readonly Dictionary<Rigidbody, Pose> lastSafePose = new Dictionary<Rigidbody, Pose>();
    private readonly Dictionary<Rigidbody, float> toolRadius = new Dictionary<Rigidbody, float>();

    private float nextScanTime;

    private void Awake()
    {
        boundsCollider = GetComponent<BoxCollider>();
        boundsCollider.isTrigger = true;

        ScanTools();
    }

    private void Update()
    {
        if (Time.time >= nextScanTime)
        {
            ScanTools();
            nextScanTime = Time.time + scanInterval;
        }
    }

    private void LateUpdate()
    {
        for (int i = trackedTools.Count - 1; i >= 0; i--)
        {
            Rigidbody tool = trackedTools[i];

            if (tool == null)
            {
                trackedTools.RemoveAt(i);
                continue;
            }

            CheckTool(tool);
        }
    }

    private void ScanTools()
    {
        trackedTools.Clear();

        GameObject[] toolObjects = GameObject.FindGameObjectsWithTag(toolTag);

        foreach (GameObject obj in toolObjects)
        {
            Rigidbody rb = obj.GetComponent<Rigidbody>();

            if (rb == null)
                continue;

            trackedTools.Add(rb);

            if (!lastSafePose.ContainsKey(rb))
                lastSafePose.Add(rb, new Pose(rb.transform.position, rb.transform.rotation));

            toolRadius[rb] = CalculateToolRadius(rb);
        }
    }

    private void CheckTool(Rigidbody tool)
    {
        XRGrabInteractable grab = tool.GetComponent<XRGrabInteractable>();
        bool isHeld = grab != null && grab.isSelected;

        float radius = toolRadius.ContainsKey(tool) ? toolRadius[tool] : 0f;

        if (IsInsideBounds(tool.transform.position, radius))
        {
            lastSafePose[tool] = new Pose(tool.transform.position, tool.transform.rotation);
            return;
        }

        if (isHeld && clampWhileHeld)
        {
            ClampHeldTool(tool, radius);
        }
        else
        {
            ReturnTool(tool, radius);
        }
    }

    private void ClampHeldTool(Rigidbody tool, float radius)
    {
        Vector3 clampedPosition = ClampPositionToBounds(tool.transform.position, radius);

        if (resetVelocity)
        {
            tool.linearVelocity = Vector3.zero;
            tool.angularVelocity = Vector3.zero;
        }

        tool.useGravity = false;
        tool.isKinematic = true;

        tool.transform.position = clampedPosition;
    }

    private void ReturnTool(Rigidbody tool, float radius)
    {
        Vector3 targetPosition;
        Quaternion targetRotation;

        if (returnToLastSafePosition && lastSafePose.TryGetValue(tool, out Pose safePose))
        {
            targetPosition = safePose.position;
            targetRotation = safePose.rotation;
        }
        else
        {
            targetPosition = ClampPositionToBounds(tool.transform.position, radius);
            targetRotation = tool.transform.rotation;
        }

        if (resetVelocity)
        {
            tool.linearVelocity = Vector3.zero;
            tool.angularVelocity = Vector3.zero;
        }

        tool.useGravity = false;
        tool.isKinematic = true;

        tool.transform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    private bool IsInsideBounds(Vector3 worldPosition, float objectRadius)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPosition);

        Vector3 center = boundsCollider.center;
        Vector3 halfSize = boundsCollider.size * 0.5f;

        float safeMargin = margin;

        if (clampUsingObjectSize)
            safeMargin += objectRadius;

        bool insideX = localPoint.x >= center.x - halfSize.x + safeMargin &&
                       localPoint.x <= center.x + halfSize.x - safeMargin;

        bool insideY = localPoint.y >= center.y - halfSize.y + safeMargin &&
                       localPoint.y <= center.y + halfSize.y - safeMargin;

        bool insideZ = localPoint.z >= center.z - halfSize.z + safeMargin &&
                       localPoint.z <= center.z + halfSize.z - safeMargin;

        return insideX && insideY && insideZ;
    }

    private Vector3 ClampPositionToBounds(Vector3 worldPosition, float objectRadius)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPosition);

        Vector3 center = boundsCollider.center;
        Vector3 halfSize = boundsCollider.size * 0.5f;

        float safeMargin = margin;

        if (clampUsingObjectSize)
            safeMargin += objectRadius;

        localPoint.x = Mathf.Clamp(localPoint.x, center.x - halfSize.x + safeMargin, center.x + halfSize.x - safeMargin);
        localPoint.y = Mathf.Clamp(localPoint.y, center.y - halfSize.y + safeMargin, center.y + halfSize.y - safeMargin);
        localPoint.z = Mathf.Clamp(localPoint.z, center.z - halfSize.z + safeMargin, center.z + halfSize.z - safeMargin);

        return transform.TransformPoint(localPoint);
    }

    private float CalculateToolRadius(Rigidbody rb)
    {
        Collider[] colliders = rb.GetComponentsInChildren<Collider>();

        if (colliders == null || colliders.Length == 0)
            return 0.1f;

        Bounds combinedBounds = colliders[0].bounds;

        for (int i = 1; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
                continue;

            combinedBounds.Encapsulate(colliders[i].bounds);
        }

        Vector3 extents = combinedBounds.extents;

        float radius = Mathf.Max(extents.x, extents.y, extents.z);

        return Mathf.Clamp(radius, 0.05f, 0.8f);
    }
}