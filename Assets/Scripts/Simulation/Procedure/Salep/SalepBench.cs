using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Koordinator runtime workflow Salep. Menghubungkan timbangan (PowderDepositZone),
/// sendokTanduk (HornSpoon), mortar (SalepMortarVisual), dan profil bahan.
///
/// Tidak auto-run saat scene load. Di-bind sekali oleh SalepIngredientRuntimeSetup
/// (dipanggil saat MULAI SIMULASI), lalu di-drive per step oleh SalepProcedureManager.
/// </summary>
[DisallowMultipleComponent]
public sealed class SalepBench : MonoBehaviour
{
    public static SalepBench Instance { get; private set; }

    [Header("Refs (auto-resolved jika kosong)")]
    [SerializeField] private PowderDepositZone depositZone;
    [SerializeField] private HornSpoon spoon;
    [SerializeField] private MortarController mortar;
    [SerializeField] private SalepPanDepositVisual panVisual;
    [SerializeField] private SalepMortarVisual mortarVisual;

    [Header("Resep (sumber data editable)")]
    [SerializeField] private SalepRecipeDefinition recipe;

    [Header("Profil bahan")]
    [SerializeField] private IngredientVisualProfile asamProfile;
    [SerializeField] private IngredientVisualProfile sulfurProfile;
    [SerializeField] private IngredientVisualProfile vaselinProfile;

    [Header("Visual krim di piring (Vaselin)")]
    [SerializeField] private SalepPanCreamVisual panCream;

    [Header("Toleransi target (mg)")]
    [SerializeField] private float toleranceMg = 1f;

    public SalepRecipeDefinition Recipe => recipe;

    [Header("Events")]
    public UnityEvent<string> onWeighingTargetReached;

    private IngredientVisualProfile activeProfile;
    private bool subscribed;
    private bool targetAnnounced;
    private string currentScoopableId;

    /// <summary>True jika bahan ini termasuk bahan Salep yang dikelola bench (Asam/Sulfur/Vaselin).</summary>
    public bool ManagesIngredient(string ingredientId)
    {
        if (string.IsNullOrEmpty(ingredientId))
            return false;

        return (asamProfile != null && ingredientId == asamProfile.IngredientId)
            || (sulfurProfile != null && ingredientId == sulfurProfile.IngredientId)
            || (vaselinProfile != null && ingredientId == vaselinProfile.IngredientId);
    }

    /// <summary>
    /// Apakah bahan boleh di-scoop sekarang.
    /// - Saat ada step penimbangan aktif (currentScoopableId terisi): HANYA bahan itu.
    /// - Saat tidak menimbang (null): izinkan (lid gating tetap berlaku) supaya tetap
    ///   playable meski step belum maju. Mencegah semua bahan terkunci.
    /// </summary>
    public bool IsIngredientScoopable(string ingredientId)
    {
        if (string.IsNullOrEmpty(currentScoopableId))
            return true;

        return ingredientId == currentScoopableId;
    }

    /// <summary>Set bahan mana yang boleh di-scoop pada step ini (null = tidak ada).</summary>
    public void SetScoopableIngredient(string ingredientId)
    {
        currentScoopableId = ingredientId;
    }

    public bool IsWeighingActive =>
        activeProfile != null && !string.IsNullOrEmpty(currentScoopableId);

    /// <summary>True saat amount di pan sudah mencapai target bahan yang sedang ditimbang.</summary>
    public bool WeighingTargetReached => targetAnnounced;

    /// <summary>Teks progress timbangan live, mis. "Asam Salisilat: 50 / 200 mg". Null jika tidak menimbang.</summary>
    public string GetWeighingProgressText()
    {
        if (!IsWeighingActive || depositZone == null)
            return null;

        float current = depositZone.DepositedMg;
        float target = activeProfile.TargetTotalMg;

        if (activeProfile.DisplayInGrams)
            return $"{activeProfile.DisplayName}: {current / 1000f:0.#} / {target / 1000f:0.#} g";

        return $"{activeProfile.DisplayName}: {current:0} / {target:0} mg";
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        Unsubscribe();
    }

    /// <summary>Set komponen visual krim piring (Vaselin) dari setup runtime. Aman null.</summary>
    public void SetPanCream(SalepPanCreamVisual cream)
    {
        if (cream != null)
            panCream = cream;
    }

    /// <summary>Bind semua referensi sekali; aman dipanggil ulang.</summary>
    public void Bind(
        PowderDepositZone newDepositZone,
        HornSpoon newSpoon,
        MortarController newMortar,
        IngredientVisualProfile asam,
        IngredientVisualProfile sulfur,
        IngredientVisualProfile vaselin)
    {
        if (newDepositZone != null) depositZone = newDepositZone;
        if (newSpoon != null) spoon = newSpoon;
        if (newMortar != null) mortar = newMortar;
        if (asam != null) asamProfile = asam;
        if (sulfur != null) sulfurProfile = sulfur;
        if (vaselin != null) vaselinProfile = vaselin;

        EnsureVisuals();
        Subscribe();
    }

    private void EnsureVisuals()
    {
        // Visual bubuk di piring neraca memakai PowderVisualLevelSwitcher bawaan
        // (PowderDepositVisual pada Plate_Left_Target) yang SUDAH di-drive PowderDepositZone.
        // Procedural mound (SalepPanDepositVisual) dimatikan supaya tidak berantakan/dobel.
        if (depositZone != null)
        {
            SalepPanDepositVisual stray = depositZone.GetComponentInChildren<SalepPanDepositVisual>(true);
            if (stray != null)
                stray.gameObject.SetActive(false);
        }
        panVisual = null;

        if (mortarVisual == null && mortar != null)
        {
            mortarVisual = mortar.GetComponentInChildren<SalepMortarVisual>(true);
            if (mortarVisual == null)
                mortarVisual = mortar.gameObject.AddComponent<SalepMortarVisual>();
        }

        // Matikan visual bubuk bawaan MortarController supaya tidak menutup visual
        // dua-warna Salep (Asam putih + Sulfur kuning).
        if (mortar != null)
        {
            mortar.SetPowderVisualSuppressed(true);
            // Salep butuh kapasitas besar: 600 mg serbuk + 10000 mg Vaselin. Default Sirup
            // (3000 mg) menyebabkan tuang Vaselin mentok di ~2.4 g. Naikkan ke 12000 mg.
            mortar.SetMaxCapacityMg(12000f);
        }

        // Pakai mesh granul bubuk asli (dari plate timbangan) untuk mound mortar supaya
        // terlihat seperti bubuk butiran, bukan bola halus.
        if (mortarVisual != null && depositZone != null)
        {
            Mesh granule = depositZone.GetDepositGranuleMesh();
            if (granule != null)
                mortarVisual.ConfigureGranuleSource(granule);
        }
    }

    private void Subscribe()
    {
        if (subscribed || depositZone == null)
            return;

        depositZone.onPowderDeposited.AddListener(OnDeposited);
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || depositZone == null)
            return;

        depositZone.onPowderDeposited.RemoveListener(OnDeposited);
        subscribed = false;
    }

    /// <summary>Siapkan timbangan + visual untuk menimbang satu bahan (Asam/Sulfur/Vaselin).</summary>
    public void BeginWeighing(string ingredientId)
    {
        activeProfile = ResolveProfile(ingredientId);
        targetAnnounced = false;

        // Hanya bahan step ini yang boleh di-scoop (gating + tooltip).
        currentScoopableId = activeProfile != null ? activeProfile.IngredientId : null;

        if (activeProfile == null || depositZone == null)
            return;

        depositZone.SetDepositMg(0f);
        depositZone.ConfigureForRecipe(
            activeProfile.AmountPerScoopMg,
            activeProfile.TargetTotalMg,
            activeProfile.TargetTotalMg);
        depositZone.SetAcceptingDeposits(true);

        // Visual bubuk di piring kiri ikut warna bahan (Asam putih, Sulfur kuning,
        // Vaselin ivory). Memakai PowderVisualLevelSwitcher bawaan pada Plate_Left_Target.
        depositZone.SetDepositVisualTint(activeProfile.FallbackColor);
        // Salep neraca realistis: WAJIB taruh anak timbangan di piring kanan dulu
        // sebelum bubuk bisa masuk ke piring kiri. Target selesai tetap dari resep.
        depositZone.SetRequireRightPanTarget(true);

        if (panVisual != null)
            panVisual.ConfigureForIngredient(activeProfile, activeProfile.SpoonMaterial);

        // Vaselin = krim semi-padat → tampilkan visual krim mengilap di piring (bukan bubuk).
        // Bahan bubuk (Asam/Sulfur) → pastikan mode krim mati supaya mound bubuk normal tampil.
        if (panCream != null)
        {
            if (activeProfile.IsCream)
                panCream.Begin();
            else
                panCream.Stop();
        }
    }

    /// <summary>Bersihkan visual pan timbangan (mis. setelah dipindah ke mortar / reset).</summary>
    public void ClearPan()
    {
        if (depositZone != null)
            depositZone.SetDepositMg(0f);
        if (panVisual != null)
            panVisual.Clear();
        if (panCream != null)
            panCream.Stop();
    }

    public void SetMortarPhase(SalepMortarPhase phase, float fill01)
    {
        EnsureVisuals();
        if (mortarVisual != null)
            mortarVisual.SetPhase(phase, fill01);
    }

    public void SetMortarPhase(SalepMortarPhase phase, float fill01, float amount01)
    {
        EnsureVisuals();
        if (mortarVisual != null)
            mortarVisual.SetPhase(phase, fill01, amount01);
    }

    public void ResetAll()
    {
        ClearPan();
        currentScoopableId = null;
        if (mortarVisual != null)
            mortarVisual.Clear();

        // Kembalikan konfigurasi deposit zone ke default Difenhidramin (shared resource)
        // supaya workflow Syrup/Difenhidramin tidak terpengaruh takaran Salep.
        if (depositZone != null)
        {
            depositZone.ConfigureForRecipe(50f, 500f, 250f);
            depositZone.SetRequireRightPanTarget(true);
            depositZone.ClearDepositVisualTint();
        }

        // Kembalikan visual bubuk MortarController untuk workflow Syrup/Difenhidramin.
        if (mortar != null)
        {
            mortar.SetPowderVisualSuppressed(false);
            mortar.SetMaxCapacityMg(3000f); // kembalikan default Sirup
        }

        activeProfile = null;
        targetAnnounced = false;
    }

    private void OnDeposited(float depositedThisStepMg)
    {
        if (activeProfile == null || depositZone == null)
            return;

        // Update visual mound pada pan timbangan. Floating "+X" ditampilkan saat
        // SCOOP (lihat ScoopBottleTarget) agar tidak menumpuk dengan deposit.
        if (panVisual != null)
            panVisual.SetAmountMg(depositZone.DepositedMg);

        // Deteksi target tercapai → feedback "Tepat!" sekali.
        float effectiveTolerance = recipe != null ? Mathf.Max(0.001f, recipe.toleranceMg) : toleranceMg;
        if (!targetAnnounced &&
            depositZone.IsAtTargetMg(activeProfile.TargetTotalMg, effectiveTolerance))
        {
            targetAnnounced = true;
            Vector3 anchor = depositZone.transform.position + Vector3.up * 0.16f;
            SalepFloatingAmountText.Spawn(
                anchor,
                "Tepat!",
                new Color(0.3f, 1f, 0.45f, 1f));
            onWeighingTargetReached?.Invoke(activeProfile.IngredientId);
        }
    }

    private IngredientVisualProfile ResolveProfile(string ingredientId)
    {
        if (string.IsNullOrEmpty(ingredientId))
            return null;

        if (asamProfile != null && ingredientId == asamProfile.IngredientId)
            return asamProfile;
        if (sulfurProfile != null && ingredientId == sulfurProfile.IngredientId)
            return sulfurProfile;
        if (vaselinProfile != null && ingredientId == vaselinProfile.IngredientId)
            return vaselinProfile;

        return null;
    }
}
