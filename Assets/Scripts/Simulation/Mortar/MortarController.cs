using UnityEngine;
using UnityEngine.Events;

public class MortarController : MonoBehaviour
{
    [Header("Powder Capacity")]
    [SerializeField] private float maxCapacityMg = 3000f;
    [SerializeField] private float currentAmountMg = 0f;

    [Header("Recipe Visual Limit")]
    [Tooltip("Batas visual penuh untuk resep aktif. Untuk resep Difenhidramin sekarang = 250 mg.")]
    [SerializeField] private float powderVisualMaxMg = 250f;

    [Header("Transfer Guard")]
    [SerializeField] private bool requireExplicitPowderTransfer = true;
    [SerializeField] private bool acceptingPowderTransfer;

    [Header("Water")]
    [SerializeField] private float maxWaterMl = 100f;
    [SerializeField] private float currentWaterMl = 0f;

    [Header("Grinding / Mixing")]
    [SerializeField] private float grindingProgressRequired = 100f;
    [SerializeField] private float currentGrindingProgress = 0f;
    [SerializeField] private bool isHomogeneous = false;

    [Header("Step 5 Stir Phase")]
    [SerializeField] private float stirProgressRequired = 25f;
    [SerializeField] private float currentStirProgress = 0f;
    [SerializeField] private bool waitingForStir = false;
    [SerializeField] private int completedStirPhases = 0;

    [Header("Powder Mesh Visual Levels")]
    [Tooltip("Root kosong yang menampung semua level bubuk.")]
    [SerializeField] private Transform powderVisualRoot;

    [Tooltip("Isi dari kecil ke besar. Contoh: Bubuk_Level_01, Bubuk_Level_02, Bubuk_Level_03.")]
    [SerializeField] private GameObject[] powderLevelObjects;

    [SerializeField] private bool hideWhenEmpty = true;

    [Tooltip("ON kalau mesh level sudah kamu atur manual pos/scale-nya di scene.")]
    [SerializeField] private bool preserveAuthoredLevelTransforms = true;

    [Header("Wet Visual Adjustment")]
    [SerializeField] private bool adjustRootWhenWet = true;
    [SerializeField] private float wetSpreadMultiplier = 1.08f;
    [SerializeField] private float wetHeightMultiplier = 0.75f;

    [Header("Powder Material / Color")]
    [SerializeField] private Material powderMaterial;
    [SerializeField] private Color rawColor = new Color(0.96f, 0.96f, 0.92f, 1f);
    [SerializeField] private Color wetPowderColor = new Color(0.90f, 0.90f, 0.82f, 1f);
    [SerializeField] private Color homogeneousColor = new Color(0.84f, 0.84f, 0.74f, 1f);

    [Header("Events")]
    public UnityEvent<float> onAmountChanged;
    public UnityEvent<float> onWaterChanged;
    public UnityEvent<float> onGrindingProgressChanged;
    public UnityEvent<float> onStirProgressChanged;
    public UnityEvent onBecameHomogeneous;

    private Vector3 rootInitialLocalScale = Vector3.one;
    private Vector3 rootInitialLocalPosition = Vector3.zero;

    public float MaxCapacityMg => maxCapacityMg;
    public float CurrentAmountMg => currentAmountMg;
    public float CurrentWaterMl => currentWaterMl;

    public float PowderVisualMaxMg => powderVisualMaxMg;
    public float FillRatio => maxCapacityMg > 0f ? Mathf.Clamp01(currentAmountMg / maxCapacityMg) : 0f;
    public float VisualFillRatio => powderVisualMaxMg > 0f ? Mathf.Clamp01(currentAmountMg / powderVisualMaxMg) : 0f;
    public float WaterRatio => maxWaterMl > 0f ? Mathf.Clamp01(currentWaterMl / maxWaterMl) : 0f;

    public float GrindingProgressRatio => grindingProgressRequired > 0f ? Mathf.Clamp01(currentGrindingProgress / grindingProgressRequired) : 0f;

    public float CurrentStirProgress => currentStirProgress;
    public float CurrentStirProgress01 => stirProgressRequired > 0f ? Mathf.Clamp01(currentStirProgress / stirProgressRequired) : 0f;
    public bool WaitingForStir => waitingForStir;
    public int CompletedStirPhases => completedStirPhases;

    public bool IsHomogeneous => isHomogeneous;
    public bool IsEmpty => currentAmountMg <= 0.001f;
    public bool IsFull => currentAmountMg >= maxCapacityMg - 0.001f;
    public bool IsAcceptingPowderTransfer => acceptingPowderTransfer;
    public bool IsStep5MixDone => completedStirPhases >= 2 && isHomogeneous;

    private void Awake()
    {
        if (powderVisualRoot != null)
        {
            rootInitialLocalScale = powderVisualRoot.localScale;
            rootInitialLocalPosition = powderVisualRoot.localPosition;
        }
    }

    private void Start()
    {
        ApplyMaterialToAllLevels();
        UpdateVisual();
    }

    public void SetPowderVisualMaxMg(float value)
    {
        powderVisualMaxMg = Mathf.Max(1f, value);
        UpdateVisual();
    }

    public void SetAcceptingPowderTransfer(bool value)
    {
        acceptingPowderTransfer = value;
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

        UpdateVisual();
        onAmountChanged?.Invoke(currentAmountMg);

        return added;
    }

    public float AddPowderMg(float amountMg)
    {
        return AddPowder(amountMg);
    }

    public float AddWaterMl(float amountMl)
    {
        float safeAmount = Mathf.Max(0f, amountMl);
        float available = Mathf.Max(0f, maxWaterMl - currentWaterMl);
        float added = Mathf.Min(safeAmount, available);

        if (added <= 0.001f)
            return 0f;

        currentWaterMl += added;

        UpdateVisual();
        onWaterChanged?.Invoke(currentWaterMl);

        return added;
    }

    public void SetWaterMl(float amountMl)
    {
        currentWaterMl = Mathf.Clamp(amountMl, 0f, maxWaterMl);
        UpdateVisual();
        onWaterChanged?.Invoke(currentWaterMl);
    }

    public void BeginStirPhase()
    {
        waitingForStir = true;
        currentStirProgress = 0f;
        onStirProgressChanged?.Invoke(CurrentStirProgress01);
    }

    public void AddStirProgress(float amount)
    {
        if (!waitingForStir)
            return;

        if (IsEmpty)
            return;

        float safeAmount = Mathf.Max(0f, amount);
        currentStirProgress = Mathf.Min(currentStirProgress + safeAmount, stirProgressRequired);

        onStirProgressChanged?.Invoke(CurrentStirProgress01);

        if (currentStirProgress >= stirProgressRequired)
        {
            waitingForStir = false;
            completedStirPhases++;

            if (completedStirPhases >= 2)
            {
                isHomogeneous = true;
                onBecameHomogeneous?.Invoke();
            }

            UpdateVisual();
        }
    }

    public void AddGrindingProgress(float amount)
    {
        if (waitingForStir)
        {
            AddStirProgress(amount);
            return;
        }

        if (isHomogeneous || IsEmpty)
            return;

        float safeAmount = Mathf.Max(0f, amount);
        currentGrindingProgress = Mathf.Min(currentGrindingProgress + safeAmount, grindingProgressRequired);

        onGrindingProgressChanged?.Invoke(GrindingProgressRatio);

        if (currentGrindingProgress >= grindingProgressRequired)
        {
            isHomogeneous = true;
            onBecameHomogeneous?.Invoke();
            UpdateVisual();
        }
    }

    public void ResetStep5MixData()
    {
        currentWaterMl = 0f;
        currentStirProgress = 0f;
        completedStirPhases = 0;
        waitingForStir = false;
        isHomogeneous = false;

        UpdateVisual();
        onWaterChanged?.Invoke(currentWaterMl);
        onStirProgressChanged?.Invoke(CurrentStirProgress01);
    }

    public void ResetMortar()
    {
        currentAmountMg = 0f;
        currentWaterMl = 0f;
        currentGrindingProgress = 0f;
        currentStirProgress = 0f;
        completedStirPhases = 0;
        waitingForStir = false;
        isHomogeneous = false;
        acceptingPowderTransfer = false;

        UpdateVisual();

        onAmountChanged?.Invoke(currentAmountMg);
        onWaterChanged?.Invoke(currentWaterMl);
        onGrindingProgressChanged?.Invoke(GrindingProgressRatio);
        onStirProgressChanged?.Invoke(CurrentStirProgress01);
    }

    private void UpdateVisual()
    {
        UpdateLevelVisibility();
        UpdateWetRootShape();
        ApplyCurrentColor();
    }

    private void UpdateLevelVisibility()
    {
        if (powderLevelObjects == null || powderLevelObjects.Length == 0)
            return;

        for (int i = 0; i < powderLevelObjects.Length; i++)
        {
            if (powderLevelObjects[i] != null)
                powderLevelObjects[i].SetActive(false);
        }

        if (IsEmpty && hideWhenEmpty)
            return;

        float ratio = VisualFillRatio;

        int index = Mathf.CeilToInt(ratio * powderLevelObjects.Length) - 1;
        index = Mathf.Clamp(index, 0, powderLevelObjects.Length - 1);

        if (powderLevelObjects[index] != null)
            powderLevelObjects[index].SetActive(true);
    }

    private void UpdateWetRootShape()
    {
        if (!adjustRootWhenWet)
            return;

        if (powderVisualRoot == null)
            return;

        if (!preserveAuthoredLevelTransforms)
            return;

        float waterT = WaterRatio;

        Vector3 newScale = rootInitialLocalScale;
        newScale.x *= Mathf.Lerp(1f, wetSpreadMultiplier, waterT);
        newScale.z *= Mathf.Lerp(1f, wetSpreadMultiplier, waterT);
        newScale.y *= Mathf.Lerp(1f, wetHeightMultiplier, waterT);

        powderVisualRoot.localScale = newScale;
        powderVisualRoot.localPosition = rootInitialLocalPosition;
    }

    private void ApplyCurrentColor()
    {
        if (powderLevelObjects == null)
            return;

        float waterT = WaterRatio;

        Color colorNow = rawColor;

        if (waterT > 0f)
            colorNow = Color.Lerp(rawColor, wetPowderColor, waterT);

        if (isHomogeneous)
            colorNow = Color.Lerp(colorNow, homogeneousColor, 0.85f);

        for (int i = 0; i < powderLevelObjects.Length; i++)
        {
            GameObject level = powderLevelObjects[i];

            if (level == null || !level.activeSelf)
                continue;

            Renderer[] renderers = level.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (powderMaterial != null)
                    renderer.sharedMaterial = powderMaterial;

                Material mat = renderer.material;

                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", colorNow);

                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", colorNow);
            }
        }
    }

    private void ApplyMaterialToAllLevels()
    {
        if (powderMaterial == null || powderLevelObjects == null)
            return;

        for (int i = 0; i < powderLevelObjects.Length; i++)
        {
            GameObject level = powderLevelObjects[i];

            if (level == null)
                continue;

            Renderer[] renderers = level.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                    renderer.sharedMaterial = powderMaterial;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxCapacityMg = Mathf.Max(1f, maxCapacityMg);
        currentAmountMg = Mathf.Clamp(currentAmountMg, 0f, maxCapacityMg);

        powderVisualMaxMg = Mathf.Max(1f, powderVisualMaxMg);

        maxWaterMl = Mathf.Max(1f, maxWaterMl);
        currentWaterMl = Mathf.Clamp(currentWaterMl, 0f, maxWaterMl);

        grindingProgressRequired = Mathf.Max(1f, grindingProgressRequired);
        currentGrindingProgress = Mathf.Clamp(currentGrindingProgress, 0f, grindingProgressRequired);

        stirProgressRequired = Mathf.Max(1f, stirProgressRequired);
        currentStirProgress = Mathf.Clamp(currentStirProgress, 0f, stirProgressRequired);
    }
#endif
}