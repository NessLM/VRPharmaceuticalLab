using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controls the visual behavior of the MG analytical balance scale.
/// Reads logical gram values from left/right WeightingZone components and smoothly
/// animates the beam tilt and pan vertical displacement proportionally.
///
/// Convention:
///   DifferenceGrams = RightMassGrams - LeftMassGrams
///   Positive difference → right is heavier → beam tilts right-down, right pan descends.
///   Adjust maxBeamAngleDegrees to a negative value if the visual is mirrored in-world.
///
/// Attach to: MG_BalanceScale root GameObject.
/// Wire beamVisual, leftPanVisual, rightPanVisual, leftZone, rightZone in the Inspector.
/// </summary>
public class MG_BalanceController : MonoBehaviour
{
    [Header("Zones (Legacy — used as fallback if Virtual system is not wired)")]
    [Tooltip("Trigger zone positioned above the LEFT pan.")]
    [SerializeField] private WeightingZone leftZone;
    [Tooltip("Trigger zone positioned above the RIGHT pan.")]
    [SerializeField] private WeightingZone rightZone;

    [Header("Virtual Balance System (takes priority over Zones when wired)")]
    [Tooltip("VirtualWeightSelector that provides the locked right-pan target mass.")]
    [SerializeField] private VirtualWeightSelector virtualWeightSelector;
    [Tooltip("PowderDepositZone on LeftWeighingZone that tracks deposited powder grams.")]
    [SerializeField] private PowderDepositZone powderDepositZone;
    [Tooltip("Before virtual weights are accepted, still read physical weights placed in the right pan zone.")]
    [SerializeField] private bool useRightZoneWhenVirtualSelectorUnlocked = true;

    [Header("Visual References")]
    [Tooltip("The beam mesh Transform that tilts around its local Z axis.")]
    [SerializeField] private Transform beamVisual;
    [Tooltip("Left pan mesh Transform. Moves up when right side is heavier.")]
    [SerializeField] private Transform leftPanVisual;
    [Tooltip("Right pan mesh Transform. Moves down when right side is heavier.")]
    [SerializeField] private Transform rightPanVisual;

    [Header("Calibration")]
    [Tooltip("Beam tilt angle (degrees) at maximum imbalance. Negate if beam tilts the wrong way.")]
    [SerializeField] private float maxBeamAngleDegrees = 12f;
    [Tooltip("Gram difference that corresponds to maximum beam angle.")]
    [SerializeField] private float maxImbalanceGrams = 500f;
    [Tooltip("Vertical pan displacement (meters) at maximum imbalance.")]
    [SerializeField] private float maxPanOffsetMeters = 0.015f;
    [Tooltip("Grams difference considered balanced. Tune to the scale's required precision.")]
    [SerializeField] private float balanceToleranceGrams = 0.5f;
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

    // Baseline transforms cached at Start
    private Vector3 beamBaseEuler;
    private Vector3 leftPanBaseLocalPos;
    private Vector3 rightPanBaseLocalPos;

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
    /// Uses VirtualWeightSelector locked target if wired, otherwise falls back to rightZone.
    /// </summary>
    public float RightMassGrams
    {
        get
        {
            if (virtualWeightSelector != null)
            {
                if (virtualWeightSelector.IsLocked)
                    return virtualWeightSelector.LockedRightMassGrams;

                if (useRightZoneWhenVirtualSelectorUnlocked && rightZone != null)
                    return rightZone.TotalGrams;

                return 0f;
            }

            return rightZone != null ? rightZone.TotalGrams : 0f;
        }
    }

    /// <summary>Signed gram difference (right - left - zeroOffset). Positive = right heavier.</summary>
    public float DifferenceGrams => RightMassGrams - LeftMassGrams - zeroOffsetGrams;

    /// <summary>True when |DifferenceGrams| is within balanceToleranceGrams.</summary>
    public bool IsBalanced => Mathf.Abs(DifferenceGrams) <= balanceToleranceGrams;

    // --- Lifecycle ---

    private void Start()
    {
        if (beamVisual != null) beamBaseEuler = beamVisual.localEulerAngles;
        if (leftPanVisual != null) leftPanBaseLocalPos = leftPanVisual.localPosition;
        if (rightPanVisual != null) rightPanBaseLocalPos = rightPanVisual.localPosition;

        if (leftZone != null)
            leftZone.onMassChanged.AddListener(HandleLeftMassChanged);
        if (rightZone != null)
            rightZone.onMassChanged.AddListener(HandleRightMassChanged);
    }

    private void OnDestroy()
    {
        if (leftZone != null)
            leftZone.onMassChanged.RemoveListener(HandleLeftMassChanged);
        if (rightZone != null)
            rightZone.onMassChanged.RemoveListener(HandleRightMassChanged);
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
        float normalized = Mathf.Clamp(DifferenceGrams / Mathf.Max(maxImbalanceGrams, 1f), -1f, 1f);
        float targetAngle = normalized * maxBeamAngleDegrees;
        float targetPanOffset = normalized * maxPanOffsetMeters;

        float dt = Time.deltaTime * smoothSpeed;
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
            pos.y += animatedPanOffset; // right heavier → left pan rises
            leftPanVisual.localPosition = pos;
        }

        if (rightPanVisual != null)
        {
            Vector3 pos = rightPanBaseLocalPos;
            pos.y -= animatedPanOffset; // right heavier → right pan falls
            rightPanVisual.localPosition = pos;
        }
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxImbalanceGrams = Mathf.Max(1f, maxImbalanceGrams);
        balanceToleranceGrams = Mathf.Max(0.01f, balanceToleranceGrams);
        smoothSpeed = Mathf.Max(0.1f, smoothSpeed);
        maxBeamAngleDegrees = Mathf.Clamp(maxBeamAngleDegrees, -45f, 45f);
    }
#endif
}
