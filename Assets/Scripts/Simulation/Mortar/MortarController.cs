using UnityEngine;
using UnityEngine.Events;

public enum MortarMixPhase
{
    AddWaterStage1,
    StirStage1,
    ScrapeStage1,
    AddWaterStage2,
    StirStage2,
    ScrapeStage2,
    Complete
}

[DisallowMultipleComponent]
public class MortarController : MonoBehaviour
{
    [Header("Powder Capacity")]
    [SerializeField] private float maxCapacityMg = 3000f;
    [SerializeField] private float currentAmountMg = 0f;

    [Header("Recipe - Syrup Difenhidramin")]
    [SerializeField] private float targetPowderMg = 250f;
    [SerializeField] private float powderVisualMaxMg = 250f;
    [SerializeField] private float targetWaterMl = 100f;
    [SerializeField] private float stageWaterMl = 50f;

    [Header("Transfer Guard")]
    [SerializeField] private bool requireExplicitPowderTransfer = true;
    [SerializeField] private bool acceptingPowderTransfer;

    [Header("Water Runtime")]
    [SerializeField] private float currentWaterMl = 0f;

    [Header("Step 5 Runtime")]
    [SerializeField] private MortarMixPhase currentPhase = MortarMixPhase.AddWaterStage1;
    [SerializeField] private float stirProgressRequired = 25f;
    [SerializeField] private float currentStirProgress = 0f;
    [SerializeField] private int completedStirPhases = 0;
    [SerializeField] private bool hasResidueOnStamper = false;
    [SerializeField] private bool isHomogeneous = false;

    [Header("Dry Powder Visual Levels")]
    [SerializeField] private Transform powderVisualRoot;
    [SerializeField] private GameObject[] powderLevelObjects;
    [SerializeField] private bool hideWhenEmpty = true;
    [SerializeField] private float wetPowderMinimumScale = 0.32f;

    [Header("Mixture / Syrup Visual")]
    [SerializeField] private bool createMixtureVisualIfMissing = true;
    [SerializeField] private Transform mixtureVisual;
    [SerializeField] private Renderer mixtureRenderer;
    [SerializeField] private Material mixtureMaterial;

    [Tooltip("Karena Mortar mesh kamu rotation X -90, posisi naik/turun visual biasanya lewat local Z, bukan local Y.")]
    [SerializeField] private Vector3 mixtureEmptyLocalPosition = new Vector3(0f, 0f, 0.00012f);
    [SerializeField] private Vector3 mixtureFullLocalPosition = new Vector3(0f, 0f, 0.00019f);

    [Tooltip("Mesh campuran dibuat pada bidang XY. Karena root Mortar sudah X -90, visual harus tetap rotation 0.")]
    [SerializeField] private Vector3 mixtureLocalEuler = Vector3.zero;

    [SerializeField] private Vector3 mixtureEmptyLocalScale = new Vector3(0.00145f, 0.00145f, 1f);
    [SerializeField] private Vector3 mixtureFullLocalScale = new Vector3(0.00285f, 0.00285f, 1f);

    [Header("Visual Color")]
    [SerializeField] private Material powderMaterial;
    [SerializeField] private Color dryPowderColor = new Color(0.96f, 0.95f, 0.88f, 1f);
    [SerializeField] private Color wetPowderColor = new Color(0.88f, 0.86f, 0.76f, 1f);
    [SerializeField] private Color earlySyrupColor = new Color(0.92f, 0.90f, 0.76f, 0.28f);
    [SerializeField] private Color finalSyrupColor = new Color(0.82f, 0.78f, 0.55f, 0.48f);
    [Header("Events")]
    public UnityEvent<float> onAmountChanged;
    public UnityEvent<float> onWaterChanged;
    public UnityEvent<float> onStirProgressChanged;
    public UnityEvent onBecameHomogeneous;
    public UnityEvent onResidueRequired;
    public UnityEvent onScrapeCompleted;
    [Header("Mixture Stir Effect")]
    [SerializeField] private bool animateMixtureWhileStirring = true;
    [SerializeField] private float mixturePulseAmount = 0.035f;
    [SerializeField] private float mixturePulseSpeed = 8f;
    [Tooltip("Efek cairan tetap aktif sebentar setelah gerakan Stamper terakhir agar animasinya tidak patah-patah.")]
    [SerializeField] private float activeStirEffectHoldSeconds = 0.16f;

    public float MaxCapacityMg => maxCapacityMg;
    public float CurrentAmountMg => currentAmountMg;
    public float CurrentPowderMg => currentAmountMg;
    public float CurrentWaterMl => currentWaterMl;

    public float TargetPowderMg => targetPowderMg;
    public float TargetWaterMl => targetWaterMl;
    public float StageWaterMl => stageWaterMl;

    public float PowderVisualMaxMg => powderVisualMaxMg;
    public float PowderAmount01 => powderVisualMaxMg > 0f ? Mathf.Clamp01(currentAmountMg / powderVisualMaxMg) : 0f;
    public float Water01 => targetWaterMl > 0f ? Mathf.Clamp01(currentWaterMl / targetWaterMl) : 0f;
    public float CurrentStirProgress01 => stirProgressRequired > 0f ? Mathf.Clamp01(currentStirProgress / stirProgressRequired) : 0f;
    public float OverallMix01 => GetOverallMix01();

    public MortarMixPhase CurrentPhase => currentPhase;
    public int CompletedStirPhases => completedStirPhases;
    public bool HasResidueOnStamper => hasResidueOnStamper;
    public bool IsHomogeneous => isHomogeneous;
    public bool IsEmpty => currentAmountMg <= 0.001f;
    public bool IsFull => currentAmountMg >= maxCapacityMg - 0.001f;
    public bool IsAcceptingPowderTransfer => acceptingPowderTransfer;
    public bool WaitingForStir => currentPhase == MortarMixPhase.StirStage1 || currentPhase == MortarMixPhase.StirStage2;
    public bool IsActivelyStirring =>
        WaitingForStir &&
        Application.isPlaying &&
        Time.time - lastStirMotionTime <= activeStirEffectHoldSeconds;
    public bool IsStep5MixDone => currentPhase == MortarMixPhase.Complete;
    public float RemainingWaterMlForCurrentPhase => Mathf.Max(0f, GetCurrentWaterLimitForPhase() - currentWaterMl);

    private Vector3[] powderOriginalScales;
    private float lastStirMotionTime = float.NegativeInfinity;
    private bool powderVisualSuppressed;

    /// <summary>
    /// Saat true, visual bubuk bawaan MortarController (Bubuk_Level_*) dimatikan total.
    /// Dipakai workflow Salep agar visual dua-warna (SalepMortarVisual) tidak tertutup
    /// blob cream MortarController. Tidak memengaruhi data/mixing, hanya rendering powder.
    /// </summary>
    public void SetPowderVisualSuppressed(bool suppressed)
    {
        powderVisualSuppressed = suppressed;
        RefreshVisuals();
    }

    private void Awake()
    {
        CachePowderTransforms();
        EnsureMixtureVisual();
        RefreshVisuals();
    }

    private void Start()
    {
        ApplyPowderMaterialToAllLevels();
        RefreshVisuals();
    }

    public void SetPowderVisualMaxMg(float value)
    {
        powderVisualMaxMg = Mathf.Max(1f, value);
        RefreshVisuals();
    }

    public void SetRecipeVisual(float powderTargetMg, float waterTargetMl, float waterStageMl)
    {
        targetPowderMg = Mathf.Max(1f, powderTargetMg);
        powderVisualMaxMg = targetPowderMg;
        targetWaterMl = Mathf.Max(1f, waterTargetMl);
        stageWaterMl = Mathf.Clamp(waterStageMl, 1f, targetWaterMl);

        currentWaterMl = Mathf.Clamp(currentWaterMl, 0f, targetWaterMl);

        RefreshVisuals();
    }

    public void SetAcceptingPowderTransfer(bool value)
    {
        acceptingPowderTransfer = value;
    }

    /// <summary>
    /// Ubah kapasitas maksimum bubuk/isi mortar (mg). Default 3000 mg untuk Sirup, tapi
    /// Salep butuh jauh lebih besar (600 mg serbuk + 10000 mg Vaselin). Tanpa ini, tuang
    /// Vaselin berhenti di ~2.4 g karena mentok kapasitas. Aman dipanggil ulang.
    /// </summary>
    public void SetMaxCapacityMg(float value)
    {
        maxCapacityMg = Mathf.Max(1f, value);
        currentAmountMg = Mathf.Clamp(currentAmountMg, 0f, maxCapacityMg);
        RefreshVisuals();
    }

    public float AddPowder(float amountMg)
    {
        if (requireExplicitPowderTransfer && !acceptingPowderTransfer)
            return 0f;

        float safeAmount = Mathf.Max(0f, amountMg);
        float available = Mathf.Max(0f, maxCapacityMg - currentAmountMg);
        float added = Mathf.Min(safeAmount, available);

        if (added <= 0.001f)
            return 0f;

        currentAmountMg += added;

        RefreshVisuals();
        onAmountChanged?.Invoke(currentAmountMg);

        return added;
    }

    public float AddPowderMg(float amountMg)
    {
        return AddPowder(amountMg);
    }

    public void SetPowderMg(float amountMg)
    {
        currentAmountMg = Mathf.Clamp(amountMg, 0f, maxCapacityMg);

        RefreshVisuals();
        onAmountChanged?.Invoke(currentAmountMg);
    }

    public float AddWaterMl(float amountMl)
    {
        float safeAmount = Mathf.Max(0f, amountMl);

        if (safeAmount <= 0.001f)
            return 0f;

        float phaseLimit = GetCurrentWaterLimitForPhase();
        float available = Mathf.Max(0f, phaseLimit - currentWaterMl);
        float added = Mathf.Min(safeAmount, available);

        if (added <= 0.001f)
            return 0f;

        currentWaterMl += added;

        AdvanceWaterPhaseIfNeeded();

        RefreshVisuals();
        onWaterChanged?.Invoke(currentWaterMl);

        return added;
    }

    public float AddWater(float amountMl)
    {
        return AddWaterMl(amountMl);
    }

    public float RemoveMixtureMl(float amountMl)
    {
        float removed = Mathf.Min(Mathf.Max(0f, amountMl), currentWaterMl);

        if (removed <= 0.001f)
            return 0f;

        currentWaterMl -= removed;

        if (currentWaterMl <= 0.001f && isHomogeneous)
        {
            currentWaterMl = 0f;
            currentAmountMg = 0f;
            onAmountChanged?.Invoke(currentAmountMg);
        }

        RefreshVisuals();
        onWaterChanged?.Invoke(currentWaterMl);
        return removed;
    }

    public void SetWaterMl(float amountMl)
    {
        currentWaterMl = Mathf.Clamp(amountMl, 0f, targetWaterMl);

        AdvanceWaterPhaseIfNeeded();

        RefreshVisuals();
        onWaterChanged?.Invoke(currentWaterMl);
    }

    public void ClearCompletedMixture()
    {
        currentWaterMl = 0f;
        currentAmountMg = 0f;

        RefreshVisuals();
        onAmountChanged?.Invoke(currentAmountMg);
        onWaterChanged?.Invoke(currentWaterMl);
    }

    public void BeginStirPhase()
    {
        if (currentPhase == MortarMixPhase.AddWaterStage1 && currentWaterMl >= stageWaterMl - 0.01f)
            currentPhase = MortarMixPhase.StirStage1;
        else if (currentPhase == MortarMixPhase.AddWaterStage2 && currentWaterMl >= targetWaterMl - 0.01f)
            currentPhase = MortarMixPhase.StirStage2;

        if (WaitingForStir)
        {
            currentStirProgress = 0f;
            lastStirMotionTime = float.NegativeInfinity;
        }

        RefreshVisuals();
    }

    public void AddStirProgress(float amount)
    {
        if (!WaitingForStir)
            return;

        if (IsEmpty)
            return;

        float safeAmount = Mathf.Max(0f, amount);

        if (safeAmount <= 0.0001f)
            return;

        lastStirMotionTime = Time.time;
        currentStirProgress = Mathf.Min(currentStirProgress + safeAmount, stirProgressRequired);
        onStirProgressChanged?.Invoke(CurrentStirProgress01);

        if (currentStirProgress >= stirProgressRequired)
        {
            CompleteCurrentStirPhase();
        }

        RefreshVisuals();
    }

    public void AddGrindingProgress(float amount)
    {
        AddStirProgress(amount);
    }

    public void ForceResidueOnStamper()
    {
        hasResidueOnStamper = true;
        onResidueRequired?.Invoke();
        RefreshVisuals();
    }

    public void CompleteScrape()
    {
        if (!hasResidueOnStamper)
            return;

        hasResidueOnStamper = false;
        onScrapeCompleted?.Invoke();

        if (currentPhase == MortarMixPhase.ScrapeStage1)
        {
            currentPhase = MortarMixPhase.AddWaterStage2;
            currentStirProgress = 0f;
        }
        else if (currentPhase == MortarMixPhase.ScrapeStage2)
        {
            currentPhase = MortarMixPhase.Complete;
            currentStirProgress = stirProgressRequired;
            isHomogeneous = true;
            onBecameHomogeneous?.Invoke();
        }

        RefreshVisuals();
    }

    public void ResetStep5MixData()
    {
        currentWaterMl = 0f;
        currentStirProgress = 0f;
        completedStirPhases = 0;
        hasResidueOnStamper = false;
        isHomogeneous = false;
        currentPhase = MortarMixPhase.AddWaterStage1;
        lastStirMotionTime = float.NegativeInfinity;

        RefreshVisuals();

        onWaterChanged?.Invoke(currentWaterMl);
        onStirProgressChanged?.Invoke(CurrentStirProgress01);
    }

    public void ResetMortar()
    {
        currentAmountMg = 0f;
        currentWaterMl = 0f;
        currentStirProgress = 0f;
        completedStirPhases = 0;
        hasResidueOnStamper = false;
        isHomogeneous = false;
        acceptingPowderTransfer = false;
        currentPhase = MortarMixPhase.AddWaterStage1;
        lastStirMotionTime = float.NegativeInfinity;

        RefreshVisuals();

        onAmountChanged?.Invoke(currentAmountMg);
        onWaterChanged?.Invoke(currentWaterMl);
        onStirProgressChanged?.Invoke(CurrentStirProgress01);
    }

    private float GetCurrentWaterLimitForPhase()
    {
        if (currentPhase == MortarMixPhase.AddWaterStage1)
            return stageWaterMl;

        if (currentPhase == MortarMixPhase.StirStage1 || currentPhase == MortarMixPhase.ScrapeStage1)
            return stageWaterMl;

        return targetWaterMl;
    }

    private void AdvanceWaterPhaseIfNeeded()
    {
        if (currentPhase == MortarMixPhase.AddWaterStage1 && currentWaterMl >= stageWaterMl - 0.01f)
        {
            currentPhase = MortarMixPhase.StirStage1;
            currentStirProgress = 0f;
        }
        else if (currentPhase == MortarMixPhase.AddWaterStage2 && currentWaterMl >= targetWaterMl - 0.01f)
        {
            currentPhase = MortarMixPhase.StirStage2;
            currentStirProgress = 0f;
        }
    }

    private void CompleteCurrentStirPhase()
    {
        hasResidueOnStamper = true;
        completedStirPhases++;
        onResidueRequired?.Invoke();

        if (currentPhase == MortarMixPhase.StirStage1)
        {
            currentPhase = MortarMixPhase.ScrapeStage1;
        }
        else if (currentPhase == MortarMixPhase.StirStage2)
        {
            currentPhase = MortarMixPhase.ScrapeStage2;
        }
    }

    private float GetOverallMix01()
    {
        if (currentPhase == MortarMixPhase.AddWaterStage1)
            return 0f;

        if (currentPhase == MortarMixPhase.StirStage1)
            return 0.5f * CurrentStirProgress01;

        if (currentPhase == MortarMixPhase.ScrapeStage1 || currentPhase == MortarMixPhase.AddWaterStage2)
            return 0.5f;

        if (currentPhase == MortarMixPhase.StirStage2)
            return 0.5f + 0.5f * CurrentStirProgress01;

        if (currentPhase == MortarMixPhase.ScrapeStage2 || currentPhase == MortarMixPhase.Complete)
            return 1f;

        return 0f;
    }

    private void RefreshVisuals()
    {
        EnsureMixtureVisual();

        float mix01 = GetOverallMix01();
        float powderVisibility = isHomogeneous
            ? 0f
            : 1f - Mathf.SmoothStep(0f, 1f, mix01);
        float mixtureVisibility = Mathf.Clamp01(Water01 * 0.8f + mix01 * 0.55f);

        RefreshPowderLevels(powderVisibility, mix01);
        RefreshMixtureVisual(mixtureVisibility, mix01);
    }

    private void RefreshPowderLevels(float powderVisibility, float mix01)
    {
        if (powderLevelObjects == null || powderLevelObjects.Length == 0)
            return;

        for (int i = 0; i < powderLevelObjects.Length; i++)
        {
            if (powderLevelObjects[i] != null)
                powderLevelObjects[i].SetActive(false);
        }

        // Salep: visual bubuk bawaan dimatikan, biar SalepMortarVisual yang tampil.
        if (powderVisualSuppressed)
            return;

        if (IsEmpty && hideWhenEmpty)
            return;

        float effective = PowderAmount01 * Mathf.Clamp01(powderVisibility);

        if (effective <= 0.04f)
            return;

        int index = Mathf.CeilToInt(effective * powderLevelObjects.Length) - 1;
        index = Mathf.Clamp(index, 0, powderLevelObjects.Length - 1);

        if (powderLevelObjects[index] != null)
        {
            powderLevelObjects[index].SetActive(true);
            ApplyPowderShrink(index, mix01);
        }

        ApplyPowderColor(index, mix01);
    }

    private void CachePowderTransforms()
    {
        if (powderLevelObjects == null)
            return;

        powderOriginalScales = new Vector3[powderLevelObjects.Length];

        for (int i = 0; i < powderLevelObjects.Length; i++)
        {
            powderOriginalScales[i] = powderLevelObjects[i] != null
                ? powderLevelObjects[i].transform.localScale
                : Vector3.one;
        }
    }

    private void ApplyPowderShrink(int activeIndex, float mix01)
    {
        if (powderLevelObjects == null ||
            powderOriginalScales == null ||
            activeIndex < 0 ||
            activeIndex >= powderLevelObjects.Length ||
            activeIndex >= powderOriginalScales.Length ||
            powderLevelObjects[activeIndex] == null)
        {
            return;
        }

        float wetness = Mathf.Clamp01(mix01 * 0.92f + Water01 * 0.08f);
        float scaleMultiplier = Mathf.Lerp(1f, wetPowderMinimumScale, wetness);
        powderLevelObjects[activeIndex].transform.localScale = powderOriginalScales[activeIndex] * scaleMultiplier;
    }

    private void RefreshMixtureVisual(float mixtureVisibility, float mix01)
    {
        if (mixtureVisual == null)
            return;

        bool show = mixtureVisibility > 0.03f && currentWaterMl > 0.1f;
        mixtureVisual.gameObject.SetActive(show);

        if (!show)
            return;

        mixtureVisual.localPosition = Vector3.Lerp(mixtureEmptyLocalPosition, mixtureFullLocalPosition, Water01);
        mixtureVisual.localRotation = Quaternion.Euler(mixtureLocalEuler);

        Vector3 scale = Vector3.Lerp(mixtureEmptyLocalScale, mixtureFullLocalScale, Water01);
        float spreadBonus = Mathf.Lerp(1f, 1.08f, mix01);
        scale.x *= spreadBonus;
        scale.y *= spreadBonus;

        if (animateMixtureWhileStirring && IsActivelyStirring)
        {
            float pulse = 1f + Mathf.Sin(Time.time * mixturePulseSpeed) * mixturePulseAmount;
            scale.x *= pulse;
            scale.y *= pulse;
        }


        mixtureVisual.localScale = scale;

        if (mixtureRenderer == null)
            mixtureRenderer = mixtureVisual.GetComponentInChildren<Renderer>(true);

        if (mixtureRenderer == null)
            return;

        if (mixtureMaterial != null)
            mixtureRenderer.sharedMaterial = mixtureMaterial;

        Material mat = mixtureRenderer.material;
        Color color = Color.Lerp(earlySyrupColor, finalSyrupColor, mix01);
        color.a = Mathf.Lerp(0.16f, color.a, mixtureVisibility);

        SetMaterialColor(mat, color);
    }

    private void Update()
    {
        if (WaitingForStir && mixtureVisual != null && mixtureVisual.gameObject.activeSelf)
        {
            RefreshVisuals();
        }
    }

    private void ApplyPowderColor(int activeIndex, float mix01)
    {
        if (powderLevelObjects == null)
            return;

        if (activeIndex < 0 || activeIndex >= powderLevelObjects.Length)
            return;

        GameObject activeLevel = powderLevelObjects[activeIndex];

        if (activeLevel == null)
            return;

        Renderer[] renderers = activeLevel.GetComponentsInChildren<Renderer>(true);
        Color color = Color.Lerp(dryPowderColor, wetPowderColor, Mathf.Clamp01(Water01 + mix01 * 0.5f));

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
                continue;

            if (powderMaterial != null)
                renderer.sharedMaterial = powderMaterial;

            Material mat = renderer.material;
            SetMaterialColor(mat, color);
        }
    }

    private void ApplyPowderMaterialToAllLevels()
    {
        if (powderMaterial == null || powderLevelObjects == null)
            return;

        for (int i = 0; i < powderLevelObjects.Length; i++)
        {
            GameObject level = powderLevelObjects[i];

            if (level == null)
                continue;

            Renderer[] renderers = level.GetComponentsInChildren<Renderer>(true);

            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] != null)
                    renderers[r].sharedMaterial = powderMaterial;
            }
        }
    }

    private void EnsureMixtureVisual()
    {
        if (mixtureVisual == null)
        {
            Transform existing = transform.Find("MortarMixtureVisual");

            if (existing == null)
                existing = transform.Find("MortarSyrupVisual");

            if (existing != null)
                mixtureVisual = existing;
        }

        if (mixtureRenderer == null && mixtureVisual != null)
            mixtureRenderer = mixtureVisual.GetComponentInChildren<Renderer>(true);

        if (mixtureVisual != null || !createMixtureVisualIfMissing)
            return;

        GameObject visualObject = new GameObject("MortarMixtureVisual");
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = mixtureEmptyLocalPosition;
        visualObject.transform.localRotation = Quaternion.Euler(mixtureLocalEuler);
        visualObject.transform.localScale = mixtureEmptyLocalScale;

        MeshFilter meshFilter = visualObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = visualObject.AddComponent<MeshRenderer>();

        meshFilter.sharedMesh = BuildDiskMesh();

        mixtureVisual = visualObject.transform;
        mixtureRenderer = meshRenderer;

        Material mat = mixtureMaterial != null ? mixtureMaterial : CreateDefaultMixtureMaterial();

        if (mat != null)
            mixtureRenderer.sharedMaterial = mat;

        mixtureVisual.gameObject.SetActive(false);
    }

    private Mesh BuildDiskMesh()
    {
        const int segments = 80;
        const float radius = 0.5f;

        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uvs = new Vector2[segments + 1];
        int[] triangles = new int[segments * 6];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;

            vertices[i + 1] = new Vector3(x, y, 0f);
            uvs[i + 1] = new Vector2(0.5f + x, 0.5f + y);
        }

        int t = 0;

        for (int i = 0; i < segments; i++)
        {
            int a = 0;
            int b = i + 1;
            int c = i == segments - 1 ? 1 : i + 2;

            triangles[t++] = a;
            triangles[t++] = b;
            triangles[t++] = c;

            triangles[t++] = a;
            triangles[t++] = c;
            triangles[t++] = b;
        }

        Mesh mesh = new Mesh();
        mesh.name = "MortarMixtureDisk_XY_Generated";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;
    }

    private Material CreateDefaultMixtureMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        Material mat = new Material(shader);
        mat.name = "Runtime_MortarMixture_Material";

        SetMaterialColor(mat, earlySyrupColor);

        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f);

        if (mat.HasProperty("_Blend"))
            mat.SetFloat("_Blend", 0f);

        if (mat.HasProperty("_SrcBlend"))
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (mat.HasProperty("_DstBlend"))
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        if (mat.HasProperty("_ZWrite"))
            mat.SetFloat("_ZWrite", 0f);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        return mat;
    }

    private void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null)
            return;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }

    [ContextMenu("DEBUG Set Powder 250mg")]
    private void DebugSetPowder250Mg()
    {
        acceptingPowderTransfer = true;
        currentAmountMg = targetPowderMg;
        acceptingPowderTransfer = false;

        RefreshVisuals();
        onAmountChanged?.Invoke(currentAmountMg);
    }

    [ContextMenu("DEBUG Add Water 50ml")]
    private void DebugAddWater50Ml()
    {
        AddWaterMl(50f);
    }

    [ContextMenu("DEBUG Complete Stir")]
    private void DebugCompleteStir()
    {
        if (!WaitingForStir)
            BeginStirPhase();

        currentStirProgress = stirProgressRequired;
        CompleteCurrentStirPhase();
        RefreshVisuals();
    }

    [ContextMenu("DEBUG Complete Scrape")]
    private void DebugCompleteScrape()
    {
        CompleteScrape();
    }

    [ContextMenu("DEBUG Reset Mortar")]
    private void DebugResetMortar()
    {
        ResetMortar();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxCapacityMg = Mathf.Max(1f, maxCapacityMg);
        currentAmountMg = Mathf.Clamp(currentAmountMg, 0f, maxCapacityMg);

        targetPowderMg = Mathf.Max(1f, targetPowderMg);
        powderVisualMaxMg = Mathf.Max(1f, powderVisualMaxMg);

        targetWaterMl = Mathf.Max(1f, targetWaterMl);
        stageWaterMl = Mathf.Clamp(stageWaterMl, 1f, targetWaterMl);
        currentWaterMl = Mathf.Clamp(currentWaterMl, 0f, targetWaterMl);

        stirProgressRequired = Mathf.Max(1f, stirProgressRequired);
        currentStirProgress = Mathf.Clamp(currentStirProgress, 0f, stirProgressRequired);
        wetPowderMinimumScale = Mathf.Clamp(wetPowderMinimumScale, 0.05f, 1f);
        activeStirEffectHoldSeconds = Mathf.Clamp(activeStirEffectHoldSeconds, 0.05f, 0.5f);
    }
#endif
}
