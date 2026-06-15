using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controls the visual behavior of the MG analytical balance scale.
/// Reads logical gram values from left/right WeightingZone components and smoothly
/// animates the beam tilt and pan vertical displacement proportionally.
///
/// Convention:
///   DifferenceGrams = RightMassGrams - LeftMassGrams
///   Positive difference means right is heavier, so the beam tilts right-down and right pan descends.
///   Adjust maxBeamAngleDegrees to a negative value if the visual is mirrored in-world.
///
/// Attach to: timbanganNeraca root GameObject.
/// Wire beamVisual, leftPanVisual, rightPanVisual, leftZone, rightZone in the Inspector.
/// </summary>
public class MG_BalanceController : MonoBehaviour
{
    [Header("Physical Zones")]
    [Tooltip("Trigger zone positioned above the LEFT pan.")]
    [SerializeField] private WeightingZone leftZone;
    [Tooltip("Trigger zone positioned above the RIGHT pan.")]
    [SerializeField] private WeightingZone rightZone;

    [Header("Powder Tracking")]
    [Tooltip("PowderDepositZone on Collider_Piring_Kiri that tracks deposited powder grams.")]
    [SerializeField] private PowderDepositZone powderDepositZone;

    [Header("Visual References")]
    [Tooltip("The beam mesh Transform that tilts around its local Z axis.")]
    [SerializeField] private Transform beamVisual;
    [Tooltip("Left pan mesh Transform. Moves up when right side is heavier.")]
    [SerializeField] private Transform leftPanVisual;
    [Tooltip("Right pan mesh Transform. Moves down when right side is heavier.")]
    [SerializeField] private Transform rightPanVisual;
    [Tooltip("Extra LEFT-side transforms that should follow the left pan vertical motion, such as zone, snap target, and payload anchor.")]
    [SerializeField] private Transform[] leftPanLinkedTransforms;
    [Tooltip("Extra RIGHT-side transforms that should follow the right pan vertical motion, such as zone, snap target, and payload anchor.")]
    [SerializeField] private Transform[] rightPanLinkedTransforms;

    [Header("Calibration")]
    [Tooltip("Beam tilt angle (degrees) at maximum imbalance. Negate if beam tilts the wrong way.")]
    [SerializeField] private float maxBeamAngleDegrees = 12f;
    [Tooltip("Gram difference that corresponds to maximum beam angle.")]
    [SerializeField] private float maxImbalanceGrams = 1f;
    [Tooltip("Vertical pan displacement (meters) at maximum imbalance.")]
    [SerializeField] private float maxPanOffsetMeters = 0.015f;
    [Tooltip("Grams difference considered balanced. Tune to the scale's required precision.")]
    [SerializeField] private float balanceToleranceGrams = 0.005f;
    [Tooltip("Animation lerp speed for beam and pan movement.")]
    [SerializeField] private float smoothSpeed = 4f;
    [Tooltip("Zero calibration offset in grams applied before computing the tilt angle.")]
    [SerializeField] private float zeroOffsetGrams = 0f;

    [Header("Events")]
    public UnityEvent onBalanced;
    public UnityEvent onUnbalanced;
    public UnityEvent<float> onLeftMassChanged;
    public UnityEvent<float> onRightMassChanged;

    // Runtime state
    private float animatedBeamAngle;
    private float animatedPanOffset;
    private bool wasBalanced;
    private bool hasCachedBaseTransforms;

    // Baseline transforms cached at Start
    private Vector3 beamBaseEuler;
    private Vector3 leftPanBaseLocalPos;
    private Vector3 rightPanBaseLocalPos;
    private Vector3[] leftLinkedBaseLocalPositions;
    private Vector3[] rightLinkedBaseLocalPositions;

    // --- Public API ---

    /// <summary>
    /// Current total grams on the left pan.
    /// Uses PowderDepositZone if wired, otherwise falls back to leftZone.
    /// </summary>
    public float LeftMassGrams
    {
        get
        {
            if (powderDepositZone != null) return powderDepositZone.DepositedGrams;
            return leftZone != null ? leftZone.TotalGrams : 0f;
        }
    }

    /// <summary>
    /// Current total grams on the right pan.
    /// Uses physical weights placed inside the right pan zone.
    /// </summary>
    public float RightMassGrams
    {
        get
        {
            return rightZone != null ? rightZone.TotalGrams : 0f;
        }
    }

    /// <summary>Signed gram difference (right - left - zeroOffset). Positive = right heavier.</summary>
    public float DifferenceGrams => RightMassGrams - LeftMassGrams - zeroOffsetGrams;

    /// <summary>True when |DifferenceGrams| is within balanceToleranceGrams.</summary>
    public bool IsBalanced => Mathf.Abs(DifferenceGrams) <= balanceToleranceGrams;

    public bool IsParchmentOnLeft => leftZone != null && leftZone.HasParchment;
    public bool IsParchmentOnRight => rightZone != null && rightZone.HasParchment;

    public bool HasRequiredParchment()
    {
        return IsParchmentOnLeft && IsParchmentOnRight;
    }

    public bool IsRightMassAtLeast(float grams)
    {
        return RightMassGrams >= grams;
    }

    public bool IsLeftMassAtLeast(float grams)
    {
        return LeftMassGrams >= grams;
    }

    public bool IsBalancedWithTarget(float targetGrams, float toleranceGrams)
    {
        float safeTolerance = Mathf.Max(0f, toleranceGrams);
        return Mathf.Abs(LeftMassGrams - targetGrams) <= safeTolerance && IsBalanced;
    }

    // --- Lifecycle ---

    private void Start()
    {
        CacheBaseTransforms();
        WarnIfReferencesMissing();

        if (leftZone != null)
            leftZone.onMassChanged.AddListener(HandleLeftMassChanged);
        if (rightZone != null)
            rightZone.onMassChanged.AddListener(HandleRightMassChanged);
        if (powderDepositZone != null)
            powderDepositZone.onDepositChanged.AddListener(HandleLeftMassChanged);

        wasBalanced = IsBalanced;
    }

    private void OnDestroy()
    {
        if (leftZone != null)
            leftZone.onMassChanged.RemoveListener(HandleLeftMassChanged);
        if (rightZone != null)
            rightZone.onMassChanged.RemoveListener(HandleRightMassChanged);
        if (powderDepositZone != null)
            powderDepositZone.onDepositChanged.RemoveListener(HandleLeftMassChanged);
    }

    private void HandleLeftMassChanged(float grams) => onLeftMassChanged?.Invoke(grams);
    private void HandleRightMassChanged(float grams) => onRightMassChanged?.Invoke(grams);

    private void Update()
    {
        AnimateBeamAndPans();
        EvaluateBalanceState();
    }

    // --- Visual Animation ---

    private void AnimateBeamAndPans()
    {
        if (!hasCachedBaseTransforms)
            CacheBaseTransforms();

        float normalized = Mathf.Clamp(DifferenceGrams / Mathf.Max(maxImbalanceGrams, 0.001f), -1f, 1f);
        float targetAngle = normalized * maxBeamAngleDegrees;
        float targetPanOffset = normalized * maxPanOffsetMeters;

        float dt = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        animatedBeamAngle = Mathf.Lerp(animatedBeamAngle, targetAngle, dt);
        animatedPanOffset = Mathf.Lerp(animatedPanOffset, targetPanOffset, dt);

        if (beamVisual != null)
        {
            Vector3 euler = beamBaseEuler;
            euler.z += animatedBeamAngle;
            beamVisual.localEulerAngles = euler;
        }

        if (leftPanVisual != null)
        {
            Vector3 pos = leftPanBaseLocalPos;
            pos.y += animatedPanOffset; // right heavier -> left pan rises
            leftPanVisual.localPosition = pos;
        }

        ApplyLinkedPanTransforms(leftPanLinkedTransforms, leftLinkedBaseLocalPositions, animatedPanOffset);

        if (rightPanVisual != null)
        {
            Vector3 pos = rightPanBaseLocalPos;
            pos.y -= animatedPanOffset; // right heavier -> right pan falls
            rightPanVisual.localPosition = pos;
        }

        ApplyLinkedPanTransforms(rightPanLinkedTransforms, rightLinkedBaseLocalPositions, -animatedPanOffset);
    }

    // --- Balance State ---

    private void EvaluateBalanceState()
    {
        bool balanced = IsBalanced;
        if (balanced == wasBalanced) return;
        wasBalanced = balanced;
        if (balanced) onBalanced?.Invoke();
        else onUnbalanced?.Invoke();
    }

    private void CacheBaseTransforms()
    {
        if (beamVisual != null) beamBaseEuler = beamVisual.localEulerAngles;
        if (leftPanVisual != null) leftPanBaseLocalPos = leftPanVisual.localPosition;
        if (rightPanVisual != null) rightPanBaseLocalPos = rightPanVisual.localPosition;
        leftLinkedBaseLocalPositions = CacheLinkedLocalPositions(leftPanLinkedTransforms);
        rightLinkedBaseLocalPositions = CacheLinkedLocalPositions(rightPanLinkedTransforms);
        hasCachedBaseTransforms = true;
    }

    private Vector3[] CacheLinkedLocalPositions(Transform[] linkedTransforms)
    {
        if (linkedTransforms == null)
            return new Vector3[0];

        Vector3[] cachedPositions = new Vector3[linkedTransforms.Length];
        for (int i = 0; i < linkedTransforms.Length; i++)
            cachedPositions[i] = linkedTransforms[i] != null ? linkedTransforms[i].localPosition : Vector3.zero;

        return cachedPositions;
    }

    private void ApplyLinkedPanTransforms(Transform[] linkedTransforms, Vector3[] cachedPositions, float yOffset)
    {
        if (linkedTransforms == null || cachedPositions == null)
            return;

        int count = Mathf.Min(linkedTransforms.Length, cachedPositions.Length);
        for (int i = 0; i < count; i++)
        {
            Transform linked = linkedTransforms[i];
            if (linked == null || linked == leftPanVisual || linked == rightPanVisual)
                continue;

            Vector3 pos = cachedPositions[i];
            pos.y += yOffset;
            linked.localPosition = pos;
        }
    }

    private void WarnIfReferencesMissing()
    {
        if (leftZone == null)
            Debug.LogWarning("[MG_BalanceController] leftZone is not assigned. Left mass will read as 0.", this);

        if (rightZone == null)
            Debug.LogWarning("[MG_BalanceController] rightZone is not assigned. Right mass will read as 0.", this);

        if (beamVisual == null)
            Debug.LogWarning("[MG_BalanceController] beamVisual is not assigned. Beam tilt will be skipped.", this);

        if (leftPanVisual == null)
            Debug.LogWarning("[MG_BalanceController] leftPanVisual is not assigned. Left pan movement will be skipped.", this);

        if (rightPanVisual == null)
            Debug.LogWarning("[MG_BalanceController] rightPanVisual is not assigned. Right pan movement will be skipped.", this);
    }

    [ContextMenu("DebugPrintMass")]
    public void DebugPrintMass()
    {
        Debug.Log(
            $"[MG_BalanceController] Left={LeftMassGrams:0.###}g Right={RightMassGrams:0.###}g Difference={DifferenceGrams:0.###}g Balanced={IsBalanced} ParchmentLeft={IsParchmentOnLeft} ParchmentRight={IsParchmentOnRight}",
            this);
    }

    [ContextMenu("ForceRecalibrateZero")]
    public void ForceRecalibrateZero()
    {
        zeroOffsetGrams = RightMassGrams - LeftMassGrams;
        DebugPrintMass();
    }

    [ContextMenu("ResetVisual")]
    public void ResetVisual()
    {
        if (!hasCachedBaseTransforms)
            CacheBaseTransforms();

        animatedBeamAngle = 0f;
        animatedPanOffset = 0f;

        if (beamVisual != null)
            beamVisual.localEulerAngles = beamBaseEuler;

        if (leftPanVisual != null)
            leftPanVisual.localPosition = leftPanBaseLocalPos;

        if (rightPanVisual != null)
            rightPanVisual.localPosition = rightPanBaseLocalPos;

        ApplyLinkedPanTransforms(leftPanLinkedTransforms, leftLinkedBaseLocalPositions, 0f);
        ApplyLinkedPanTransforms(rightPanLinkedTransforms, rightLinkedBaseLocalPositions, 0f);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxImbalanceGrams = Mathf.Max(0.001f, maxImbalanceGrams);
        balanceToleranceGrams = Mathf.Max(0.0001f, balanceToleranceGrams);
        smoothSpeed = Mathf.Max(0.1f, smoothSpeed);
        maxBeamAngleDegrees = Mathf.Clamp(maxBeamAngleDegrees, -45f, 45f);
    }
#endif
}
