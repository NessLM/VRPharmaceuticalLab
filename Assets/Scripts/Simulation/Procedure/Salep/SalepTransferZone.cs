using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Zona snap/proximity untuk memindahkan bahan secara prosedural (bukan teleport otomatis).
///
/// Mode SpoonPowderToMortar (Step 3 &amp; 6):
///   Saat aktif, jika sendok tanduk yang sedang dipegang &amp; berisi bahan yang sesuai
///   masuk ke zona dan diam sebentar, isinya dipindah ke mortar (MortarController.AddPowder)
///   dan ReceivedMg bertambah. SalepProcedureManager membandingkan ReceivedMg dengan target.
///
/// Mode DwellToPot (Step 10):
///   Saat aktif, jika alat pembawa yang dipegang (sendok berisi krim, sudip, atau mortar)
///   diam di zona, Progress01 naik bertahap sampai 1. Dipakai untuk memindahkan salep
///   jadi ke pot tanpa harus mengandalkan timer diam-diam.
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public sealed class SalepTransferZone : MonoBehaviour
{
    public enum TransferMode
    {
        SpoonPowderToMortar,
        DwellToPot
    }

    [Header("Mode")]
    [SerializeField] private TransferMode mode = TransferMode.SpoonPowderToMortar;

    [Header("Filter Bahan (kosong = semua)")]
    [Tooltip("ID bahan yang diterima zona ini. Kosong = terima apa saja.")]
    [SerializeField] private string requiredIngredientId = "";

    [Tooltip("Jika ON, hanya menerima sendok yang membawa krim (Vaselin/salep).")]
    [SerializeField] private bool requireCream = false;

    [Header("SpoonPowderToMortar")]
    [SerializeField] private MortarController mortar;
    [Tooltip("Jeda minimal sendok diam di zona sebelum isinya dipindah (detik).")]
    [SerializeField] private float depositDwellSeconds = 0.25f;
    [Tooltip("Cooldown antar deposit agar tidak dobel (detik).")]
    [SerializeField] private float depositCooldown = 0.35f;

    [Tooltip("Sumber jumlah yang sudah ditimbang di pan. Jika sendok kosong didekatkan ke " +
             "mortar, jumlah dari pan ini dipindahkan (abstraksi gesture menuang perkamen).")]
    [SerializeField] private PowderDepositZone sourceDepositZone;

    [Tooltip("Izinkan menarik jumlah dari pan saat sendok kosong didekatkan ke mortar.")]
    [SerializeField] private bool pullFromDepositWhenEmpty = true;

    [Header("DwellToPot")]
    [Tooltip("Lama menahan alat di zona pot untuk menyelesaikan transfer (detik).")]
    [SerializeField] private float potDwellSeconds = 2.5f;
    [Tooltip("Jika ON, mortar yang dipegang & didekatkan juga diterima sebagai pemindah salep.")]
    [SerializeField] private bool acceptHeldMortarForPot = true;

    [Header("Validasi")]
    [Tooltip("Wajib alat sedang dipegang (XRGrabInteractable.isSelected).")]
    [SerializeField] private bool requireHeld = true;

    [Header("Debug (read-only)")]
    [SerializeField] private float receivedMg;
    [SerializeField] private float progress01;
    [SerializeField] private bool active;

    private float dwellTimer;
    private float nextDepositTime;

    public float ReceivedMg => receivedMg;
    public float Progress01 => Mathf.Clamp01(progress01);
    public bool IsActive => active;
    public TransferMode Mode => mode;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        if (mortar == null)
            mortar = GetComponentInParent<MortarController>();
    }

    public void SetActive(bool value)
    {
        active = value;
        dwellTimer = 0f;
    }

    public void SetRequiredIngredient(string id, bool cream)
    {
        requiredIngredientId = id;
        requireCream = cream;
    }

    public void ResetZone()
    {
        receivedMg = 0f;
        progress01 = 0f;
        dwellTimer = 0f;
        nextDepositTime = 0f;
    }

    public void ConfigureMortar(MortarController controller)
    {
        if (controller != null)
            mortar = controller;
    }

    public void ConfigureSource(PowderDepositZone depositZone)
    {
        if (depositZone != null)
            sourceDepositZone = depositZone;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!active)
            return;

        if (mode == TransferMode.SpoonPowderToMortar)
            HandleSpoonDeposit(other);
        else
            HandleDwellToPot(other);
    }

    private void HandleSpoonDeposit(Collider other)
    {
        HornSpoon spoon = other.GetComponentInParent<HornSpoon>();
        if (spoon == null)
            return;

        if (requireHeld && !IsHeld(spoon.gameObject))
            return;

        bool spoonHasMatch = !spoon.IsEmpty &&
                             MatchesFilter(spoon.CurrentIngredientId, spoon.CurrentVisualType);
        bool canPullFromPan = spoon.IsEmpty &&
                              pullFromDepositWhenEmpty &&
                              sourceDepositZone != null &&
                              sourceDepositZone.DepositedMg > 0.001f;

        if (!spoonHasMatch && !canPullFromPan)
            return;

        dwellTimer += Time.deltaTime;
        if (dwellTimer < depositDwellSeconds || Time.time < nextDepositTime)
            return;

        float amount;
        if (spoonHasMatch)
        {
            amount = spoon.RemovePowder(spoon.CurrentAmountMg);
        }
        else
        {
            amount = sourceDepositZone.DepositedMg;
            sourceDepositZone.SetDepositMg(0f);
        }

        if (amount <= 0.001f)
            return;

        if (mortar != null)
            mortar.AddPowder(amount);

        receivedMg += amount;
        dwellTimer = 0f;
        nextDepositTime = Time.time + depositCooldown;
    }

    private void HandleDwellToPot(Collider other)
    {
        bool valid = false;

        HornSpoon spoon = other.GetComponentInParent<HornSpoon>();
        if (spoon != null && !spoon.IsEmpty && MatchesFilter(spoon.CurrentIngredientId, spoon.CurrentVisualType))
            valid = requireHeld ? IsHeld(spoon.gameObject) : true;

        if (!valid && acceptHeldMortarForPot)
        {
            MortarController nearMortar = other.GetComponentInParent<MortarController>();
            if (nearMortar != null)
                valid = requireHeld ? IsHeld(nearMortar.gameObject) : true;
        }

        // Terima Sudip yang sedang membawa salep (visual ter-load) & sedang dipegang.
        if (!valid)
        {
            SudipSalepVisual sudipComp = other.GetComponentInParent<SudipSalepVisual>();
            if (sudipComp != null && sudipComp.IsLoaded)
                valid = requireHeld ? IsHeld(sudipComp.gameObject) : true;
        }

        if (!valid)
            return;

        dwellTimer += Time.deltaTime;
        progress01 = Mathf.Clamp01(dwellTimer / Mathf.Max(0.1f, potDwellSeconds));
    }

    private bool MatchesFilter(string ingredientId, IngredientVisualType visualType)
    {
        if (requireCream && visualType != IngredientVisualType.CreamOintment)
            return false;

        if (string.IsNullOrEmpty(requiredIngredientId))
            return true;

        return string.Equals(ingredientId, requiredIngredientId, System.StringComparison.Ordinal);
    }

    private static bool IsHeld(GameObject go)
    {
        XRGrabInteractable grab = go.GetComponentInParent<XRGrabInteractable>();
        return grab != null && grab.isSelected;
    }
}
