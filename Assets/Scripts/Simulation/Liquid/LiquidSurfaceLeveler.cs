using UnityEngine;

public class LiquidSurfaceLeveler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LiquidContainer container;
    [SerializeField] private Transform liquidVisual;

    [Header("Surface Rotation")]
    [Tooltip("1 = permukaan air sangat horizontal terhadap dunia. 0 = ikut rotasi gelas. Untuk visual VR, 0.75-0.9 biasanya enak.")]
    [Range(0f, 1f)]
    [SerializeField] private float worldLevelStrength = 0.82f;

    [SerializeField] private float rotationFollowSpeed = 10f;

    [Header("Rotation Slosh")]
    [SerializeField] private bool enableRotationSlosh = true;
    [SerializeField] private float angularSloshStrength = 5f;
    [SerializeField] private float maxSurfaceTilt = 12f;
    [SerializeField] private float sloshSmoothSpeed = 9f;
    [SerializeField] private float sloshReturnSpeed = 5f;

    [Header("Movement Slosh")]
    [SerializeField] private bool enableMovementSlosh = true;
    [SerializeField] private float movementSloshAmount = 0.01f;
    [SerializeField] private float maxPositionOffset = 0.018f;
    [SerializeField] private float positionSloshSmoothSpeed = 10f;
    [SerializeField] private float positionReturnSpeed = 6f;

    private Vector3 previousContainerPosition;
    private Quaternion previousContainerRotation;

    private Vector2 currentSloshAngles;
    private Vector2 targetSloshAngles;

    private Vector3 currentPositionOffset;
    private Vector3 targetPositionOffset;

    /// <summary>Stable base local position of the liquid visual, stored at Awake before any slosh is applied.</summary>
    private Vector3 _baseLocalPosition;

    private void Reset()
    {
        container = GetComponentInParent<LiquidContainer>();
        liquidVisual = transform;
    }

    private void Awake()
    {
        if (container == null)
            container = GetComponentInParent<LiquidContainer>();

        if (liquidVisual == null)
            liquidVisual = transform;

        if (container != null)
        {
            previousContainerPosition = container.transform.position;
            previousContainerRotation = container.transform.rotation;
        }

        // Capture base local position before any slosh offset is applied.
        if (liquidVisual != null)
            _baseLocalPosition = liquidVisual.localPosition;
    }

    private void LateUpdate()
    {
        if (container == null || liquidVisual == null)
            return;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);

        UpdateRotationSlosh(deltaTime);
        UpdateMovementSlosh(deltaTime);

        ApplySurfaceRotation(deltaTime);
        ApplyPositionSlosh(deltaTime);

        previousContainerPosition = container.transform.position;
        previousContainerRotation = container.transform.rotation;
    }

    private void UpdateRotationSlosh(float deltaTime)
    {
        if (!enableRotationSlosh)
        {
            targetSloshAngles = Vector2.zero;
            return;
        }

        Quaternion deltaRotation = container.transform.rotation * Quaternion.Inverse(previousContainerRotation);
        deltaRotation.ToAngleAxis(out float angleDegrees, out Vector3 axis);

        if (angleDegrees > 180f)
            angleDegrees -= 360f;

        if (float.IsNaN(axis.x) || axis.sqrMagnitude < 0.0001f)
        {
            targetSloshAngles = Vector2.zero;
            return;
        }

        Vector3 angularVelocityWorld = axis.normalized * (angleDegrees * Mathf.Deg2Rad / deltaTime);
        Vector3 angularVelocityLocal = container.transform.InverseTransformDirection(angularVelocityWorld);

        targetSloshAngles = new Vector2(
            Mathf.Clamp(angularVelocityLocal.z * angularSloshStrength, -maxSurfaceTilt, maxSurfaceTilt),
            Mathf.Clamp(-angularVelocityLocal.x * angularSloshStrength, -maxSurfaceTilt, maxSurfaceTilt)
        );

        targetSloshAngles = Vector2.Lerp(
            targetSloshAngles,
            Vector2.zero,
            Time.deltaTime * sloshReturnSpeed
        );

        currentSloshAngles = Vector2.Lerp(
            currentSloshAngles,
            targetSloshAngles,
            Time.deltaTime * sloshSmoothSpeed
        );
    }

    private void UpdateMovementSlosh(float deltaTime)
    {
        if (!enableMovementSlosh)
        {
            targetPositionOffset = Vector3.zero;
            return;
        }

        Vector3 velocityWorld = (container.transform.position - previousContainerPosition) / deltaTime;
        Vector3 velocityLocal = container.transform.InverseTransformDirection(velocityWorld);

        targetPositionOffset = new Vector3(
            -velocityLocal.x,
            0f,
            -velocityLocal.z
        ) * movementSloshAmount;

        targetPositionOffset = Vector3.ClampMagnitude(targetPositionOffset, maxPositionOffset);

        targetPositionOffset = Vector3.Lerp(
            targetPositionOffset,
            Vector3.zero,
            Time.deltaTime * positionReturnSpeed
        );

        currentPositionOffset = Vector3.Lerp(
            currentPositionOffset,
            targetPositionOffset,
            Time.deltaTime * positionSloshSmoothSpeed
        );
    }

    private void ApplySurfaceRotation(float deltaTime)
    {
        Vector3 projectedForward = Vector3.ProjectOnPlane(container.transform.forward, Vector3.up);

        if (projectedForward.sqrMagnitude < 0.001f)
            projectedForward = Vector3.ProjectOnPlane(container.transform.right, Vector3.up);

        if (projectedForward.sqrMagnitude < 0.001f)
            projectedForward = Vector3.forward;

        Quaternion levelRotation = Quaternion.LookRotation(projectedForward.normalized, Vector3.up);
        Quaternion followContainerRotation = container.transform.rotation;

        Quaternion baseTargetRotation = Quaternion.Slerp(
            followContainerRotation,
            levelRotation,
            worldLevelStrength
        );

        Quaternion sloshRotation =
            Quaternion.AngleAxis(currentSloshAngles.x, baseTargetRotation * Vector3.right) *
            Quaternion.AngleAxis(currentSloshAngles.y, baseTargetRotation * Vector3.forward);

        Quaternion targetRotation = sloshRotation * baseTargetRotation;

        liquidVisual.rotation = Quaternion.Slerp(
            liquidVisual.rotation,
            targetRotation,
            deltaTime * rotationFollowSpeed
        );
    }

    private void ApplyPositionSlosh(float deltaTime)
    {
        // Apply the accumulated slosh offset relative to the stored base local position.
        // Without this, the liquid surface would never react to lateral movement of the container.
        liquidVisual.localPosition = _baseLocalPosition + currentPositionOffset;
    }

    /// <summary>
    /// Call this whenever the LiquidContainer changes the liquid visual's local position (e.g., on fill-level change)
    /// so the slosh base position stays in sync with the fill level.
    /// </summary>
    public void SyncBasePosition()
    {
        if (liquidVisual != null)
            _baseLocalPosition = liquidVisual.localPosition - currentPositionOffset;
    }
}