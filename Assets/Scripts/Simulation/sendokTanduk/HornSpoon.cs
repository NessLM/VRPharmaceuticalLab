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

    [Header("Cream Visual on Spoon")]
    [SerializeField] private Transform creamMesh;
    [SerializeField] private Renderer creamRenderer;
    [SerializeField] private bool createCreamVisualIfMissing = true;

    [Header("Held Ingredient")]
    [SerializeField] private string currentIngredientId;
    [SerializeField] private string currentIngredientName;
    [SerializeField] private IngredientVisualType currentVisualType = IngredientVisualType.PowderWhiteCrystal;
    [SerializeField] private Material currentIngredientMaterial;
    [SerializeField] private Color currentIngredientColor = new Color(0.96f, 0.96f, 0.92f, 1f);
    [SerializeField] private bool requireIngredientProfileForDirectScoop = true;

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
    private Material defaultPowderMaterial;
    private Color defaultPowderColor;

    public float MaxCapacityMg => maxCapacityMg;
    public float CurrentAmountMg => currentAmountMg;
    public float FillRatio => maxCapacityMg > 0f ? Mathf.Clamp01(currentAmountMg / maxCapacityMg) : 0f;

    public bool IsEmpty => currentAmountMg <= 0.001f;
    public bool IsFull => currentAmountMg >= maxCapacityMg - 0.001f;
    public bool CanReceivePowder => !IsFull;
    public string CurrentIngredientId => currentIngredientId;
    public string CurrentIngredientName => currentIngredientName;
    public IngredientVisualType CurrentVisualType => currentVisualType;
    public bool IsHoldingCream => !IsEmpty && currentVisualType == IngredientVisualType.CreamOintment;

    public Transform TipTransform => tipTransform != null ? tipTransform : transform;

    private void Awake()
    {
        defaultPowderMaterial = powderMaterial;
        defaultPowderColor = powderColor;
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (tipTransform == null)
            tipTransform = FindChildByName("SpoonTip");

        if (powderHoldPoint == null)
            powderHoldPoint = FindChildByName("PowderHoldPoint");

        if (powderMesh == null)
            powderMesh = FindChildByName("PowderOnSpoonVisual");

        if (creamMesh == null)
            creamMesh = FindChildByName("CreamOnSpoonVisual");

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

        if (creamMesh == null && createCreamVisualIfMissing)
            CreateDefaultCreamVisual();

        if (powderRenderer == null && powderMesh != null)
            powderRenderer = powderMesh.GetComponentInChildren<Renderer>(true);

        if (creamRenderer == null && creamMesh != null)
            creamRenderer = creamMesh.GetComponentInChildren<Renderer>(true);

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
        IngredientVisualProfile nearestProfile = null;

        foreach (Collider hit in hits)
        {
            if (hit == null)
                continue;

            IngredientVisualProfile profile = hit.GetComponentInParent<IngredientVisualProfile>();
            if (requireIngredientProfileForDirectScoop && profile == null)
                continue;

            nearestPowder = hit.GetComponentInParent<PowderContainer>();

            if (nearestPowder != null)
            {
                nearestProfile = profile;
                break;
            }
        }

        if (nearestPowder == null ||
            !nearestPowder.IsAccessible ||
            nearestPowder.IsEmpty ||
            !CanAcceptIngredient(nearestProfile))
            return;

        float availableSpace = Mathf.Max(0f, maxCapacityMg - currentAmountMg);
        float requestMg = Mathf.Min(scoopRateMgPerSecond * Time.deltaTime, availableSpace);
        float takenMg = nearestPowder.TakePowder(requestMg);
        float acceptedMg = AddIngredient(takenMg, nearestProfile);
        if (acceptedMg < takenMg)
            nearestPowder.AddPowder(takenMg - acceptedMg);

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

        if (IsEmpty && string.IsNullOrEmpty(currentIngredientId))
            ApplyIngredientProfile(null);

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

    public bool CanAcceptIngredient(IngredientVisualProfile profile)
    {
        if (IsEmpty)
            return true;

        string sourceId = profile != null ? profile.IngredientId : "Difenhidramin";
        return string.Equals(currentIngredientId, sourceId, System.StringComparison.Ordinal);
    }

    public float AddIngredient(float amountMg, IngredientVisualProfile profile)
    {
        if (!CanAcceptIngredient(profile))
            return 0f;

        if (IsEmpty)
            ApplyIngredientProfile(profile);

        return AddPowder(amountMg);
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

        if (currentAmountMg <= 0.001f)
            ResetIngredientSelection();

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
        if (powderMesh == null && creamMesh == null)
            return;

        bool hasSubstance = !IsEmpty;
        bool showCream = hasSubstance && currentVisualType == IngredientVisualType.CreamOintment;
        bool showPowder = hasSubstance && !showCream;

        if (powderMesh != null)
            powderMesh.gameObject.SetActive(showPowder);
        if (creamMesh != null)
            creamMesh.gameObject.SetActive(showCream);

        if (!hasSubstance)
            return;

        if (showPowder && powderMesh != null && !preserveAuthoredPowderVisualTransform)
        {
            float t = FillRatio;
            powderMesh.localScale = Vector3.Lerp(emptyLocalScale, fullLocalScale, t);
            powderMesh.localPosition = Vector3.Lerp(emptyLocalPosition, fullLocalPosition, t);
        }

        ApplyHeldIngredientMaterial();
    }

    public void RefreshVisual()
    {
        UpdateVisual();
    }

    public void ConfigureIngredientScoopSupport(bool enableDirectProfileScoop, float newDetectionRadius)
    {
        allowDirectPowderContainerScoop = enableDirectProfileScoop;
        requireIngredientProfileForDirectScoop = true;
        detectionRadius = Mathf.Max(0.01f, newDetectionRadius);
    }

    public void EnsureCreamVisual(Material creamMaterial)
    {
        if (creamMesh == null)
            creamMesh = FindChildByName("CreamOnSpoonVisual");

        if (creamMesh == null)
            CreateDefaultCreamVisual();

        if (creamRenderer == null && creamMesh != null)
            creamRenderer = creamMesh.GetComponentInChildren<Renderer>(true);

        if (creamMesh != null)
        {
            CreamMoundVisual visual = creamMesh.GetComponent<CreamMoundVisual>();
            if (visual == null)
                visual = creamMesh.gameObject.AddComponent<CreamMoundVisual>();
            visual.Configure(0.04f, 0.026f, 0.004f, 0.018f, 0.024f, 0.0024f, creamMaterial);
            creamMesh.gameObject.SetActive(false);
        }
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

    private void ApplyHeldIngredientMaterial()
    {
        Renderer targetRenderer = currentVisualType == IngredientVisualType.CreamOintment
            ? creamRenderer
            : powderRenderer;

        if (targetRenderer == null)
            return;

        Material selectedMaterial = currentIngredientMaterial;
        if (selectedMaterial == null && currentVisualType != IngredientVisualType.CreamOintment)
            selectedMaterial = defaultPowderMaterial != null ? defaultPowderMaterial : powderMaterial;

        if (selectedMaterial != null)
        {
            targetRenderer.sharedMaterial = selectedMaterial;
            return;
        }

        Material runtimeMaterial = targetRenderer.material;
        if (runtimeMaterial == null)
            return;

        if (runtimeMaterial.HasProperty("_BaseColor"))
            runtimeMaterial.SetColor("_BaseColor", currentIngredientColor);
        if (runtimeMaterial.HasProperty("_Color"))
            runtimeMaterial.SetColor("_Color", currentIngredientColor);
    }

    private void ApplyIngredientProfile(IngredientVisualProfile profile)
    {
        if (profile == null)
        {
            currentIngredientId = "Difenhidramin";
            currentIngredientName = "Difenhidramin";
            currentVisualType = IngredientVisualType.PowderWhiteCrystal;
            currentIngredientMaterial = defaultPowderMaterial != null ? defaultPowderMaterial : powderMaterial;
            currentIngredientColor = defaultPowderColor;
            return;
        }

        currentIngredientId = profile.IngredientId;
        currentIngredientName = profile.DisplayName;
        currentVisualType = profile.VisualType;
        currentIngredientMaterial = profile.SpoonMaterial;
        currentIngredientColor = profile.FallbackColor;
    }

    private void ResetIngredientSelection()
    {
        currentIngredientId = string.Empty;
        currentIngredientName = string.Empty;
        currentVisualType = IngredientVisualType.PowderWhiteCrystal;
        currentIngredientMaterial = null;
        currentIngredientColor = defaultPowderColor;
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

    private void CreateDefaultCreamVisual()
    {
        GameObject creamObject = new GameObject("CreamOnSpoonVisual");
        creamObject.transform.SetParent(powderHoldPoint != null ? powderHoldPoint : transform, false);
        creamObject.transform.localPosition = Vector3.zero;
        creamObject.transform.localRotation = Quaternion.identity;
        creamObject.transform.localScale = Vector3.one * 0.6f;

        creamObject.AddComponent<MeshFilter>();
        creamRenderer = creamObject.AddComponent<MeshRenderer>();
        CreamMoundVisual visual = creamObject.AddComponent<CreamMoundVisual>();
        visual.Configure(0.04f, 0.026f, 0.004f, 0.018f, 0.022f, 0.0025f, currentIngredientMaterial);

        creamMesh = creamObject.transform;
        creamObject.SetActive(false);
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
