using UnityEngine;
using UnityEngine.Events;

public enum BalanceBeamRotationAxis
{
    LocalX,
    LocalY,
    LocalZ
}

public class MG_BalanceController : MonoBehaviour
{
    [Header("Physical Zones")]
    [SerializeField] private WeightingZone leftZone;
    [SerializeField] private WeightingZone rightZone;

    [Header("Powder Tracking")]
    [SerializeField] private PowderDepositZone powderDepositZone;

    [Header("Visual References")]
    [Tooltip("Assign Balance_ScaleBeam langsung di sini.")]
    [SerializeField] private Transform beamVisual;

    [SerializeField] private Transform leftPanVisual;
    [SerializeField] private Transform rightPanVisual;

    [Header("Pan Linked Transforms")]
    [SerializeField] private Transform[] leftPanLinkedTransforms;
    [SerializeField] private Transform[] rightPanLinkedTransforms;

    [Header("Beam Base Rotation")]
    [Tooltip("Rotasi netral Balance_ScaleBeam. Untuk model kamu kemungkinan: -90, 90, 90.")]
    [SerializeField] private Vector3 beamNeutralEuler = new Vector3(-90f, 90f, 90f);

    [Tooltip("Kalau aktif, script selalu memakai Beam Neutral Euler sebagai rotasi dasar.")]
    [SerializeField] private bool forceBeamNeutralEuler = true;

    [Header("Beam Rotation")]
    [SerializeField] private BalanceBeamRotationAxis beamRotationAxis = BalanceBeamRotationAxis.LocalX;
    [SerializeField] private bool invertBeamDirection = false;

    [Header("Pan Movement")]
    [SerializeField] private bool invertPanDirection = false;

    [Header("Calibration")]
    [SerializeField] private float maxBeamAngleDegrees = 60f;
    [SerializeField] private float maxImbalanceGrams = 0.15f;
    [Tooltip("Jika ON, rentang kemiringan beam diskalakan ke massa anak timbangan (piring " +
             "kanan), bukan nilai tetap. Ini membuat gerak beam tetap bertahap baik untuk " +
             "50 mg maupun 5 g — tanpa ini, target gram besar bikin beam mentok lalu 'bum' " +
             "setara di akhir.")]
    [SerializeField] private bool scaleImbalanceToCounterweight = true;
    [Tooltip("Batas bawah rentang imbalance (gram) saat penskalaan aktif, agar massa sangat " +
             "kecil tetap punya rentang gerak wajar.")]
    [SerializeField] private float minImbalanceGrams = 0.03f;
    [SerializeField] private float maxPanOffsetMeters = 0.04f;
    [SerializeField] private float balanceToleranceGrams = 0.005f;
    [SerializeField] private float smoothSpeed = 6f;
    [SerializeField] private float zeroOffsetGrams = 0f;

    [Header("Events")]
    public UnityEvent onBalanced;
    public UnityEvent onUnbalanced;
    public UnityEvent<float> onLeftMassChanged;
    public UnityEvent<float> onRightMassChanged;

    private Quaternion beamBaseLocalRotation;
    private Vector3 leftPanBaseLocalPosition;
    private Vector3 rightPanBaseLocalPosition;
    private Vector3[] leftLinkedBasePositions;
    private Vector3[] rightLinkedBasePositions;

    private float animatedBeamAngle;
    private float animatedPanOffset;
    private bool hasCachedBasePose;
    private bool wasBalanced;

    public float LeftMassGrams
    {
        get
        {
            if (powderDepositZone != null)
                return powderDepositZone.DepositedGrams;

            return leftZone != null ? leftZone.TotalGrams : 0f;
        }
    }

    public float RightMassGrams
    {
        get
        {
            return rightZone != null ? rightZone.TotalGrams : 0f;
        }
    }

    public float DifferenceGrams => RightMassGrams - LeftMassGrams - zeroOffsetGrams;

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

    private void Awake()
    {
        CacheBasePose();
    }

    private void Start()
    {
        SubscribeEvents();
        wasBalanced = IsBalanced;
        ApplyImmediateVisual();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void Update()
    {
        if (!hasCachedBasePose)
            CacheBasePose();

        AnimateVisual();
        EvaluateBalanceState();
    }

    private void SubscribeEvents()
    {
        if (leftZone != null)
            leftZone.onMassChanged.AddListener(HandleLeftMassChanged);

        if (rightZone != null)
            rightZone.onMassChanged.AddListener(HandleRightMassChanged);

        if (powderDepositZone != null)
            powderDepositZone.onDepositChanged.AddListener(HandleLeftMassChanged);
    }

    private void UnsubscribeEvents()
    {
        if (leftZone != null)
            leftZone.onMassChanged.RemoveListener(HandleLeftMassChanged);

        if (rightZone != null)
            rightZone.onMassChanged.RemoveListener(HandleRightMassChanged);

        if (powderDepositZone != null)
            powderDepositZone.onDepositChanged.RemoveListener(HandleLeftMassChanged);
    }

    private void HandleLeftMassChanged(float grams)
    {
        onLeftMassChanged?.Invoke(grams);
    }

    private void HandleRightMassChanged(float grams)
    {
        onRightMassChanged?.Invoke(grams);
    }

    // Rentang imbalance efektif. Saat penskalaan aktif, pakai massa terbesar dari kedua
    // piring sebagai acuan full-tilt: piring kosong vs anak timbangan 5 g → beam full,
    // lalu mendekati 0 secara bertahap saat bahan ditambah. Untuk 50 mg juga proporsional.
    private float GetEffectiveImbalanceReference()
    {
        if (!scaleImbalanceToCounterweight)
            return Mathf.Max(0.0001f, maxImbalanceGrams);

        float reference = Mathf.Max(RightMassGrams, LeftMassGrams);
        return Mathf.Max(minImbalanceGrams, reference);
    }

    private void AnimateVisual()
    {
        float safeMaxImbalance = GetEffectiveImbalanceReference();
        float normalized = Mathf.Clamp(DifferenceGrams / safeMaxImbalance, -1f, 1f);

        float beamSign = invertBeamDirection ? -1f : 1f;
        float panSign = invertPanDirection ? -1f : 1f;

        float targetBeamAngle = normalized * maxBeamAngleDegrees * beamSign;
        float targetPanOffset = normalized * maxPanOffsetMeters * panSign;

        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);

        animatedBeamAngle = Mathf.Lerp(animatedBeamAngle, targetBeamAngle, t);
        animatedPanOffset = Mathf.Lerp(animatedPanOffset, targetPanOffset, t);

        ApplyBeamRotation(animatedBeamAngle);
        ApplyPanMovement(animatedPanOffset);
    }

    private void ApplyImmediateVisual()
    {
        float safeMaxImbalance = GetEffectiveImbalanceReference();
        float normalized = Mathf.Clamp(DifferenceGrams / safeMaxImbalance, -1f, 1f);

        float beamSign = invertBeamDirection ? -1f : 1f;
        float panSign = invertPanDirection ? -1f : 1f;

        animatedBeamAngle = normalized * maxBeamAngleDegrees * beamSign;
        animatedPanOffset = normalized * maxPanOffsetMeters * panSign;

        ApplyBeamRotation(animatedBeamAngle);
        ApplyPanMovement(animatedPanOffset);
    }

    private void ApplyBeamRotation(float angle)
    {
        if (beamVisual == null)
            return;

        Vector3 euler = beamNeutralEuler;

        switch (beamRotationAxis)
        {
            case BalanceBeamRotationAxis.LocalX:
                euler.x = beamNeutralEuler.x + angle;
                euler.y = beamNeutralEuler.y;
                euler.z = beamNeutralEuler.z;
                break;

            case BalanceBeamRotationAxis.LocalY:
                euler.x = beamNeutralEuler.x;
                euler.y = beamNeutralEuler.y + angle;
                euler.z = beamNeutralEuler.z;
                break;

            case BalanceBeamRotationAxis.LocalZ:
                euler.x = beamNeutralEuler.x;
                euler.y = beamNeutralEuler.y;
                euler.z = beamNeutralEuler.z + angle;
                break;
        }

        beamVisual.localEulerAngles = euler;
    }

    private Vector3 GetBeamAxis()
    {
        switch (beamRotationAxis)
        {
            case BalanceBeamRotationAxis.LocalY:
                return Vector3.up;

            case BalanceBeamRotationAxis.LocalZ:
                return Vector3.forward;

            default:
                return Vector3.right;
        }
    }

    private void ApplyPanMovement(float panOffset)
    {
        if (leftPanVisual != null)
        {
            Vector3 pos = leftPanBaseLocalPosition;
            pos.y += panOffset;
            leftPanVisual.localPosition = pos;
        }

        if (rightPanVisual != null)
        {
            Vector3 pos = rightPanBaseLocalPosition;
            pos.y -= panOffset;
            rightPanVisual.localPosition = pos;
        }

        ApplyLinkedTransforms(leftPanLinkedTransforms, leftLinkedBasePositions, panOffset);
        ApplyLinkedTransforms(rightPanLinkedTransforms, rightLinkedBasePositions, -panOffset);
    }

    private void ApplyLinkedTransforms(Transform[] linkedTransforms, Vector3[] basePositions, float yOffset)
    {
        if (linkedTransforms == null || basePositions == null)
            return;

        int count = Mathf.Min(linkedTransforms.Length, basePositions.Length);

        for (int i = 0; i < count; i++)
        {
            Transform target = linkedTransforms[i];

            if (target == null)
                continue;

            Vector3 pos = basePositions[i];
            pos.y += yOffset;
            target.localPosition = pos;
        }
    }

    private void EvaluateBalanceState()
    {
        bool balanced = IsBalanced;

        if (balanced == wasBalanced)
            return;

        wasBalanced = balanced;

        if (balanced)
            onBalanced?.Invoke();
        else
            onUnbalanced?.Invoke();
    }

    private void CacheBasePose()
    {
        if (beamVisual != null)
        {
            beamBaseLocalRotation = forceBeamNeutralEuler
                ? Quaternion.Euler(beamNeutralEuler)
                : beamVisual.localRotation;
        }

        if (leftPanVisual != null)
            leftPanBaseLocalPosition = leftPanVisual.localPosition;

        if (rightPanVisual != null)
            rightPanBaseLocalPosition = rightPanVisual.localPosition;

        leftLinkedBasePositions = CachePositions(leftPanLinkedTransforms);
        rightLinkedBasePositions = CachePositions(rightPanLinkedTransforms);

        hasCachedBasePose = true;
    }

    private Vector3[] CachePositions(Transform[] transforms)
    {
        if (transforms == null)
            return new Vector3[0];

        Vector3[] result = new Vector3[transforms.Length];

        for (int i = 0; i < transforms.Length; i++)
        {
            result[i] = transforms[i] != null
                ? transforms[i].localPosition
                : Vector3.zero;
        }

        return result;
    }

    [ContextMenu("Apply Neutral Beam Rotation")]
    public void ApplyNeutralBeamRotation()
    {
        if (beamVisual == null)
            return;

        beamVisual.localRotation = Quaternion.Euler(beamNeutralEuler);
        beamBaseLocalRotation = beamVisual.localRotation;
    }

    [ContextMenu("Recache Current Pose As Base")]
    public void RecacheCurrentPoseAsBase()
    {
        CacheBasePose();
        ApplyImmediateVisual();
    }

    [ContextMenu("Reset Visual To Base Pose")]
    public void ResetVisualToBasePose()
    {
        if (!hasCachedBasePose)
            CacheBasePose();

        animatedBeamAngle = 0f;
        animatedPanOffset = 0f;

        if (beamVisual != null)
            beamVisual.localRotation = forceBeamNeutralEuler
                ? Quaternion.Euler(beamNeutralEuler)
                : beamBaseLocalRotation;

        if (leftPanVisual != null)
            leftPanVisual.localPosition = leftPanBaseLocalPosition;

        if (rightPanVisual != null)
            rightPanVisual.localPosition = rightPanBaseLocalPosition;

        ApplyLinkedTransforms(leftPanLinkedTransforms, leftLinkedBasePositions, 0f);
        ApplyLinkedTransforms(rightPanLinkedTransforms, rightLinkedBasePositions, 0f);
    }

    [ContextMenu("Recalibrate Zero Using Current Mass")]
    public void RecalibrateZeroUsingCurrentMass()
    {
        zeroOffsetGrams = RightMassGrams - LeftMassGrams;
        DebugPrintMass();
    }

    [ContextMenu("Debug Print Mass")]
    public void DebugPrintMass()
    {
        Debug.Log(
            $"[MG_BalanceController] Left={LeftMassGrams:0.###}g | Right={RightMassGrams:0.###}g | Difference={DifferenceGrams:0.###}g | Balanced={IsBalanced}",
            this
        );
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxBeamAngleDegrees = Mathf.Clamp(maxBeamAngleDegrees, 0f, 120f);
        maxImbalanceGrams = Mathf.Max(0.0001f, maxImbalanceGrams);
        maxPanOffsetMeters = Mathf.Max(0f, maxPanOffsetMeters);
        balanceToleranceGrams = Mathf.Max(0f, balanceToleranceGrams);
        smoothSpeed = Mathf.Max(0.01f, smoothSpeed);
    }
#endif
}