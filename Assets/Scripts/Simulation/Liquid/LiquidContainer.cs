using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

public class LiquidContainer : MonoBehaviour
{
    [Header("Liquid Data")]
    [SerializeField] private LiquidData currentLiquid;

    [Header("Amount")]
    [SerializeField] private float capacityMl = 100f;
    [SerializeField] private float currentMl = 0f;

    [Header("Liquid Visual")]
    [SerializeField] private Transform liquidVisual;
    [SerializeField] private Renderer liquidRenderer;
    [SerializeField] private Material liquidMaterial;
    [SerializeField] private bool hideWhenEmpty = true;

    [Header("Visual Size")]
    [SerializeField] private float bottomLocalY = 0.02f;
    [SerializeField] private float fullHeightLocal = 0.16f;
    [SerializeField] private float diameterXLocal = 0.07f;
    [SerializeField] private float diameterZLocal = 0.07f;
    [SerializeField] private Vector3 fillAxisLocal = Vector3.up;

    [Header("Visual Safety")]
    [SerializeField] private bool clampVisualHeightToCollider = true;
    [SerializeField, Range(0f, 0.3f)] private float visualRimPaddingPercent = 0.08f;

    [Header("Overflow Spill")]
    [SerializeField] private bool spillOverflowWhenFull = true;
    [SerializeField] private float overflowSpillDiameterPerSqrtMl = 0.01f;
    [SerializeField] private float maxOverflowSpillDiameter = 0.22f;
    [SerializeField] private float overflowSpillThickness = 0.001f;
    [SerializeField] private Color overflowSpillColor = new Color(0.25f, 0.65f, 1f, 0.35f);

    [Header("Tilt Spill")]
    [SerializeField] private bool enableTiltSpill = true;
    [SerializeField] private Transform mouthDirection;
    [SerializeField] private float spillTiltAngle = 90f;
    [SerializeField] private float spillRateMlPerSecond = 80f;

    [Header("Events")]
    public UnityEvent<float> onAmountChanged;
    public UnityEvent onBecameEmpty;
    public UnityEvent onBecameFull;

    public LiquidData CurrentLiquid => currentLiquid;
    public LiquidData LiquidType => currentLiquid;
    public float CurrentMl => currentMl;
    public float CurrentVolumeMl => currentMl;
    public float CapacityMl => capacityMl;
    public float MaxVolumeMl => capacityMl;
    public float AvailableCapacityMl => Mathf.Max(0f, capacityMl - currentMl);
    public float FillRatio => capacityMl <= 0f ? 0f : Mathf.Clamp01(currentMl / capacityMl);
    public float FillPercent => FillRatio;
    public bool IsFull => currentMl >= capacityMl - 0.001f;
    public bool IsEmpty => currentMl <= 0.001f;
    public bool HasLiquidVisual => liquidVisual != null;
    public bool ClampVisualHeightToCollider => clampVisualHeightToCollider;
    public float VisualMaxHeightLocal => GetEffectiveFullHeightLocal();
    public Vector3 FillAxisLocal => GetFillAxisLocal();

    private bool wasEmpty = true;
    private bool wasFull = false;
    private float spilledMl;
    private Transform overflowSpillVisual;
    private Renderer overflowSpillRenderer;

    private void Awake()
    {
        if (liquidVisual == null)
            CreateDefaultLiquidVisual();

        UpdateLiquidVisual();
        UpdateStateEvents();
    }

    private void OnValidate()
    {
        capacityMl = Mathf.Max(1f, capacityMl);
        currentMl = Mathf.Clamp(currentMl, 0f, capacityMl);
        bottomLocalY = Mathf.Max(0f, bottomLocalY);
        fullHeightLocal = Mathf.Max(0.000001f, fullHeightLocal);
        diameterXLocal = Mathf.Max(0.000001f, diameterXLocal);
        diameterZLocal = Mathf.Max(0.000001f, diameterZLocal);
        fillAxisLocal = GetFillAxisLocal();
        visualRimPaddingPercent = Mathf.Clamp01(visualRimPaddingPercent);
        overflowSpillDiameterPerSqrtMl = Mathf.Max(0f, overflowSpillDiameterPerSqrtMl);
        maxOverflowSpillDiameter = Mathf.Max(0.001f, maxOverflowSpillDiameter);
        overflowSpillThickness = Mathf.Max(0.0001f, overflowSpillThickness);
        spillTiltAngle = Mathf.Clamp(spillTiltAngle, 0f, 180f);
        spillRateMlPerSecond = Mathf.Max(0f, spillRateMlPerSecond);

        if (liquidVisual != null)
            UpdateLiquidVisual();
    }

    private void Update()
    {
        if (!enableTiltSpill || IsEmpty)
            return;

        Vector3 containerUp = mouthDirection != null
            ? mouthDirection.up
            : transform.TransformDirection(GetFillAxisLocal());
        float angle = Vector3.Angle(containerUp, Vector3.up);
        if (angle >= spillTiltAngle)
        {
            float spilledAmount = Mathf.Min(currentMl, spillRateMlPerSecond * Time.deltaTime);
            RemoveLiquid(spilledAmount);
            ShowSpill(spilledAmount);
        }
    }

    public float AddLiquid(float amountMl, LiquidData incomingLiquid)
    {
        if (amountMl <= 0f)
            return 0f;

        if (IsFull)
        {
            ShowSpill(amountMl);
            return amountMl;
        }

        if (currentLiquid == null && incomingLiquid != null)
            currentLiquid = incomingLiquid;

        float availableCapacity = Mathf.Max(0f, capacityMl - currentMl);
        float acceptedAmount = Mathf.Min(amountMl, availableCapacity);
        float overflowAmount = Mathf.Max(0f, amountMl - acceptedAmount);

        SetLiquidAmountInternal(currentMl + acceptedAmount);
        ShowSpill(overflowAmount);
        return overflowAmount;
    }

    public float AddLiquid(float amountMl)
    {
        return AddLiquid(amountMl, currentLiquid);
    }

    public bool CanAddLiquid(float amountMl)
    {
        return amountMl > 0f && AvailableCapacityMl > 0f;
    }

    public void RemoveLiquid(float amountMl)
    {
        if (amountMl <= 0f)
            return;

        SetLiquidAmountInternal(currentMl - amountMl);

        if (IsEmpty)
            currentLiquid = null;
    }

    public void EmptyLiquid()
    {
        currentLiquid = null;
        SetLiquidAmountInternal(0f);
    }

    public void ClearLiquid()
    {
        EmptyLiquid();
    }

    public void SetLiquidAmount(float amountMl)
    {
        SetLiquidAmountInternal(amountMl);
    }

    public void SetLiquid(float amountMl, LiquidData liquidType)
    {
        currentLiquid = liquidType;
        SetLiquidAmountInternal(amountMl);
    }

    public float TryTransferTo(LiquidContainer targetContainer, float amountMl)
    {
        if (targetContainer == null || targetContainer == this || amountMl <= 0f || IsEmpty)
            return 0f;

        float transferAmount = Mathf.Min(amountMl, currentMl, targetContainer.AvailableCapacityMl);
        if (transferAmount <= 0f)
            return 0f;

        float overflowAmount = targetContainer.AddLiquid(transferAmount, currentLiquid);
        float acceptedAmount = Mathf.Max(0f, transferAmount - overflowAmount);
        RemoveLiquid(acceptedAmount);
        return acceptedAmount;
    }

    [ContextMenu("Create Default Liquid Visual")]
    public void CreateDefaultLiquidVisual()
    {
        GameObject liquidObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        liquidObject.name = "LiquidVisual";
        liquidObject.transform.SetParent(transform, false);

        Collider col = liquidObject.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying)
                Destroy(col);
            else
                DestroyImmediate(col);
        }

        liquidVisual = liquidObject.transform;
        liquidRenderer = liquidObject.GetComponent<Renderer>();

        if (liquidMaterial == null)
            liquidMaterial = CreateTransparentMaterial();

        if (liquidRenderer != null)
            liquidRenderer.sharedMaterial = liquidMaterial;

        UpdateLiquidVisual();
    }

    [ContextMenu("Create Default Fill Zone")]
    public void CreateDefaultFillZone()
    {
        Transform existing = transform.Find("FillZone");
        GameObject zoneObject = existing != null ? existing.gameObject : new GameObject("FillZone");
        zoneObject.transform.SetParent(transform, false);
        Vector3 fillAxis = GetFillAxisLocal();
        float effectiveFullHeight = GetEffectiveFullHeightLocal();
        float receiveOffset = Mathf.Max(diameterXLocal, diameterZLocal) * 0.5f;
        zoneObject.transform.localPosition = fillAxis * (bottomLocalY + effectiveFullHeight + receiveOffset);
        zoneObject.transform.localRotation = Quaternion.FromToRotation(Vector3.up, fillAxis);
        zoneObject.transform.localScale = Vector3.one;

        SphereCollider sphere = zoneObject.GetComponent<SphereCollider>();
        if (sphere == null)
            sphere = zoneObject.AddComponent<SphereCollider>();

        sphere.isTrigger = true;
        sphere.radius = Mathf.Max(diameterXLocal, diameterZLocal) * 0.65f;

        WasherFillZone fillZone = zoneObject.GetComponent<WasherFillZone>();
        if (fillZone == null)
            fillZone = zoneObject.AddComponent<WasherFillZone>();

        fillZone.SetTargetContainer(this);
    }

    private void SetLiquidAmountInternal(float amountMl)
    {
        currentMl = Mathf.Clamp(amountMl, 0f, capacityMl);
        if (IsEmpty)
            currentLiquid = null;

        UpdateLiquidVisual();
        onAmountChanged?.Invoke(currentMl);
        UpdateStateEvents();
    }

    private Material CreateTransparentMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = "Runtime_Water_Material";

        Color color = currentLiquid != null
            ? currentLiquid.liquidColor
            : new Color(0.25f, 0.65f, 1f, 0.45f);

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);

        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f);

        if (mat.HasProperty("_Blend"))
            mat.SetFloat("_Blend", 0f);

        if (mat.HasProperty("_SrcBlend"))
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);

        if (mat.HasProperty("_DstBlend"))
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);

        if (mat.HasProperty("_ZWrite"))
            mat.SetInt("_ZWrite", 0);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        return mat;
    }

    private void UpdateLiquidVisual()
    {
        if (liquidVisual == null)
            return;

        float t = Mathf.Clamp01(FillPercent);

        if (hideWhenEmpty)
            liquidVisual.gameObject.SetActive(t > 0.001f);

        float effectiveFullHeight = GetEffectiveFullHeightLocal();
        float emptyHeightLocal = Mathf.Max(0.000001f, effectiveFullHeight * 0.01f);
        float liquidHeight = Mathf.Lerp(emptyHeightLocal, effectiveFullHeight, t);
        Vector3 fillAxis = GetFillAxisLocal();

        liquidVisual.localRotation = Quaternion.FromToRotation(Vector3.up, fillAxis);
        liquidVisual.localScale = new Vector3(
            diameterXLocal,
            liquidHeight * 0.5f,
            diameterZLocal
        );

        liquidVisual.localPosition = fillAxis * (bottomLocalY + liquidHeight * 0.5f);

        if (liquidRenderer == null)
            liquidRenderer = liquidVisual.GetComponent<Renderer>();

        if (liquidRenderer == null)
            return;

        Material mat = Application.isPlaying ? liquidRenderer.material : liquidRenderer.sharedMaterial;
        if (mat == null)
            return;

        Color color = currentLiquid != null
            ? currentLiquid.liquidColor
            : new Color(0.25f, 0.65f, 1f, 0.45f);

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }

    private void ShowSpill(float amountMl)
    {
        if (!spillOverflowWhenFull || amountMl <= 0f)
            return;

        spilledMl += amountMl;

        if (overflowSpillVisual == null)
            CreateOverflowSpillVisual();

        if (overflowSpillVisual == null)
            return;

        overflowSpillVisual.position = FindSpillSurfacePosition();
        overflowSpillVisual.rotation = Quaternion.identity;

        float diameter = Mathf.Min(
            maxOverflowSpillDiameter,
            Mathf.Sqrt(Mathf.Max(0f, spilledMl)) * overflowSpillDiameterPerSqrtMl
        );

        overflowSpillVisual.localScale = new Vector3(diameter, overflowSpillThickness, diameter);
    }

    private void CreateOverflowSpillVisual()
    {
        GameObject spillObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        spillObject.name = $"{name}_OverflowSpill";

        Collider col = spillObject.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        overflowSpillVisual = spillObject.transform;
        overflowSpillRenderer = spillObject.GetComponent<Renderer>();

        if (overflowSpillRenderer != null)
            overflowSpillRenderer.material = CreateOverflowSpillMaterial();
    }

    private Material CreateOverflowSpillMaterial()
    {
        Material mat = liquidMaterial != null
            ? new Material(liquidMaterial)
            : CreateTransparentMaterial();

        mat.name = "Runtime_Overflow_Spill_Material";

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", overflowSpillColor);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", overflowSpillColor);

        return mat;
    }

    private Vector3 FindSpillSurfacePosition()
    {
        Vector3 fillTop = transform.TransformPoint(GetFillAxisLocal() * (bottomLocalY + GetEffectiveFullHeightLocal()));
        Vector3 origin = fillTop + Vector3.up * 0.05f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            return hit.point + Vector3.up * 0.002f;
        }

        return fillTop - Vector3.up * 0.05f;
    }

    private Vector3 GetFillAxisLocal()
    {
        if (fillAxisLocal.sqrMagnitude < 0.0001f)
            return Vector3.up;

        return fillAxisLocal.normalized;
    }

    private float GetEffectiveFullHeightLocal()
    {
        if (!clampVisualHeightToCollider)
            return fullHeightLocal;

        if (!TryGetColliderTopAlongFillAxis(out float colliderTop))
            return fullHeightLocal;

        float availableHeight = colliderTop - bottomLocalY;
        if (availableHeight <= 0f)
            return fullHeightLocal;

        float paddedHeight = availableHeight * (1f - visualRimPaddingPercent);
        return Mathf.Min(fullHeightLocal, Mathf.Max(0.000001f, paddedHeight));
    }

    private bool TryGetColliderTopAlongFillAxis(out float top)
    {
        Vector3 fillAxis = GetFillAxisLocal();
        top = float.NegativeInfinity;
        bool found = false;

        foreach (Collider col in GetComponents<Collider>())
        {
            if (col == null || col.isTrigger)
                continue;

            if (col is BoxCollider box)
            {
                Vector3 halfSize = box.size * 0.5f;
                Vector3 absAxis = new Vector3(Mathf.Abs(fillAxis.x), Mathf.Abs(fillAxis.y), Mathf.Abs(fillAxis.z));
                float center = Vector3.Dot(box.center, fillAxis);
                float extent =
                    absAxis.x * halfSize.x +
                    absAxis.y * halfSize.y +
                    absAxis.z * halfSize.z;

                top = Mathf.Max(top, center + extent);
                found = true;
            }
        }

        return found;
    }

    private void UpdateStateEvents()
    {
        bool empty = IsEmpty;
        bool full = IsFull;

        if (empty && !wasEmpty)
            onBecameEmpty?.Invoke();

        if (full && !wasFull)
            onBecameFull?.Invoke();

        wasEmpty = empty;
        wasFull = full;
    }
}
