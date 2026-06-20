using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class HornSpoon : MonoBehaviour
{
    private enum LocalAxis
    {
        Up,
        Down,
        Forward,
        Back,
        Right,
        Left
    }

    [Header("Interaction")]
    [SerializeField] private bool requireHeldToTransfer = true;

    [Tooltip("OFF untuk sistem baru. Kalau ON, sendok bisa auto-scoop dari PowderContainer lama.")]
    [SerializeField] private bool allowDirectPowderContainerScoop = false;

    [Header("Capacity")]
    [SerializeField] private float maxCapacityMg = 50f;
    [SerializeField] private float currentAmountMg = 0f;

    [Header("Legacy Direct Scoop")]
    [SerializeField] private float scoopRateMgPerSecond = 80f;

    [Header("Tip Detection")]
    [SerializeField] private Transform tipTransform;
    [SerializeField] private float detectionRadius = 0.06f;
    [SerializeField] private LayerMask detectionLayerMask = ~0;

    [Header("Powder Visual on Spoon")]
    [SerializeField] private Transform powderHoldPoint;
    [SerializeField] private Transform powderMesh;
    [SerializeField] private bool createDefaultVisualIfMissing = false;

    [Tooltip("ON kalau visual PowderOnSpoonVisual sudah kamu atur manual di scene.")]
    [SerializeField] private bool preserveAuthoredPowderVisualTransform = true;

    [SerializeField] private Vector3 emptyLocalScale = new Vector3(0.8f, 0.001f, 0.8f);
    [SerializeField] private Vector3 fullLocalScale = new Vector3(0.8f, 0.25f, 0.8f);
    [SerializeField] private Vector3 emptyLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 fullLocalPosition = new Vector3(0f, 0.125f, 0f);

    [SerializeField] private Renderer powderRenderer;
    [SerializeField] private Material powderMaterial;
    [SerializeField] private Color powderColor = new Color(0.96f, 0.96f, 0.92f, 1f);

    [Header("Dump By Rotation")]
    [SerializeField] private bool enableDumpByRotation = true;
    [SerializeField] private bool requireHeldToDump = true;

    [Tooltip("Isi dengan PowderHoldPoint. Kalau arah deteksi salah, ganti Axis To Point Down.")]
    [SerializeField] private Transform dumpOrientationReference;

    [SerializeField] private LocalAxis axisToPointDown = LocalAxis.Up;

    [Range(0.1f, 1f)]
    [SerializeField] private float upsideDownDotThreshold = 0.75f;

    [SerializeField] private float requiredUpsideDownTime = 0.45f;
    [SerializeField] private float dumpCooldown = 0.5f;
    [SerializeField] private ParticleSystem dumpFx;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;
    [SerializeField] private float dumpDownDot;
    [SerializeField] private float dumpTimer;
    [SerializeField] private bool canDumpAgain = true;

    [Header("Events")]
    public UnityEvent<float> onAmountChanged;
    public UnityEvent onSpoonFull;
    public UnityEvent onSpoonEmpty;
    public UnityEvent onPowderDumped;

    private XRGrabInteractable grabInteractable;
    private float nextAllowedDumpTime;

    public float MaxCapacityMg => maxCapacityMg;
    public float CurrentAmountMg => currentAmountMg;
    public float FillRatio => maxCapacityMg > 0f ? Mathf.Clamp01(currentAmountMg / maxCapacityMg) : 0f;

    public bool IsEmpty => currentAmountMg <= 0.001f;
    public bool IsFull => currentAmountMg >= maxCapacityMg - 0.001f;
    public bool CanReceivePowder => !IsFull;

    public Transform TipTransform => tipTransform != null ? tipTransform : transform;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (tipTransform == null)
            tipTransform = FindChildByName("SpoonTip");

        if (powderHoldPoint == null)
            powderHoldPoint = FindChildByName("PowderHoldPoint");

        if (powderMesh == null)
            powderMesh = FindChildByName("PowderOnSpoonVisual");

        if (dumpOrientationReference == null)
        {
            if (powderHoldPoint != null)
                dumpOrientationReference = powderHoldPoint;
            else if (tipTransform != null)
                dumpOrientationReference = tipTransform;
            else
                dumpOrientationReference = transform;
        }
    }

    private void Start()
    {
        if (powderMesh == null && createDefaultVisualIfMissing)
            CreateDefaultPowderVisual();

        if (powderRenderer == null && powderMesh != null)
            powderRenderer = powderMesh.GetComponentInChildren<Renderer>(true);

        ApplyPowderMaterial();
        UpdateVisual();
    }

    private void Update()
    {
        UpdateLegacyDirectScoop();
        UpdateDumpByRotation();
    }

    private void UpdateLegacyDirectScoop()
    {
        if (!allowDirectPowderContainerScoop)
            return;

        if (tipTransform == null)
            return;

        if (!CanTransfer())
            return;

        if (IsFull)
            return;

        Collider[] hits = Physics.OverlapSphere(TipTransform.position, detectionRadius, detectionLayerMask);

        PowderContainer nearestPowder = null;

        foreach (Collider hit in hits)
        {
            if (hit == null)
                continue;

            nearestPowder = hit.GetComponentInParent<PowderContainer>();

            if (nearestPowder != null)
                break;
        }

        if (nearestPowder == null || nearestPowder.IsEmpty)
            return;

        float availableSpace = Mathf.Max(0f, maxCapacityMg - currentAmountMg);
        float requestMg = Mathf.Min(scoopRateMgPerSecond * Time.deltaTime, availableSpace);
        float takenMg = nearestPowder.TakePowder(requestMg);

        AddPowder(takenMg);

        if (debugLogs && takenMg > 0.001f)
            Debug.Log($"[HornSpoon] Direct scoop {takenMg:0.###} mg from {nearestPowder.name}", this);
    }

    private void UpdateDumpByRotation()
    {
        if (!enableDumpByRotation)
            return;

        if (IsEmpty)
        {
            dumpTimer = 0f;
            return;
        }

        if (requireHeldToDump && !IsHeld())
        {
            dumpTimer = 0f;
            return;
        }

        if (dumpOrientationReference == null)
            return;

        Vector3 checkedAxis = GetWorldAxis(dumpOrientationReference, axisToPointDown);
        dumpDownDot = Vector3.Dot(checkedAxis.normalized, Vector3.down);

        bool isUpsideDown = dumpDownDot >= upsideDownDotThreshold;

        if (!isUpsideDown)
        {
            dumpTimer = 0f;

            if (dumpDownDot < 0.25f)
                canDumpAgain = true;

            return;
        }

        if (!canDumpAgain)
            return;

        dumpTimer += Time.deltaTime;

        if (dumpTimer >= requiredUpsideDownTime && Time.time >= nextAllowedDumpTime)
            DumpPowderByRotation();
    }

    public float AddPowder(float amountMg)
    {
        float safeAmount = Mathf.Max(0f, amountMg);

        if (safeAmount <= 0.001f)
            return 0f;

        float before = currentAmountMg;
        float availableSpace = Mathf.Max(0f, maxCapacityMg - currentAmountMg);
        float acceptedMg = Mathf.Min(safeAmount, availableSpace);

        currentAmountMg = Mathf.Clamp(currentAmountMg + acceptedMg, 0f, maxCapacityMg);

        if (!Mathf.Approximately(before, currentAmountMg))
        {
            UpdateVisual();
            onAmountChanged?.Invoke(currentAmountMg);

            if (IsFull)
                onSpoonFull?.Invoke();
        }

        return acceptedMg;
    }

    public float AddPowderMg(float amountMg)
    {
        return AddPowder(amountMg);
    }

    public float RemovePowder(float amountMg)
    {
        float safeAmount = Mathf.Max(0f, amountMg);

        if (safeAmount <= 0.001f)
            return 0f;

        float before = currentAmountMg;
        float removedMg = Mathf.Min(currentAmountMg, safeAmount);

        currentAmountMg = Mathf.Clamp(currentAmountMg - removedMg, 0f, maxCapacityMg);

        if (!Mathf.Approximately(before, currentAmountMg))
        {
            UpdateVisual();
            onAmountChanged?.Invoke(currentAmountMg);

            if (IsEmpty)
                onSpoonEmpty?.Invoke();
        }

        return removedMg;
    }

    public float RemovePowderMg(float amountMg)
    {
        return RemovePowder(amountMg);
    }

    public void SetPowderMg(float amountMg)
    {
        float before = currentAmountMg;
        currentAmountMg = Mathf.Clamp(amountMg, 0f, maxCapacityMg);

        if (!Mathf.Approximately(before, currentAmountMg))
        {
            UpdateVisual();
            onAmountChanged?.Invoke(currentAmountMg);

            if (IsFull)
                onSpoonFull?.Invoke();

            if (IsEmpty)
                onSpoonEmpty?.Invoke();
        }
    }

    public void ClearPowder()
    {
        SetPowderMg(0f);
    }

    public void DumpPowderByRotation()
    {
        if (IsEmpty)
            return;

        float removedMg = RemovePowder(maxCapacityMg + 99999f);

        if (removedMg <= 0.001f)
            return;

        PlayDumpFx();

        dumpTimer = 0f;
        canDumpAgain = false;
        nextAllowedDumpTime = Time.time + dumpCooldown;

        onPowderDumped?.Invoke();

        if (debugLogs)
            Debug.Log($"[HornSpoon] Dumped {removedMg:0.###} mg by rotation.", this);
    }

    public void UpdateVisual()
    {
        if (powderMesh == null)
            return;

        bool hasPowder = !IsEmpty;
        powderMesh.gameObject.SetActive(hasPowder);

        if (!hasPowder)
            return;

        if (!preserveAuthoredPowderVisualTransform)
        {
            float t = FillRatio;
            powderMesh.localScale = Vector3.Lerp(emptyLocalScale, fullLocalScale, t);
            powderMesh.localPosition = Vector3.Lerp(emptyLocalPosition, fullLocalPosition, t);
        }

        ApplyPowderMaterial();
    }

    public void RefreshVisual()
    {
        UpdateVisual();
    }

    private bool CanTransfer()
    {
        if (!requireHeldToTransfer)
            return true;

        return IsHeld();
    }

    private bool IsHeld()
    {
        return grabInteractable != null && grabInteractable.isSelected;
    }

    private void PlayDumpFx()
    {
        if (dumpFx == null)
            return;

        Transform source = dumpOrientationReference != null ? dumpOrientationReference : transform;

        dumpFx.transform.position = source.position;
        dumpFx.transform.rotation = source.rotation;

        dumpFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        dumpFx.Play(true);
    }

    private Vector3 GetWorldAxis(Transform reference, LocalAxis axis)
    {
        switch (axis)
        {
            case LocalAxis.Up:
                return reference.up;

            case LocalAxis.Down:
                return -reference.up;

            case LocalAxis.Forward:
                return reference.forward;

            case LocalAxis.Back:
                return -reference.forward;

            case LocalAxis.Right:
                return reference.right;

            case LocalAxis.Left:
                return -reference.right;

            default:
                return reference.up;
        }
    }

    private void ApplyPowderMaterial()
    {
        if (powderRenderer == null)
            return;

        if (powderMaterial != null)
        {
            powderRenderer.sharedMaterial = powderMaterial;
            return;
        }

        Material material = powderRenderer.material;

        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", powderColor);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", powderColor);
    }

    private void CreateDefaultPowderVisual()
    {
        GameObject powderObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        powderObject.name = "Runtime_PowderOnSpoonVisual";
        powderObject.transform.SetParent(powderHoldPoint != null ? powderHoldPoint : transform, false);
        powderObject.transform.localPosition = fullLocalPosition;
        powderObject.transform.localRotation = Quaternion.identity;
        powderObject.transform.localScale = fullLocalScale;

        Collider col = powderObject.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        powderMesh = powderObject.transform;
        powderRenderer = powderObject.GetComponent<Renderer>();

        if (powderRenderer != null)
            ApplyPowderMaterial();
    }

    private Transform FindChildByName(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxCapacityMg = Mathf.Max(1f, maxCapacityMg);
        currentAmountMg = Mathf.Clamp(currentAmountMg, 0f, maxCapacityMg);
        detectionRadius = Mathf.Max(0.001f, detectionRadius);
        scoopRateMgPerSecond = Mathf.Max(0f, scoopRateMgPerSecond);
        upsideDownDotThreshold = Mathf.Clamp(upsideDownDotThreshold, 0.1f, 1f);
        requiredUpsideDownTime = Mathf.Max(0.05f, requiredUpsideDownTime);
        dumpCooldown = Mathf.Max(0f, dumpCooldown);
    }
#endif
}