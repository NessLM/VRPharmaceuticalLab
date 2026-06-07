using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// Script-only fake liquid for VR lab containers. Focus-pour version: no puddle/residue visuals.
/// Core idea:
/// - The glass/container may have ugly imported rotation/scale.
/// - Liquid is generated inside a clean child space called LiquidSpace.
/// - LiquidSpace local Y is always the container's fill direction.
/// - The liquid volume is a procedural cylinder clipped by a gravity/world-up plane.
/// This avoids the common bug where water becomes a long slab when a rotated/scaled model is tilted.
/// </summary>
public class LiquidContainer : MonoBehaviour
{
    [Header("Liquid Data")]
    [SerializeField] private LiquidData currentLiquid;
    [Tooltip("Used when volume is added but no liquid type is set yet.")]
    [SerializeField] private LiquidData defaultLiquid;

    [Header("Amount")]
    [SerializeField] private float capacityMl = 100f;
    [SerializeField] private float currentMl = 0f;

    [Header("Liquid Space / Visual")]
    [Tooltip("Clean child transform. Local Y must point from bottom to mouth/rim. Leave empty and use Create/Repair Liquid Space.")]
    [SerializeField] private Transform liquidSpace;

    [Tooltip("MeshRenderer object under LiquidSpace. Leave empty and the script will create one.")]
    [SerializeField] private Transform liquidVisual;

    [SerializeField] private Renderer liquidRenderer;
    [SerializeField] private Material liquidMaterial;
    [SerializeField] private bool hideWhenEmpty = true;

    [Header("Visual Size - Legacy Container Local")]
    [Tooltip("Offset from container pivot to internal liquid bottom, along Fill Axis Local.")]
    [SerializeField] private float bottomLocalY = 0.02f;

    [Tooltip("Internal usable height from bottom to rim, along Fill Axis Local.")]
    [SerializeField] private float fullHeightLocal = 0.16f;

    [Tooltip("Internal diameter on LiquidSpace local X.")]
    [SerializeField] private float diameterXLocal = 0.07f;

    [Tooltip("Internal diameter on LiquidSpace local Z.")]
    [SerializeField] private float diameterZLocal = 0.07f;

    [Tooltip("Container local direction from bottom to mouth/rim. Auto Detect usually finds this. Pyrex imported with X=-90 often uses Z.")]
    [SerializeField] private Vector3 fillAxisLocal = Vector3.up;

    [Header("Auto Setup")]
    [SerializeField] private bool autoDetectFillAxisOnAwake = true;

    [Tooltip("Recommended ON. Creates/repairs LiquidSpace from Fill Axis Local during Awake/Validate.")]
    [SerializeField] private bool autoCreateOrRepairLiquidSpace = true;

    [Tooltip("Optional. Auto-estimates size from visible mesh. Turn OFF if it makes water too large because it reads handles/extra children.")]
    [SerializeField] private bool autoFitVisualSizeOnAwake = false;

    [SerializeField, Range(0f, 0.25f)] private float autoBottomPaddingPercent = 0.04f;
    [SerializeField, Range(0.45f, 1f)] private float autoHeightPercent = 0.82f;
    [SerializeField, Range(0.25f, 1f)] private float autoDiameterPercent = 0.82f;

    [Header("Rim / Edge Tuning")]
    [Tooltip("1 = use exact diameter. 0.92 keeps water safely inside. 1.05 makes it reach closer to the visual wall.")]
    [SerializeField, Range(0.5f, 1.15f)] private float diameterMultiplier = 0.98f;

    [Tooltip("Shrinks the liquid height slightly under the rim to avoid ugly overlap.")]
    [SerializeField, Range(0f, 0.08f)] private float rimPaddingPercent = 0.015f;

    [Header("Gravity Physics")]
    [Tooltip("ON = surface stays world-level when the container tilts.")]
    [SerializeField] private bool worldAlignLiquidSurface = true;

    [Range(0f, 1f)]
    [Tooltip("1 = physically horizontal. 0 = follows container. Use 1 for final physics-looking behavior.")]
    [SerializeField] private float worldLevelStrength = 1f;

    [Tooltip("How quickly surface follows world-up. 12-20 is stable; lower gives lag.")]
    [SerializeField] private float surfaceFollowSpeed = 18f;

    [Header("Procedural Mesh")]
    [SerializeField, Range(16, 128)] private int meshAngularSegments = 72;
    [SerializeField, Range(1, 10)] private int meshHeightSegments = 3;
    [SerializeField, Range(256, 4096)] private int volumeSolveSamples = 2048;

    [Tooltip("When almost full, render a full volume instead of diagonal rim clipping.")]
    [SerializeField, Range(0.90f, 1f)] private float fullLooksFlatAt = 0.985f;

    [SerializeField] private float meshSkin = 0.000001f;

    [Header("Fake Slosh")]
    [SerializeField] private bool enableFakeSlosh = false;
    [SerializeField, Range(0f, 0.25f)] private float fakeSloshAmount = 0.035f;
    [SerializeField, Range(0f, 30f)] private float fakeSloshDamping = 12f;

    [Header("Open Container Rim Control")]
    [Tooltip("Recommended ON. The water surface is never allowed to visually pass the lowest open rim point when the container tilts.")]
    [SerializeField] private bool capSurfaceAtLowestRim = true;

    [Tooltip("Recommended ON. If the current amount cannot fit under the tilted rim, liquid is removed gradually as spill.")]
    [SerializeField] private bool autoSpillWhenPastRim = false;

    [Tooltip("Small tilts should not spill. 8-15 degrees is stable for VR hand jitter.")]
    [SerializeField, Range(0f, 45f)] private float rimSpillStartAngle = 10f;

    [Tooltip("How fast excess liquid drains when the surface passes the rim.")]
    [SerializeField] private float rimSpillRateMlPerSecond = 220f;

    [Tooltip("Rim sampling quality. Higher values make the lowest rim point more accurate.")]
    [SerializeField, Range(16, 256)] private int rimSampleSegments = 96;

    [Tooltip("Tiny inward margin so the clipped surface does not render exactly on the rim.")]
    [SerializeField] private float rimPlanePadding = 0.000001f;

    [Header("Tilt Spill")]
    [SerializeField] private bool enableTiltSpill = false;
    [SerializeField] private Transform mouthDirection;
    [SerializeField] private float spillTiltAngle = 92f;
    [SerializeField] private float spillRateMlPerSecond = 80f;

    [Header("Overflow Spill")]
    [SerializeField] private bool spillOverflowWhenFull = false;
    [SerializeField] private float overflowSpillDiameterPerSqrtMl = 0.01f;
    [SerializeField] private float maxOverflowSpillDiameter = 0.22f;
    [SerializeField] private float overflowSpillThickness = 0.001f;
    [SerializeField] private Color overflowSpillColor = new Color(0.25f, 0.65f, 1f, 0.35f);

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;

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
    public bool CanReceive(float amountMl) => amountMl > 0f && AvailableCapacityMl > 0f;
    public bool HasLiquidVisual => liquidVisual != null;
    public bool ClampVisualHeightToCollider => false;
    public float VisualMaxHeightLocal => GetEffectiveFullHeightLocal();
    public Vector3 FillAxisLocal => GetFillAxisLocal();

    private bool wasEmpty = true;
    private bool wasFull = false;
    private float spilledMl;
    private Transform overflowSpillVisual;
    private Renderer overflowSpillRenderer;

    private Mesh proceduralMesh;
    private readonly List<Vector3> sourceVertices = new List<Vector3>(1600);
    private readonly List<Vector2> sourceUvs = new List<Vector2>(1600);
    private readonly List<int> sourceTriangles = new List<int>(4800);

    private readonly List<Vector3> clippedVertices = new List<Vector3>(2200);
    private readonly List<Vector2> clippedUvs = new List<Vector2>(2200);
    private readonly List<int> clippedTriangles = new List<int>(6600);
    private readonly List<Vector3> capPoints = new List<Vector3>(768);
    private readonly List<float> solveDots = new List<float>(4096);

    private bool sourceDirty = true;
    private float cachedHeight;
    private float cachedDiameterX;
    private float cachedDiameterZ;

    private Vector3 smoothedSurfaceNormalLocal = Vector3.up;
    private Vector3 fakeSloshOffsetLocal = Vector3.zero;
    private Quaternion lastLiquidSpaceRotation;
    private bool surfaceStateInitialized;

    private void Reset()
    {
        autoDetectFillAxisOnAwake = true;
        autoCreateOrRepairLiquidSpace = true;
        worldAlignLiquidSurface = true;
        worldLevelStrength = 1f;
        AutoDetectFillAxisFromCurrentPose();
        CreateOrRepairLiquidSpace();
    }

    private void Awake()
    {
        if (autoDetectFillAxisOnAwake)
            AutoDetectFillAxisFromCurrentPose();

        if (autoFitVisualSizeOnAwake)
            AutoFitLiquidVisualToRenderers();

        if (autoCreateOrRepairLiquidSpace)
            CreateOrRepairLiquidSpace();

        ResolveVisualReferences();
        EnsureVisualRenderable();

        if (liquidSpace != null)
            lastLiquidSpaceRotation = liquidSpace.rotation;

        UpdateLiquidVisual(true);
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
        autoBottomPaddingPercent = Mathf.Clamp01(autoBottomPaddingPercent);
        autoHeightPercent = Mathf.Clamp(autoHeightPercent, 0.45f, 1f);
        autoDiameterPercent = Mathf.Clamp(autoDiameterPercent, 0.25f, 1f);
        diameterMultiplier = Mathf.Clamp(diameterMultiplier, 0.5f, 1.15f);
        rimPaddingPercent = Mathf.Clamp(rimPaddingPercent, 0f, 0.08f);
        worldLevelStrength = Mathf.Clamp01(worldLevelStrength);
        surfaceFollowSpeed = Mathf.Max(0.01f, surfaceFollowSpeed);
        meshAngularSegments = Mathf.Clamp(meshAngularSegments, 16, 128);
        meshHeightSegments = Mathf.Clamp(meshHeightSegments, 1, 10);
        volumeSolveSamples = Mathf.Clamp(volumeSolveSamples, 256, 4096);
        fullLooksFlatAt = Mathf.Clamp(fullLooksFlatAt, 0.90f, 1f);
        meshSkin = Mathf.Max(0.00000001f, meshSkin);
        fakeSloshDamping = Mathf.Max(0f, fakeSloshDamping);
        rimSpillStartAngle = Mathf.Clamp(rimSpillStartAngle, 0f, 45f);
        rimSpillRateMlPerSecond = Mathf.Max(0f, rimSpillRateMlPerSecond);
        rimSampleSegments = Mathf.Clamp(rimSampleSegments, 16, 256);
        rimPlanePadding = Mathf.Max(0.00000001f, rimPlanePadding);
        spillTiltAngle = Mathf.Clamp(spillTiltAngle, 0f, 180f);
        spillRateMlPerSecond = Mathf.Max(0f, spillRateMlPerSecond);
        maxOverflowSpillDiameter = Mathf.Max(0.001f, maxOverflowSpillDiameter);
        overflowSpillThickness = Mathf.Max(0.0001f, overflowSpillThickness);

        sourceDirty = true;

        if (!Application.isPlaying && autoCreateOrRepairLiquidSpace)
            CreateOrRepairLiquidSpace();

        if (liquidVisual != null)
            UpdateLiquidVisual(true);
    }

    private void Update()
    {
        UpdateOpenContainerRimSpill();

        if (!enableTiltSpill || IsEmpty)
            return;

        Vector3 upToMouth = mouthDirection != null
            ? mouthDirection.up
            : (liquidSpace != null ? liquidSpace.up : transform.TransformDirection(GetFillAxisLocal()));

        float angle = Vector3.Angle(upToMouth, Vector3.up);
        if (angle >= spillTiltAngle)
        {
            float amount = Mathf.Min(currentMl, spillRateMlPerSecond * Time.deltaTime);
            RemoveLiquid(amount);
            ShowSpill(amount);
        }
    }

    private void LateUpdate()
    {
        UpdateLiquidVisual(false);
        UpdateSpillPosition();
    }

    [ContextMenu("Auto Detect Fill Axis From Current Pose")]
    public void AutoDetectFillAxisFromCurrentPose()
    {
        Vector3 localUp = transform.InverseTransformDirection(Vector3.up);
        fillAxisLocal = ClosestSignedCardinal(localUp);
        sourceDirty = true;
    }

    [ContextMenu("Create/Repair Liquid Space")]
    public void CreateOrRepairLiquidSpace()
    {
        if (liquidSpace == null)
        {
            Transform found = transform.Find("LiquidSpace");
            if (found != null)
                liquidSpace = found;
        }

        if (liquidSpace == null)
        {
            GameObject spaceObject = new GameObject("LiquidSpace");
            spaceObject.transform.SetParent(transform, false);
            liquidSpace = spaceObject.transform;
        }

        Vector3 axis = GetFillAxisLocal();
        liquidSpace.localPosition = axis * bottomLocalY;
        liquidSpace.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
        liquidSpace.localScale = Vector3.one;

        if (liquidVisual == null)
        {
            Transform foundVisual = liquidSpace.Find("LiquidVisual");
            if (foundVisual != null)
                liquidVisual = foundVisual;
        }

        if (liquidVisual == null)
        {
            GameObject visualObject = new GameObject("LiquidVisual");
            visualObject.transform.SetParent(liquidSpace, false);
            liquidVisual = visualObject.transform;
        }

        liquidVisual.SetParent(liquidSpace, false);
        liquidVisual.localPosition = Vector3.zero;
        liquidVisual.localRotation = Quaternion.identity;
        liquidVisual.localScale = Vector3.one;

        sourceDirty = true;
    }

    [ContextMenu("Auto Fit Liquid Visual To Renderers")]
    public void AutoFitLiquidVisualToRenderers()
    {
        Vector3 axis = GetFillAxisLocal();
        BuildPerpendicularBasis(axis, out Vector3 basisX, out Vector3 basisZ);

        bool found = false;
        float minAxis = float.PositiveInfinity;
        float maxAxis = float.NegativeInfinity;
        float maxX = 0f;
        float maxZ = 0f;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (liquidVisual != null && renderer.transform.IsChildOf(liquidVisual))
                continue;

            if (liquidSpace != null && renderer.transform.IsChildOf(liquidSpace))
                continue;

            string lower = renderer.name.ToLowerInvariant();
            if (lower.Contains("liquid") || lower.Contains("water") || lower.Contains("fillzone") || lower.Contains("pourpoint") || lower.Contains("grabpoint") || lower.Contains("handle"))
                continue;

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                continue;

            Bounds b = filter.sharedMesh.bounds;
            for (int xi = 0; xi <= 1; xi++)
            for (int yi = 0; yi <= 1; yi++)
            for (int zi = 0; zi <= 1; zi++)
            {
                Vector3 meshPoint = new Vector3(
                    xi == 0 ? b.min.x : b.max.x,
                    yi == 0 ? b.min.y : b.max.y,
                    zi == 0 ? b.min.z : b.max.z
                );

                Vector3 worldPoint = filter.transform.TransformPoint(meshPoint);
                Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

                float along = Vector3.Dot(localPoint, axis);
                float px = Vector3.Dot(localPoint, basisX);
                float pz = Vector3.Dot(localPoint, basisZ);

                minAxis = Mathf.Min(minAxis, along);
                maxAxis = Mathf.Max(maxAxis, along);
                maxX = Mathf.Max(maxX, Mathf.Abs(px));
                maxZ = Mathf.Max(maxZ, Mathf.Abs(pz));
                found = true;
            }
        }

        if (!found)
        {
            Debug.LogWarning($"{name}: Auto Fit failed. Keep manual visual size.", this);
            return;
        }

        float rawHeight = Mathf.Max(0.000001f, maxAxis - minAxis);
        bottomLocalY = minAxis + rawHeight * autoBottomPaddingPercent;
        fullHeightLocal = rawHeight * autoHeightPercent;
        diameterXLocal = Mathf.Max(0.000001f, maxX * 2f * autoDiameterPercent);
        diameterZLocal = Mathf.Max(0.000001f, maxZ * 2f * autoDiameterPercent);

        if (autoCreateOrRepairLiquidSpace)
            CreateOrRepairLiquidSpace();

        sourceDirty = true;
        UpdateLiquidVisual(true);
    }

    [ContextMenu("Force Refresh Liquid Visual")]
    public void ForceRefreshLiquidVisual()
    {
        sourceDirty = true;
        surfaceStateInitialized = false;
        UpdateLiquidVisual(true);
    }

    [ContextMenu("Fill 50 Percent Test")]
    public void FillHalfTest()
    {
        SetLiquidAmount(capacityMl * 0.5f);
    }

    [ContextMenu("Apply Pyrex / Beaker Focus Pour Preset")]
    public void ApplyPyrexSafePreset()
    {
        // Pyrex/beaker is wide. Do not use the defensive 0.72-0.90 value here.
        // The liquid is allowed to visually fill the bowl, while LiquidPourer handles actual draining/transfer.
        diameterMultiplier = 1.04f;
        rimPaddingPercent = 0.012f;
        fullLooksFlatAt = 0.985f;
        capSurfaceAtLowestRim = false;
        autoSpillWhenPastRim = false;
        spillOverflowWhenFull = false;
        enableTiltSpill = false;
        enableFakeSlosh = false;
        rimSpillStartAngle = 8f;
        rimSpillRateMlPerSecond = 0f;
        meshAngularSegments = 80;
        meshHeightSegments = 4;
        volumeSolveSamples = 2048;

        // If the previous diameter was tiny compared to the usable height, repair it.
        // This specifically fixes the pyrex visual becoming a small blue coin in the middle.
        float h = Mathf.Max(0.000001f, fullHeightLocal);
        diameterXLocal = Mathf.Max(diameterXLocal, h * 1.25f);
        diameterZLocal = Mathf.Max(diameterZLocal, h * 1.25f);

        sourceDirty = true;
        ForceRefreshLiquidVisual();
    }

    [ContextMenu("Apply Tall Measuring Glass Focus Pour Preset")]
    public void ApplyTallMeasuringGlassPreset()
    {
        diameterMultiplier = 0.98f;
        rimPaddingPercent = 0.012f;
        fullLooksFlatAt = 0.985f;
        capSurfaceAtLowestRim = false;
        autoSpillWhenPastRim = false;
        spillOverflowWhenFull = false;
        enableTiltSpill = false;
        enableFakeSlosh = false;
        rimSpillStartAngle = 10f;
        rimSpillRateMlPerSecond = 0f;
        meshAngularSegments = 72;
        meshHeightSegments = 3;
        volumeSolveSamples = 2048;
        sourceDirty = true;
        ForceRefreshLiquidVisual();
    }

    public float AddLiquid(float amountMl, LiquidData incomingLiquid)
    {
        if (amountMl <= 0f)
            return 0f;

        if (IsFull)
            return amountMl;

        if (currentLiquid == null && incomingLiquid != null)
            currentLiquid = incomingLiquid;

        if (currentLiquid == null && incomingLiquid == null && defaultLiquid != null)
            currentLiquid = defaultLiquid;

        float accepted = Mathf.Min(amountMl, AvailableCapacityMl);
        float overflow = Mathf.Max(0f, amountMl - accepted);
        SetLiquidAmountInternal(currentMl + accepted);
        // Focus-pour mode: overflow is reported to the caller but no puddle/residue object is created.
        return overflow;
    }

    public float AddLiquid(float amountMl)
    {
        return AddLiquid(amountMl, currentLiquid != null ? currentLiquid : defaultLiquid);
    }

    public bool CanAddLiquid(float amountMl)
    {
        return CanReceive(amountMl);
    }

    public LiquidData GetEffectiveLiquid()
    {
        return currentLiquid != null ? currentLiquid : defaultLiquid;
    }

    public void EnsureLiquidTypeForPouring()
    {
        if (!IsEmpty && currentLiquid == null && defaultLiquid != null)
            currentLiquid = defaultLiquid;
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

        float transfer = Mathf.Min(amountMl, currentMl, targetContainer.AvailableCapacityMl);
        if (transfer <= 0f)
            return 0f;

        float overflow = targetContainer.AddLiquid(transfer, currentLiquid);
        float accepted = Mathf.Max(0f, transfer - overflow);
        RemoveLiquid(accepted);
        return accepted;
    }

    private void SetLiquidAmountInternal(float amountMl)
    {
        currentMl = Mathf.Clamp(amountMl, 0f, capacityMl);
        if (IsEmpty)
            currentLiquid = null;

        UpdateLiquidVisual(true);
        onAmountChanged?.Invoke(currentMl);
        UpdateStateEvents();
    }

    private void ResolveVisualReferences()
    {
        if (liquidSpace == null)
        {
            Transform foundSpace = transform.Find("LiquidSpace");
            if (foundSpace != null)
                liquidSpace = foundSpace;
        }

        if (liquidVisual == null && liquidSpace != null)
        {
            Transform foundVisual = liquidSpace.Find("LiquidVisual");
            if (foundVisual != null)
                liquidVisual = foundVisual;
        }

        if (liquidVisual == null)
        {
            Transform legacy = transform.Find("LiquidVisual");
            if (legacy != null)
                liquidVisual = legacy;
        }

        if (liquidRenderer == null && liquidVisual != null)
            liquidRenderer = liquidVisual.GetComponent<Renderer>();
    }

    private void EnsureVisualRenderable()
    {
        if (liquidSpace == null || liquidVisual == null)
            CreateOrRepairLiquidSpace();

        if (liquidVisual == null)
            return;

        MeshFilter filter = liquidVisual.GetComponent<MeshFilter>();
        if (filter == null)
            filter = liquidVisual.gameObject.AddComponent<MeshFilter>();

        MeshRenderer renderer = liquidVisual.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = liquidVisual.gameObject.AddComponent<MeshRenderer>();

        Collider col = liquidVisual.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying)
                Destroy(col);
            else
                DestroyImmediate(col);
        }

        if (liquidMaterial == null)
            liquidMaterial = CreateTransparentMaterial();

        renderer.sharedMaterial = liquidMaterial;
        liquidRenderer = renderer;
    }

    private void UpdateLiquidVisual(bool forceRebuild)
    {
        ResolveVisualReferences();
        if (liquidSpace == null || liquidVisual == null)
            return;

        EnsureVisualRenderable();

        float t = FillRatio;
        bool show = !hideWhenEmpty || t > 0.001f;
        liquidVisual.gameObject.SetActive(show);
        if (!show)
            return;

        if (proceduralMesh == null)
        {
            proceduralMesh = new Mesh();
            proceduralMesh.name = "Runtime_CleanSpace_Liquid";
            proceduralMesh.MarkDynamic();
        }

        MeshFilter filter = liquidVisual.GetComponent<MeshFilter>();
        filter.sharedMesh = proceduralMesh;

        liquidVisual.localPosition = Vector3.zero;
        liquidVisual.localRotation = Quaternion.identity;
        liquidVisual.localScale = Vector3.one;

        float h = GetEffectiveFullHeightLocal();
        float dx = Mathf.Max(0.000001f, diameterXLocal * diameterMultiplier);
        float dz = Mathf.Max(0.000001f, diameterZLocal * diameterMultiplier);

        if (forceRebuild || sourceDirty || ShapeChanged(h, dx, dz))
            BuildSourceCylinder(h, dx, dz);

        if (!worldAlignLiquidSurface)
        {
            float visibleHeight = Mathf.Max(meshSkin, Mathf.Lerp(h * 0.01f, h, t));
            BuildSimpleFilledCylinder(visibleHeight, dx, dz);
        }
        else
        {
            Vector3 normalLocal = GetSurfaceNormalInLiquidSpace();
            bool useRimCapNow = ShouldUseRimCap() && !IsActivelyPouring();

            if (t >= fullLooksFlatAt && !useRimCapNow)
            {
                BuildFullVolumeMesh();
            }
            else
            {
                float plane = t >= fullLooksFlatAt
                    ? GetLowestRimPlaneConstant(normalLocal, h, dx, dz)
                    : SolvePlaneConstantForFill(t, normalLocal, h, dx, dz);

                if (useRimCapNow)
                {
                    float rimPlane = GetLowestRimPlaneConstant(normalLocal, h, dx, dz);
                    if (plane > rimPlane)
                        plane = rimPlane;
                }

                ClipMeshByPlane(normalLocal, plane);
            }
        }

        ApplyLiquidMaterialColor();
    }

    private bool ShapeChanged(float h, float dx, float dz)
    {
        return Mathf.Abs(cachedHeight - h) > Mathf.Max(0.0000001f, h * 0.0001f)
            || Mathf.Abs(cachedDiameterX - dx) > Mathf.Max(0.0000001f, dx * 0.0001f)
            || Mathf.Abs(cachedDiameterZ - dz) > Mathf.Max(0.0000001f, dz * 0.0001f);
    }

    private float GetEffectiveFullHeightLocal()
    {
        return Mathf.Max(0.000001f, fullHeightLocal * (1f - rimPaddingPercent));
    }

    private void BuildSimpleFilledCylinder(float h, float dx, float dz)
    {
        BuildSourceCylinder(h, dx, dz, true);
        BuildFullVolumeMesh();
    }

    private void BuildSourceCylinder(float h, float dx, float dz, bool temporary = false)
    {
        sourceVertices.Clear();
        sourceUvs.Clear();
        sourceTriangles.Clear();

        float bottom = meshSkin;
        float top = Mathf.Max(bottom + meshSkin, h - meshSkin);
        float rx = Mathf.Max(0.000001f, dx * 0.5f);
        float rz = Mathf.Max(0.000001f, dz * 0.5f);
        int angular = Mathf.Max(16, meshAngularSegments);
        int heightSteps = Mathf.Max(1, meshHeightSegments);

        int[,] ring = new int[heightSteps + 1, angular];

        for (int yIndex = 0; yIndex <= heightSteps; yIndex++)
        {
            float yn = yIndex / (float)heightSteps;
            float y = Mathf.Lerp(bottom, top, yn);

            for (int s = 0; s < angular; s++)
            {
                float a = s / (float)angular * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * rx, y, Mathf.Sin(a) * rz);
                ring[yIndex, s] = AddSourceVertex(p, new Vector2(s / (float)angular, yn));
            }
        }

        int bottomCenter = AddSourceVertex(new Vector3(0f, bottom, 0f), new Vector2(0.5f, 0.5f));
        int topCenter = AddSourceVertex(new Vector3(0f, top, 0f), new Vector2(0.5f, 0.5f));

        for (int yIndex = 0; yIndex < heightSteps; yIndex++)
        {
            for (int s = 0; s < angular; s++)
            {
                int next = (s + 1) % angular;
                AddSourceQuad(ring[yIndex, s], ring[yIndex + 1, s], ring[yIndex + 1, next], ring[yIndex, next]);
            }
        }

        for (int s = 0; s < angular; s++)
        {
            int next = (s + 1) % angular;
            AddSourceTriangle(bottomCenter, ring[0, next], ring[0, s]);
            AddSourceTriangle(topCenter, ring[heightSteps, s], ring[heightSteps, next]);
        }

        if (!temporary)
        {
            cachedHeight = h;
            cachedDiameterX = dx;
            cachedDiameterZ = dz;
            sourceDirty = false;
        }
    }

    private int AddSourceVertex(Vector3 p, Vector2 uv)
    {
        int i = sourceVertices.Count;
        sourceVertices.Add(p);
        sourceUvs.Add(uv);
        return i;
    }

    private void AddSourceTriangle(int a, int b, int c)
    {
        sourceTriangles.Add(a);
        sourceTriangles.Add(b);
        sourceTriangles.Add(c);
    }

    private void AddSourceQuad(int a, int b, int c, int d)
    {
        AddSourceTriangle(a, b, c);
        AddSourceTriangle(a, c, d);
    }

    private void BuildFullVolumeMesh()
    {
        proceduralMesh.Clear();
        proceduralMesh.SetVertices(sourceVertices);
        proceduralMesh.SetUVs(0, sourceUvs);
        proceduralMesh.SetTriangles(sourceTriangles, 0, true);
        proceduralMesh.RecalculateNormals();
        proceduralMesh.RecalculateBounds();
    }

    private void UpdateOpenContainerRimSpill()
    {
        if (!autoSpillWhenPastRim || !capSurfaceAtLowestRim || IsEmpty || !worldAlignLiquidSurface || liquidSpace == null)
            return;

        if (!ShouldUseRimCap())
            return;

        float h = GetEffectiveFullHeightLocal();
        float dx = Mathf.Max(0.000001f, diameterXLocal * diameterMultiplier);
        float dz = Mathf.Max(0.000001f, diameterZLocal * diameterMultiplier);
        Vector3 normalLocal = GetImmediateSurfaceNormalInLiquidSpace();

        float wantedPlane = SolvePlaneConstantForFill(FillRatio, normalLocal, h, dx, dz);
        float rimPlane = GetLowestRimPlaneConstant(normalLocal, h, dx, dz);

        if (wantedPlane <= rimPlane)
            return;

        float maxStableRatio = EstimateFillRatioBelowPlane(normalLocal, rimPlane, h, dx, dz);
        float targetMl = Mathf.Clamp01(maxStableRatio) * capacityMl;
        float excessMl = currentMl - targetMl;

        if (excessMl <= 0.001f)
            return;

        float spilledNow = Mathf.Min(excessMl, rimSpillRateMlPerSecond * Time.deltaTime);
        if (spilledNow <= 0f)
            return;

        // Do not call RemoveLiquid here because we already know this is an overflow spill.
        SetLiquidAmountInternal(currentMl - spilledNow);
        ShowSpill(spilledNow);
    }


    private bool IsActivelyPouring()
    {
        LiquidPourer pourer = GetComponent<LiquidPourer>();
        return pourer != null && pourer.IsPouring;
    }

    private bool ShouldUseRimCap()
    {
        if (!capSurfaceAtLowestRim || liquidSpace == null)
            return false;

        float tiltAngle = Vector3.Angle(liquidSpace.up, Vector3.up);
        return tiltAngle >= rimSpillStartAngle;
    }

    private float GetLowestRimPlaneConstant(Vector3 normalLocal, float h, float dx, float dz)
    {
        float rx = Mathf.Max(0.000001f, dx * 0.5f);
        float rz = Mathf.Max(0.000001f, dz * 0.5f);
        int segments = Mathf.Max(16, rimSampleSegments);

        float lowest = float.PositiveInfinity;
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 rimPoint = new Vector3(Mathf.Cos(a) * rx, h - meshSkin, Mathf.Sin(a) * rz);
            float d = Vector3.Dot(normalLocal, rimPoint);
            if (d < lowest)
                lowest = d;
        }

        return lowest - rimPlanePadding;
    }

    private float EstimateFillRatioBelowPlane(Vector3 normalLocal, float planeConstant, float h, float dx, float dz)
    {
        float rx = Mathf.Max(0.000001f, dx * 0.5f);
        float rz = Mathf.Max(0.000001f, dz * 0.5f);
        int samples = Mathf.Max(256, volumeSolveSamples);
        int inside = 0;

        for (int i = 0; i < samples; i++)
        {
            float u1 = Frac((i + 0.5f) * 0.61803398875f);
            float u2 = Frac((i + 0.5f) * 0.75487766625f);
            float u3 = Frac((i + 0.5f) * 0.56984029099f);

            float y = Mathf.Lerp(meshSkin, Mathf.Max(meshSkin * 2f, h - meshSkin), u1);
            float r = Mathf.Sqrt(u2);
            float theta = u3 * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(theta) * rx * r, y, Mathf.Sin(theta) * rz * r);

            if (Vector3.Dot(normalLocal, p) <= planeConstant)
                inside++;
        }

        return inside / (float)samples;
    }

    private Vector3 GetImmediateSurfaceNormalInLiquidSpace()
    {
        if (liquidSpace == null)
            return Vector3.up;

        Vector3 target = GetScaleAwareWorldUpInLiquidSpace();
        return Vector3.Slerp(Vector3.up, target, worldLevelStrength).normalized;
    }

    private Vector3 GetSurfaceNormalInLiquidSpace()
    {
        if (liquidSpace == null)
            return Vector3.up;

        Vector3 target = GetScaleAwareWorldUpInLiquidSpace();
        target = Vector3.Slerp(Vector3.up, target, worldLevelStrength).normalized;

        if (!Application.isPlaying)
            return target;

        if (!surfaceStateInitialized)
        {
            smoothedSurfaceNormalLocal = target;
            fakeSloshOffsetLocal = Vector3.zero;
            lastLiquidSpaceRotation = liquidSpace.rotation;
            surfaceStateInitialized = true;
            return target;
        }

        if (enableFakeSlosh)
        {
            Quaternion delta = liquidSpace.rotation * Quaternion.Inverse(lastLiquidSpaceRotation);
            delta.ToAngleAxis(out float angle, out Vector3 worldAxis);
            if (angle > 180f)
                angle -= 360f;

            if (!float.IsNaN(worldAxis.x) && worldAxis.sqrMagnitude > 0.0001f)
            {
                Vector3 localAxis = liquidSpace.InverseTransformDirection(worldAxis.normalized);
                Vector3 impulse = Vector3.Cross(localAxis, target) * (angle * Mathf.Deg2Rad * fakeSloshAmount);
                fakeSloshOffsetLocal += impulse;
            }

            fakeSloshOffsetLocal = Vector3.Lerp(fakeSloshOffsetLocal, Vector3.zero, Time.deltaTime * fakeSloshDamping);
        }

        Vector3 sloshTarget = (target + fakeSloshOffsetLocal).normalized;
        smoothedSurfaceNormalLocal = Vector3.Slerp(smoothedSurfaceNormalLocal, sloshTarget, Time.deltaTime * surfaceFollowSpeed).normalized;
        lastLiquidSpaceRotation = liquidSpace.rotation;

        return smoothedSurfaceNormalLocal.sqrMagnitude > 0.0001f ? smoothedSurfaceNormalLocal : target;
    }

    private Vector3 GetScaleAwareWorldUpInLiquidSpace()
    {
        Vector3 local = Quaternion.Inverse(liquidSpace.rotation) * Vector3.up;
        Vector3 s = liquidSpace.lossyScale;
        Vector3 inv = new Vector3(
            Mathf.Abs(s.x) > 1e-5f ? 1f / s.x : 0f,
            Mathf.Abs(s.y) > 1e-5f ? 1f / s.y : 0f,
            Mathf.Abs(s.z) > 1e-5f ? 1f / s.z : 0f
        );

        Vector3 corrected = Vector3.Scale(local, inv);
        return corrected.sqrMagnitude > 1e-8f ? corrected.normalized : local.normalized;
    }

    private float SolvePlaneConstantForFill(float fillRatio, Vector3 normal, float h, float dx, float dz)
    {
        fillRatio = Mathf.Clamp01(fillRatio);
        float rx = Mathf.Max(0.000001f, dx * 0.5f);
        float rz = Mathf.Max(0.000001f, dz * 0.5f);

        solveDots.Clear();
        int samples = Mathf.Max(256, volumeSolveSamples);
        float minDot = float.PositiveInfinity;
        float maxDot = float.NegativeInfinity;

        for (int i = 0; i < samples; i++)
        {
            float u1 = Frac((i + 0.5f) * 0.61803398875f);
            float u2 = Frac((i + 0.5f) * 0.75487766625f);
            float u3 = Frac((i + 0.5f) * 0.56984029099f);

            float y = Mathf.Lerp(meshSkin, Mathf.Max(meshSkin * 2f, h - meshSkin), u1);
            float r = Mathf.Sqrt(u2);
            float theta = u3 * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(theta) * rx * r, y, Mathf.Sin(theta) * rz * r);
            float d = Vector3.Dot(normal, p);
            solveDots.Add(d);
            minDot = Mathf.Min(minDot, d);
            maxDot = Mathf.Max(maxDot, d);
        }

        if (fillRatio <= 0.001f)
            return minDot - h;

        if (fillRatio >= fullLooksFlatAt)
            return maxDot + h;

        float lo = minDot - h;
        float hi = maxDot + h;

        for (int iter = 0; iter < 28; iter++)
        {
            float mid = (lo + hi) * 0.5f;
            int inside = 0;
            for (int i = 0; i < solveDots.Count; i++)
            {
                if (solveDots[i] <= mid)
                    inside++;
            }

            float ratio = inside / (float)solveDots.Count;
            if (ratio < fillRatio)
                lo = mid;
            else
                hi = mid;
        }

        return (lo + hi) * 0.5f;
    }

    private float Frac(float v)
    {
        return v - Mathf.Floor(v);
    }

    private void ClipMeshByPlane(Vector3 normal, float planeConstant)
    {
        clippedVertices.Clear();
        clippedUvs.Clear();
        clippedTriangles.Clear();
        capPoints.Clear();

        for (int i = 0; i < sourceTriangles.Count; i += 3)
        {
            int ia = sourceTriangles[i];
            int ib = sourceTriangles[i + 1];
            int ic = sourceTriangles[i + 2];

            ClipTriangle(
                sourceVertices[ia], sourceVertices[ib], sourceVertices[ic],
                sourceUvs[ia], sourceUvs[ib], sourceUvs[ic],
                normal, planeConstant
            );
        }

        BuildClipCap(normal);

        proceduralMesh.Clear();
        proceduralMesh.SetVertices(clippedVertices);
        proceduralMesh.SetUVs(0, clippedUvs);
        proceduralMesh.SetTriangles(clippedTriangles, 0, true);
        proceduralMesh.RecalculateNormals();
        proceduralMesh.RecalculateBounds();
    }

    private void ClipTriangle(Vector3 a, Vector3 b, Vector3 c, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector3 normal, float planeConstant)
    {
        ClipVertex va = new ClipVertex(a, uva, planeConstant - Vector3.Dot(normal, a));
        ClipVertex vb = new ClipVertex(b, uvb, planeConstant - Vector3.Dot(normal, b));
        ClipVertex vc = new ClipVertex(c, uvc, planeConstant - Vector3.Dot(normal, c));

        bool ina = va.distance >= -0.0000001f;
        bool inb = vb.distance >= -0.0000001f;
        bool inc = vc.distance >= -0.0000001f;
        int count = (ina ? 1 : 0) + (inb ? 1 : 0) + (inc ? 1 : 0);

        if (count == 3)
        {
            AddClippedTriangle(va.position, vb.position, vc.position, va.uv, vb.uv, vc.uv);
            return;
        }

        if (count == 0)
            return;

        if (count == 1)
        {
            ClipVertex inside = ina ? va : (inb ? vb : vc);
            ClipVertex out1;
            ClipVertex out2;

            if (ina)
            {
                out1 = vb;
                out2 = vc;
            }
            else if (inb)
            {
                out1 = vc;
                out2 = va;
            }
            else
            {
                out1 = va;
                out2 = vb;
            }

            ClipVertex p1 = Intersect(inside, out1);
            ClipVertex p2 = Intersect(inside, out2);
            AddCapPoint(p1.position);
            AddCapPoint(p2.position);
            AddClippedTriangle(inside.position, p1.position, p2.position, inside.uv, p1.uv, p2.uv);
            return;
        }

        ClipVertex in1;
        ClipVertex in2;
        ClipVertex outside;

        if (!ina)
        {
            outside = va;
            in1 = vb;
            in2 = vc;
        }
        else if (!inb)
        {
            outside = vb;
            in1 = vc;
            in2 = va;
        }
        else
        {
            outside = vc;
            in1 = va;
            in2 = vb;
        }

        ClipVertex p1b = Intersect(in1, outside);
        ClipVertex p2b = Intersect(in2, outside);
        AddCapPoint(p1b.position);
        AddCapPoint(p2b.position);

        AddClippedTriangle(in1.position, in2.position, p2b.position, in1.uv, in2.uv, p2b.uv);
        AddClippedTriangle(in1.position, p2b.position, p1b.position, in1.uv, p2b.uv, p1b.uv);
    }

    private ClipVertex Intersect(ClipVertex inside, ClipVertex outside)
    {
        float t = inside.distance / Mathf.Max(0.0000001f, inside.distance - outside.distance);
        t = Mathf.Clamp01(t);
        return new ClipVertex(
            Vector3.Lerp(inside.position, outside.position, t),
            Vector2.Lerp(inside.uv, outside.uv, t),
            0f
        );
    }

    private void AddClippedTriangle(Vector3 a, Vector3 b, Vector3 c, Vector2 uva, Vector2 uvb, Vector2 uvc)
    {
        int ia = AddClippedVertex(a, uva);
        int ib = AddClippedVertex(b, uvb);
        int ic = AddClippedVertex(c, uvc);
        clippedTriangles.Add(ia);
        clippedTriangles.Add(ib);
        clippedTriangles.Add(ic);
    }

    private int AddClippedVertex(Vector3 p, Vector2 uv)
    {
        int i = clippedVertices.Count;
        clippedVertices.Add(p);
        clippedUvs.Add(uv);
        return i;
    }

    private void AddCapPoint(Vector3 p)
    {
        float eps = Mathf.Max(0.000001f, Mathf.Max(diameterXLocal, diameterZLocal, fullHeightLocal) * 0.0004f);
        float epsSqr = eps * eps;
        for (int i = 0; i < capPoints.Count; i++)
        {
            if ((capPoints[i] - p).sqrMagnitude <= epsSqr)
                return;
        }
        capPoints.Add(p);
    }

    private void BuildClipCap(Vector3 normal)
    {
        if (capPoints.Count < 3)
            return;

        Vector3 center = Vector3.zero;
        for (int i = 0; i < capPoints.Count; i++)
            center += capPoints[i];
        center /= capPoints.Count;

        Vector3 reference = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.95f ? Vector3.up : Vector3.right;
        Vector3 u = Vector3.Cross(reference, normal).normalized;
        Vector3 v = Vector3.Cross(normal, u).normalized;

        capPoints.Sort((a, b) =>
        {
            Vector3 da = a - center;
            Vector3 db = b - center;
            float aa = Mathf.Atan2(Vector3.Dot(da, v), Vector3.Dot(da, u));
            float ab = Mathf.Atan2(Vector3.Dot(db, v), Vector3.Dot(db, u));
            return aa.CompareTo(ab);
        });

        int centerIndex = AddClippedVertex(center, new Vector2(0.5f, 0.5f));
        for (int i = 0; i < capPoints.Count; i++)
        {
            int next = (i + 1) % capPoints.Count;
            int a = AddClippedVertex(capPoints[i], new Vector2(0.5f + Vector3.Dot(capPoints[i] - center, u), 0.5f + Vector3.Dot(capPoints[i] - center, v)));
            int b = AddClippedVertex(capPoints[next], new Vector2(0.5f + Vector3.Dot(capPoints[next] - center, u), 0.5f + Vector3.Dot(capPoints[next] - center, v)));
            clippedTriangles.Add(centerIndex);
            clippedTriangles.Add(a);
            clippedTriangles.Add(b);
        }
    }

    private struct ClipVertex
    {
        public Vector3 position;
        public Vector2 uv;
        public float distance;

        public ClipVertex(Vector3 position, Vector2 uv, float distance)
        {
            this.position = position;
            this.uv = uv;
            this.distance = distance;
        }
    }

    private void ApplyLiquidMaterialColor()
    {
        if (liquidRenderer == null && liquidVisual != null)
            liquidRenderer = liquidVisual.GetComponent<Renderer>();

        if (liquidRenderer == null)
            return;

        Material mat = Application.isPlaying ? liquidRenderer.material : liquidRenderer.sharedMaterial;
        if (mat == null)
            return;

        Color color = currentLiquid != null ? currentLiquid.liquidColor : new Color(0.25f, 0.65f, 1f, 0.45f);

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);

        if (mat.HasProperty("_Cull"))
            mat.SetInt("_Cull", (int)CullMode.Off);
    }

    private Material CreateTransparentMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = "Runtime_Water_Material";

        Color color = currentLiquid != null ? currentLiquid.liquidColor : new Color(0.25f, 0.65f, 1f, 0.45f);

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

        if (mat.HasProperty("_Cull"))
            mat.SetInt("_Cull", (int)CullMode.Off);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        return mat;
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

    private void ShowSpill(float amountMl)
    {
        // Focus-pour mode: LiquidContainer never creates puddle/residue discs.
        // Any pour/spill animation is handled by LiquidPourer as a temporary stream only.
        DestroyOverflowSpillVisual();
    }


    [ContextMenu("Pyrex Diameter Big Quick Fix")]
    public void PyrexDiameterBigQuickFix()
    {
        float h = Mathf.Max(0.000001f, fullHeightLocal);
        diameterXLocal = Mathf.Max(diameterXLocal, h * 1.45f);
        diameterZLocal = Mathf.Max(diameterZLocal, h * 1.45f);
        diameterMultiplier = 1.06f;
        rimPaddingPercent = 0.012f;
        capSurfaceAtLowestRim = false;
        autoSpillWhenPastRim = false;
        spillOverflowWhenFull = false;
        enableTiltSpill = false;
        enableFakeSlosh = false;
        sourceDirty = true;
        ForceRefreshLiquidVisual();
    }

    [ContextMenu("Widen Liquid Diameter 15 Percent")]
    public void WidenLiquidDiameter15Percent()
    {
        diameterXLocal *= 1.15f;
        diameterZLocal *= 1.15f;
        sourceDirty = true;
        ForceRefreshLiquidVisual();
    }

    [ContextMenu("Shrink Liquid Diameter 10 Percent")]
    public void ShrinkLiquidDiameter10Percent()
    {
        diameterXLocal *= 0.90f;
        diameterZLocal *= 0.90f;
        sourceDirty = true;
        ForceRefreshLiquidVisual();
    }

    [ContextMenu("Disable Container Spill Visuals")]
    public void DisableContainerSpillVisuals()
    {
        autoSpillWhenPastRim = false;
        spillOverflowWhenFull = false;
        enableTiltSpill = false;
        spilledMl = 0f;
        DestroyOverflowSpillVisual();
        ForceRefreshLiquidVisual();
    }

    private void DestroyOverflowSpillVisual()
    {
        if (overflowSpillVisual == null)
            return;

        GameObject go = overflowSpillVisual.gameObject;
        overflowSpillVisual = null;
        overflowSpillRenderer = null;

        if (go == null)
            return;

        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    private void CreateOverflowSpillVisual()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = $"{name}_OverflowSpill";
        go.transform.SetParent(null, false);

        Collider col = go.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        overflowSpillVisual = go.transform;
        overflowSpillRenderer = go.GetComponent<Renderer>();
        if (overflowSpillRenderer != null)
            overflowSpillRenderer.material = CreateOverflowSpillMaterial();
    }

    private Material CreateOverflowSpillMaterial()
    {
        Material mat = liquidMaterial != null ? new Material(liquidMaterial) : CreateTransparentMaterial();
        mat.name = "Runtime_Overflow_Spill_Material";

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", overflowSpillColor);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", overflowSpillColor);

        if (mat.HasProperty("_Cull"))
            mat.SetInt("_Cull", (int)CullMode.Off);

        return mat;
    }

    private void UpdateSpillPosition()
    {
        if (overflowSpillVisual == null || spilledMl <= 0f)
            return;

        overflowSpillVisual.position = FindSpillSurfacePosition();
    }

    private Vector3 FindSpillSurfacePosition()
    {
        Vector3 origin = liquidSpace != null
            ? liquidSpace.TransformPoint(new Vector3(0f, GetEffectiveFullHeightLocal(), 0f)) + Vector3.up * 0.05f
            : transform.position + Vector3.up * 0.1f;

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            return hit.point + Vector3.up * 0.002f;
        }

        return origin - Vector3.up * 0.05f;
    }

    private Vector3 GetFillAxisLocal()
    {
        return fillAxisLocal.sqrMagnitude < 0.0001f ? Vector3.up : fillAxisLocal.normalized;
    }

    private Vector3 ClosestSignedCardinal(Vector3 value)
    {
        value = value.sqrMagnitude < 0.0001f ? Vector3.up : value.normalized;
        float ax = Mathf.Abs(value.x);
        float ay = Mathf.Abs(value.y);
        float az = Mathf.Abs(value.z);

        if (ax >= ay && ax >= az)
            return value.x >= 0f ? Vector3.right : Vector3.left;

        if (ay >= ax && ay >= az)
            return value.y >= 0f ? Vector3.up : Vector3.down;

        return value.z >= 0f ? Vector3.forward : Vector3.back;
    }

    private void BuildPerpendicularBasis(Vector3 axis, out Vector3 basisX, out Vector3 basisZ)
    {
        axis = axis.normalized;
        Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.95f ? Vector3.up : Vector3.right;
        basisX = Vector3.Cross(reference, axis).normalized;
        basisZ = Vector3.Cross(axis, basisX).normalized;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        Vector3 axis = GetFillAxisLocal();
        Vector3 bottom = transform.TransformPoint(axis * bottomLocalY);
        Vector3 top = transform.TransformPoint(axis * (bottomLocalY + GetEffectiveFullHeightLocal()));

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(bottom, top);
        Gizmos.DrawWireSphere(bottom, 0.008f);
        Gizmos.DrawWireSphere(top, 0.008f);

        if (liquidSpace != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(liquidSpace.position, liquidSpace.position + liquidSpace.up * 0.12f);
        }
    }
}
