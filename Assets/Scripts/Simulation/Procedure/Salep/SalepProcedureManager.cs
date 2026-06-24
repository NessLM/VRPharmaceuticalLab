using System.Collections;
using EPOOutline;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class SalepProcedureManager : MonoBehaviour
{
    // Urutan 10 step prosedur Salep (mengikuti pola Sirup). Indeks 0..9 dipetakan ke
    // checklist, highlight, dan arrow yang sudah ada di scene.
    public enum SalepStep
    {
        Idle,
        Intro,
        Step_01_PrepareParchmentOnBalance,
        Step_02_WeighAsamSalisilat,
        Step_03_MoveAsamToMortar,
        Step_04_WeighSulfurPP,
        Step_05_MoveSulfurToMortar,
        Step_06_GrindPowders,
        Step_07_WeighVaselinAlbum,
        Step_08_MixOintment,
        Step_09_MoveOintmentToPot,
        Step_10_AttachEtiket,
        Done
    }

    [Header("Recipe (editable)")]
    [SerializeField] private SalepRecipeDefinition recipe;

    [Header("Step UI")]
    [SerializeField] private GameObject stepUiRoot;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private GameObject doneIcon;
    [SerializeField] private TMP_Text[] checklistTexts;

    [Header("Scene-authored Step Guidance")]
    [SerializeField] private Outlinable[] stepHighlights;
    [SerializeField] private WorldStepArrow[] stepArrows;

    [Header("Salep Ingredients")]
    [FormerlySerializedAs("salepObjectsRoot")]
    [SerializeField] private GameObject salepIngredientsRoot;
    [SerializeField] private Transform jarAsamSalisilat;
    [SerializeField] private Transform jarSulfurPP;
    [SerializeField] private Transform jarVaselinAlbum;
    [SerializeField] private Transform potSalep;

    [Header("Existing Shared Tools")]
    [SerializeField] private Transform mortarSet;
    [SerializeField] private Transform stamper;
    [SerializeField] private Transform sudip;
    [SerializeField] private Transform balance;
    [SerializeField] private Transform parchmentStack;
    [SerializeField] private Transform hornSpoon;

    [Header("Runtime Systems (auto-resolve if empty)")]
    [SerializeField] private SalepBench bench;
    [SerializeField] private MortarController mortarController;
    [SerializeField] private PowderDepositZone depositZone;
    [SerializeField] private WeightingZone parchmentPan;
    [SerializeField] private WeightingZone rightWeighingZone;

    [Header("Balance Reset (anak timbangan)")]
    [Tooltip("Tombol [SYS] BalanceWeightResetter di Models/Interactable.")]
    [SerializeField] private Transform balanceResetTarget;
    [SerializeField] private Outlinable balanceResetHighlight;
    [SerializeField] private float rightPanClearedGrams = 0.002f;
    [Tooltip("Komponen BalanceWeightResetter — dipanggil OTOMATIS saat step penimbangan selesai (anak timbangan kembali sendiri).")]
    [SerializeField] private BalanceWeightResetter balanceResetter;

    // Index step terakhir yang sudah memicu auto-reset timbangan (cegah retrigger tiap frame).
    private int autoResetTriggeredStep = -1;

    [Header("Interaction Zones (snap/proximity)")]
    [SerializeField] private SalepTransferZone mortarTransferZone;
    [SerializeField] private SalepMortarMixZone mortarMixZone;
    [SerializeField] private SalepTransferZone potTransferZone;

    [Header("Plate -> Mortar Pickup (pola Sirup, trigger)")]
    [Tooltip("Komponen trigger di sendokTanduk (sama seperti Sirup): L Mouse di editor / trigger controller di VR. Auto-resolve jika kosong.")]
    [SerializeField] private SpoonPowderPlateTransfer spoonPlateTransfer;

    [Header("Mixing Guide (panduan memutar, pola Sirup)")]
    [Tooltip("VIS_MortarStirGuide — lingkaran + indikator memutar saat menggerus/mengaduk. Auto-resolve jika kosong.")]
    [SerializeField] private MortarStirGuide mortarStirGuide;

    [Header("Etiket (Step 10)")]
    [SerializeField] private EtiketWorkflow etiketWorkflow;
    [SerializeField] private RectTransform etiketCanvasRoot;
    [SerializeField] private GameObject potContentVisual;
    [SerializeField] private Vector3 potContentFullScale = new Vector3(0.04f, 0.02f, 0.04f);

    [Header("Step 9 - Sudip -> Pot Workflow")]
    [Tooltip("StamperResidueController di Stamper (sisa salep di ujung). Auto-resolve jika kosong.")]
    [SerializeField] private StamperResidueController stamperResidue;
    [Tooltip("Visual salep menempel di ujung Sudip. Auto-dibuat jika kosong.")]
    [SerializeField] private SudipSalepVisual sudipSalepVisual;
    [Tooltip("Ujung Sudip (SudipTip) untuk menempelkan visual salep & deteksi. Auto-resolve.")]
    [SerializeField] private Transform sudipTip;
    [Tooltip("Tutup Pot Salep (BottleLid pada JarLidVisual). Wajib dibuka sebelum menuang. Auto-resolve.")]
    [SerializeField] private BottleLid potLid;

    [Header("Reset")]
    [SerializeField] private SimulationResetManager resetManager;

    [Header("Flow Tuning")]
    [Tooltip("Lama kondisi step harus terpenuhi sebelum dianggap selesai (detik).")]
    [SerializeField] private float stepStableSeconds = 0.35f;
    [Tooltip("Jeda transisi antar step setelah satu step selesai (detik).")]
    [SerializeField] private float stepGapSeconds = 0.6f;
    [Tooltip("DEBUG ONLY: lewati deteksi interaksi & maju otomatis. Biarkan OFF untuk play normal.")]
    [SerializeField] private bool debugAutoAdvance = false;

    [Header("Runtime State")]
    [SerializeField] private SalepStep currentStep = SalepStep.Idle;

    private float stableTimer;
    private bool isAnimating;
    private bool etiketBound;
    private bool etiketAttached;
    private float mortarBaselineMg;

    // --- Step 9 (salep -> pot) mini state machine ---
    private enum PotPhase { CleanStamper, OpenPot, DepositToPot, CloseLid }
    private PotPhase potPhase;

    // --- Per-step "timbang lalu tuang ke mortar" mini state machine (Sulfur & Vaselin) ---
    private enum WeighPourPhase { Weighing, Pouring }
    private WeighPourPhase weighPourPhase;
    private int batchesDoneInStep;
    private int batchesNeededInStep;
    private float batchTargetMg;
    private string currentIngredientId;
    private float pourBaselineMortarMg;

    private const int StepCount = 10;

    [Header("Step Text (editable dari Inspector)")]
    [Tooltip("Judul singkat tiap step (index 0..9). Kosongkan satu item untuk pakai default.")]
    [SerializeField]
    private string[] stepTitles =
    {
        "Pasang perkamen pada neraca",
        "Timbang Asam Salisilat 200 mg",
        "Masukkan Asam Salisilat ke Mortar",
        "Sulfur PP Batch 1/2: timbang 200 mg + tuang",
        "Sulfur PP Batch 2/2: timbang 200 mg + tuang",
        "Gerus serbuk hingga homogen",
        "Timbang Vaselin Album 10 g (2\u00d75 g)",
        "Aduk hingga jadi salep homogen",
        "Pindahkan salep ke Pot Salep",
        "Pasang etiket pada pot"
    };

    [Tooltip("Instruksi singkat tiap step (index 0..9).")]
    [TextArea]
    [SerializeField]
    private string[] stepInstructions =
    {
        "Ambil perkamen, letakkan di piring timbangan sampai stabil.",
        "Anak timbangan 200 mg di piring kanan, isi Asam Salisilat sampai seimbang.",
        "Ambil Asam Salisilat dari piring (trigger), tuang ke mortar, lalu reset timbangan.",
        "Batch 1/2: timbang Sulfur PP 200 mg di piring, lalu tuang ke mortar (trigger).",
        "Batch 2/2: timbang Sulfur PP 200 mg lagi, tuang ke mortar, lalu reset timbangan.",
        "Gerus serbuk dengan stamper sampai homogen.",
        "Timbang Vaselin Album 2\u00d7 5 g; tiap batch tuang ke mortar sebelum batch berikutnya, lalu reset timbangan.",
        "Aduk Vaselin dengan serbuk sampai jadi salep homogen.",
        "Tahan alat berisi salep di atas pot sampai berpindah.",
        "Pilih etiket lalu tempel ke pot salep."
    };

    private static readonly string[] DefaultTitles =
    {
        "Pasang perkamen pada neraca",
        "Timbang Asam Salisilat 200 mg",
        "Masukkan Asam Salisilat ke Mortar",
        "Sulfur PP Batch 1/2: timbang 200 mg + tuang",
        "Sulfur PP Batch 2/2: timbang 200 mg + tuang",
        "Gerus serbuk hingga homogen",
        "Timbang Vaselin Album 10 g (2\u00d75 g)",
        "Aduk hingga jadi salep homogen",
        "Pindahkan salep ke Pot Salep",
        "Pasang etiket pada pot"
    };

    private string GetStepTitle(int index)
    {
        if (stepTitles != null && index >= 0 && index < stepTitles.Length && !string.IsNullOrEmpty(stepTitles[index]))
            return stepTitles[index];
        if (index >= 0 && index < DefaultTitles.Length)
            return DefaultTitles[index];
        return $"Step {index + 1}";
    }

    private string GetStepInstruction(int index)
    {
        if (stepInstructions != null && index >= 0 && index < stepInstructions.Length && !string.IsNullOrEmpty(stepInstructions[index]))
            return stepInstructions[index];
        return GetStepTitle(index);
    }

    public SalepStep CurrentStep => currentStep;

    private float Tolerance => recipe != null ? Mathf.Max(0.001f, recipe.toleranceMg) : 1f;
    private float MixRequired => recipe != null ? Mathf.Clamp01(recipe.mixProgressRequired) : 1f;
    private float AsamTargetMg => recipe != null ? recipe.asamSalisilat.TargetTotalMg : 200f;
    private float SulfurTargetMg => recipe != null ? recipe.sulfurPP.TargetTotalMg : 400f;
    private float VaselinTargetMg => recipe != null ? recipe.vaselinAlbum.TargetTotalMg : 10000f;

    // Ukuran gundukan serbuk keseluruhan (0..1) berbasis isi mortar — MONOTONIK: tumbuh
    // dari Asam lalu Sulfur tanpa menyusut antar batch.
    private float PowderAmount01()
    {
        if (mortarController == null)
            return 1f;
        float total = AsamTargetMg + SulfurTargetMg;
        return total > 0f ? Mathf.Clamp01(mortarController.CurrentPowderMg / total) : 1f;
    }

    // Ukuran krim Vaselin (0..1) berbasis Vaselin yang sudah masuk mortar (di atas serbuk).
    private float CreamAmount01()
    {
        if (mortarController == null)
            return 1f;
        float vaselinIn = mortarController.CurrentPowderMg - (AsamTargetMg + SulfurTargetMg);
        return VaselinTargetMg > 0f ? Mathf.Clamp01(vaselinIn / VaselinTargetMg) : 1f;
    }

    private string AsamId => recipe != null ? recipe.asamSalisilat.ingredientId : "AsamSalisilat";
    private string SulfurId => recipe != null ? recipe.sulfurPP.ingredientId : "SulfurPP";
    private string VaselinId => recipe != null ? recipe.vaselinAlbum.ingredientId : "VaselinAlbum";

    private void OnEnable()
    {
        ResolveReferences();
        SetAllGuidance(false);
        DeactivateZones();
        UpdateChecklist();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        SetAllGuidance(false);
        DeactivateZones();
    }

    private void Update()
    {
        if (isAnimating)
            return;

        int index = GetStepIndex(currentStep);
        if (index < 0)
            return;

        if (debugAutoAdvance)
        {
            CompleteCurrentStep();
            return;
        }

        bool complete = EvaluateStep(currentStep);

        // Step etiket selesai lewat event, tidak butuh stable timer.
        if (currentStep == SalepStep.Step_10_AttachEtiket)
        {
            if (complete)
                CompleteCurrentStep();
            return;
        }

        if (complete)
        {
            stableTimer += Time.deltaTime;
            if (stableTimer >= stepStableSeconds)
                CompleteCurrentStep();
        }
        else
        {
            stableTimer = 0f;
        }
    }

    public void BeginSalepProcedure()
    {
        // Fallback runtime setup (visual bahan + HornSpoon). Idempotent.
        SalepIngredientRuntimeSetup.ConfigureScene();

        ResolveReferences();

        if (salepIngredientsRoot != null)
            salepIngredientsRoot.SetActive(true);

        if (stepUiRoot != null)
            stepUiRoot.SetActive(true);

        if (mortarController != null)
            mortarController.ResetMortar();

        if (potContentVisual != null)
            potContentVisual.SetActive(false);

        etiketAttached = false;

        ShowStep(SalepStep.Step_01_PrepareParchmentOnBalance);
    }

    public void ShowStep(SalepStep step)
    {
        currentStep = step;
        stableTimer = 0f;
        autoResetTriggeredStep = -1;
        SetAllGuidance(false);
        DeactivateZones();
        ApplyStepSetup(step);

        int stepIndex = GetStepIndex(step);
        if (stepIndex >= 0)
        {
            if (instructionText != null)
                instructionText.text = $"Step {stepIndex + 1}: {GetStepTitle(stepIndex)}";

            if (progressText != null)
                progressText.text = GetStepInstruction(stepIndex);

            SetGuidanceForStep(stepIndex, true);

            if (doneIcon != null)
                doneIcon.SetActive(false);
        }
        else if (step == SalepStep.Done)
        {
            if (instructionText != null)
                instructionText.text = "Pembuatan salep selesai";

            if (progressText != null)
                progressText.text = recipe != null
                    ? recipe.etiketCompletionDetail
                    : "Semua tahap prosedur Salep telah diselesaikan.";

            if (doneIcon != null)
                doneIcon.SetActive(true);

            if (bench != null)
                bench.SetMortarPhase(SalepMortarPhase.SalepHomogeneous, 0.05f);
        }

        UpdateChecklist();
    }

    // Evaluasi kondisi selesai step aktif + update teks progress. Return true jika selesai.
    private bool EvaluateStep(SalepStep step)
    {
        switch (step)
        {
            case SalepStep.Step_01_PrepareParchmentOnBalance:
                {
                    bool ready = HasParchmentReady();
                    if (progressText != null)
                        progressText.text = ready
                            ? "Perkamen sudah pada piring timbangan."
                            : GetStepInstruction(0);
                    return ready;
                }

            case SalepStep.Step_02_WeighAsamSalisilat:
                return EvaluateWeighing(2);

            case SalepStep.Step_03_MoveAsamToMortar:
                return EvaluateTransferToMortar(AsamTargetMg, recipe != null ? recipe.asamSalisilat.displayName : "Asam Salisilat", false);

            case SalepStep.Step_04_WeighSulfurPP:
                // Batch 1/2 Sulfur: timbang 200 mg lalu tuang ke mortar. Bobot tidak direset
                // (anak timbangan 200 mg dipakai lagi untuk batch 2 di Step 5).
                return EvaluateWeighAndPour(SulfurId, SulfurTargetMg * 0.5f, 1, 100f,
                    GetIngredientColor(SulfurId), SalepMortarPhase.PowderMix, false);

            case SalepStep.Step_05_MoveSulfurToMortar:
                // Batch 2/2 Sulfur: timbang 200 mg lalu tuang; di akhir wajib reset timbangan.
                return EvaluateWeighAndPour(SulfurId, SulfurTargetMg * 0.5f, 1, 100f,
                    GetIngredientColor(SulfurId), SalepMortarPhase.PowderMix, true);

            case SalepStep.Step_06_GrindPowders:
                return EvaluateMix(false);

            case SalepStep.Step_07_WeighVaselinAlbum:
                // Dua batch 5 g (pan maks 5 g): tiap batch ditimbang lalu dituang ke mortar.
                return EvaluateWeighAndPour(VaselinId, VaselinTargetMg * 0.5f, 2, 1000f,
                    GetIngredientColor(VaselinId), SalepMortarPhase.CreamAdded, true);

            case SalepStep.Step_08_MixOintment:
                return EvaluateMix(true);

            case SalepStep.Step_09_MoveOintmentToPot:
                return EvaluatePotTransfer();

            case SalepStep.Step_10_AttachEtiket:
                if (progressText != null && !etiketAttached)
                    progressText.text = GetStepInstruction(9);
                return etiketAttached;
        }

        return false;
    }

    private bool EvaluateWeighing(int humanStepNumber)
    {
        if (bench == null)
            return false;

        if (progressText != null && bench.IsWeighingActive)
        {
            string live = bench.GetWeighingProgressText();
            if (!string.IsNullOrEmpty(live))
                progressText.text = live;
        }

        return bench.WeighingTargetReached;
    }

    // Mini state machine reusable: untuk satu step, timbang 'batchTargetMg' di piring lalu
    // tuang ke mortar, ulangi 'batchesNeeded' kali. Mengembalikan true saat seluruh batch
    // pada step ini selesai. Tidak mengandalkan bench.WeighingTargetReached (pakai sub-target).
    private bool EvaluateWeighAndPour(
        string ingredientId,
        float batchTargetMg,
        int batchesNeeded,
        float pourStepMg,
        Color fxColor,
        SalepMortarPhase enterPhase,
        bool requireWeightResetAtEnd)
    {
        if (depositZone == null || mortarController == null)
            return false;

        string displayName = GetIngredientDisplayName(ingredientId);
        int batchNumber = batchesDoneInStep + 1; // 1-based batch yang sedang dikerjakan

        // ---------- FASE A: TIMBANG ----------
        if (weighPourPhase == WeighPourPhase.Weighing)
        {
            float cur = depositZone.DepositedMg;
            bool atTarget = depositZone.IsAtTargetMg(batchTargetMg, Tolerance) || cur >= batchTargetMg - Tolerance;

            if (!atTarget)
            {
                if (progressText != null)
                    progressText.text =
                        $"{displayName} \u2014 Batch {batchNumber}/{batchesNeeded}: timbang {FormatAmount(ingredientId, batchTargetMg)} " +
                        $"({FormatAmount(ingredientId, cur)}/{FormatAmount(ingredientId, batchTargetMg)})";
                return false;
            }

            // Sub-target tercapai → cue lalu beralih ke fase tuang.
            if (progressText != null)
                progressText.text = $"{displayName} \u2014 Batch {batchNumber}/{batchesNeeded} tepat! Tuang ke mortar.";

            EnterPourPhase(pourStepMg, fxColor, enterPhase);
            return false;
        }

        // ---------- FASE B: TUANG KE MORTAR ----------
        float moved = Mathf.Max(0f, mortarController.CurrentPowderMg - pourBaselineMortarMg);
        bool movedDone = moved >= batchTargetMg - Tolerance;
        bool panEmpty = depositZone.DepositedMg <= 0.1f;

        // Visual mortar: serbuk/cream bahan ini muncul & TUMBUH saat dituang.
        if (bench != null && moved > 0.5f)
        {
            float amt = enterPhase == SalepMortarPhase.CreamAdded ? CreamAmount01() : PowderAmount01();
            bench.SetMortarPhase(enterPhase, GetEnterPhaseFill(enterPhase), amt);
        }

        if (!(movedDone && panEmpty))
        {
            // Fase tuang: sorot mortar + arahkan arrow ke mortar.
            SetMortarMoveResetGuidance(false);
            if (progressText != null)
                progressText.text =
                    $"{displayName} \u2014 Batch {batchNumber}/{batchesNeeded}: tuang ke mortar " +
                    $"({FormatAmount(ingredientId, moved)}/{FormatAmount(ingredientId, batchTargetMg)}).\n" +
                    "Ambil bubuk dari piring (trigger), lalu tuang ke mortar.";
            return false;
        }

        bool isLastBatch = batchesDoneInStep + 1 >= batchesNeeded;

        // Reset anak timbangan hanya di akhir step (batch terakhir) jika diminta.
        if (isLastBatch && requireWeightResetAtEnd)
        {
            bool panCleared = IsRightPanCleared();
            SetMortarMoveResetGuidance(!panCleared);
            if (progressText != null)
                progressText.text = panCleared
                    ? $"{displayName} sudah masuk ke mortar dan timbangan sudah direset."
                    : $"{displayName} sudah masuk ke mortar.\nAnak timbangan masih di piring kanan \u2014 tekan tombol RESET timbangan.";
            if (!panCleared)
                return false;
        }

        // Batch ini selesai.
        batchesDoneInStep++;

        if (batchesDoneInStep >= batchesNeeded)
            return true; // seluruh step selesai

        // Masih ada batch berikutnya → kembali ke fase timbang dengan cue jelas.
        if (progressText != null)
            progressText.text =
                $"{displayName} \u2014 Batch {batchesDoneInStep + 1}/{batchesNeeded}: timbang {FormatAmount(ingredientId, batchTargetMg)} lagi.";

        BeginBatchWeighPhase(ingredientId, batchTargetMg);
        return false;
    }

    // Siapkan step untuk pola timbang-lalu-tuang: reset counter batch lalu mulai fase timbang.
    private void SetupWeighAndPour(string ingredientId, float perBatchTargetMg, int batchesNeeded)
    {
        currentIngredientId = ingredientId;
        batchTargetMg = perBatchTargetMg;
        batchesNeededInStep = Mathf.Max(1, batchesNeeded);
        batchesDoneInStep = 0;
        BeginBatchWeighPhase(ingredientId, perBatchTargetMg);
    }

    // Mulai (atau ulang) fase TIMBANG satu batch: piring menerima deposit dengan target
    // sub-batch, bahan ini boleh di-scoop dari jar, transfer pan→mortar dimatikan.
    private void BeginBatchWeighPhase(string ingredientId, float perBatchTargetMg)
    {
        weighPourPhase = WeighPourPhase.Weighing;

        // BeginWeighing menyiapkan tint, gating scoop, dan wajib anak timbangan kanan.
        // Lalu kita override MAX deposit ke sub-target batch (per-scoop dipertahankan).
        if (bench != null)
            bench.BeginWeighing(ingredientId);

        if (depositZone != null)
        {
            depositZone.SetDepositMg(0f);
            depositZone.ConfigureForRecipe(depositZone.DepositStepMg, perBatchTargetMg, perBatchTargetMg);
            depositZone.SetAcceptingDeposits(true);
        }

        // Pan→mortar OFF selama menimbang.
        if (spoonPlateTransfer != null)
            spoonPlateTransfer.SetTransferEnabled(false);
    }

    // Beralih ke fase TUANG: kunci deposit, aktifkan transfer pan→mortar, catat baseline mortar.
    private void EnterPourPhase(float pourStepMg, Color fxColor, SalepMortarPhase enterPhase)
    {
        weighPourPhase = WeighPourPhase.Pouring;

        pourBaselineMortarMg = mortarController != null ? mortarController.CurrentPowderMg : 0f;

        if (depositZone != null)
            depositZone.SetAcceptingDeposits(false);

        if (bench != null)
            bench.SetScoopableIngredient(null);

        if (spoonPlateTransfer != null)
        {
            spoonPlateTransfer.ConfigurePlateSource(depositZone, null);
            if (mortarController != null)
                spoonPlateTransfer.ConfigureMortarReceiver(mortarController, mortarController.transform);
            spoonPlateTransfer.SetTransferStepMg(pourStepMg);
            spoonPlateTransfer.SetFxColor(fxColor);
            // Vaselin = krim (gumpalan jatuh), bahan lain = debu serbuk.
            bool isVaselin = currentIngredientId == VaselinId;
            spoonPlateTransfer.SetCreamPourMode(isVaselin, fxColor);
            spoonPlateTransfer.SetTransferEnabled(true);
        }

        if (mortarTransferZone != null)
            mortarTransferZone.SetActive(false);

        // JANGAN tampilkan enterPhase di sini — itu membuat serbuk/krim bahan baru muncul
        // di mortar SEBELUM benar-benar dituang. Mortar tetap menampilkan isi sebelumnya
        // (di-set di ApplyStepSetup). enterPhase baru dipakai di FASE B saat moved > 0.5.
    }

    private static float GetEnterPhaseFill(SalepMortarPhase phase)
    {
        switch (phase)
        {
            case SalepMortarPhase.PowderMix:
                return 0f;   // serbuk masih terpisah (belum digerus)
            case SalepMortarPhase.CreamAdded:
                return 0.7f; // cream/vaselin masuk di atas serbuk
            default:
                return 0.5f;
        }
    }

    private string GetIngredientDisplayName(string ingredientId)
    {
        if (recipe != null)
        {
            SalepRecipeDefinition.Ingredient ing = recipe.GetById(ingredientId);
            if (ing != null && !string.IsNullOrEmpty(ing.displayName))
                return ing.displayName;
        }

        if (!string.IsNullOrEmpty(ingredientId))
        {
            if (ingredientId == SulfurId) return "Sulfur PP";
            if (ingredientId == VaselinId) return "Vaselin Album";
        }
        return "Asam Salisilat";
    }

    // Format jumlah sesuai bahan (Vaselin dalam gram, lainnya mg).
    private string FormatAmount(string ingredientId, float mg)
    {
        bool grams = !string.IsNullOrEmpty(ingredientId) && ingredientId == VaselinId;
        return grams ? $"{mg / 1000f:0.#} g" : $"{mg:0} mg";
    }

    private bool EvaluateTransferToMortar(float targetMg, string displayName, bool isSecondPowder)
    {
        // Sumber kemajuan = jumlah bubuk yang benar-benar masuk mortar (pola Sirup),
        // dihitung sebagai delta dari isi mortar saat step dimulai (aman kumulatif).
        float moved = mortarController != null
            ? Mathf.Max(0f, mortarController.CurrentPowderMg - mortarBaselineMg)
            : 0f;
        bool movedDone = moved >= targetMg - Tolerance;

        // Visual mortar tumbuh sesuai bubuk yang BENAR-BENAR sudah dituang — mulai KOSONG.
        // (Bug lama: serbuk langsung muncul saat masuk step walau 0 mg dituang.)
        if (bench != null)
        {
            float poured01 = targetMg > 0f ? Mathf.Clamp01(moved / targetMg) : 0f;
            if (moved <= 0.5f)
            {
                // Belum ada yang dituang. Bahan pertama (Asam) → mortar kosong. Bahan kedua
                // → mortar sudah berisi Asam dari step sebelumnya (jangan dikosongkan).
                bench.SetMortarPhase(
                    isSecondPowder ? SalepMortarPhase.AsamPowder : SalepMortarPhase.Empty,
                    isSecondPowder ? 0.5f : 0f);
            }
            else if (isSecondPowder)
            {
                // Serbuk kedua masuk → dua serbuk terpisah (Asam putih + Sulfur kuning).
                bench.SetMortarPhase(SalepMortarPhase.PowderMix, 0f);
            }
            else
            {
                // Serbuk pertama (Asam) masuk → mound putih tumbuh dari kecil ke penuh
                // mengikuti jumlah yang sudah benar-benar dituang.
                bench.SetMortarPhase(SalepMortarPhase.AsamPowder, Mathf.Clamp01(poured01));
            }
        }

        if (!movedDone)
        {
            // Fase 1: pindahkan bubuk ke mortar (sorot mortar).
            SetMortarMoveResetGuidance(false);
            if (progressText != null)
                progressText.text = $"{displayName} ke mortar: {moved:0} / {targetMg:0} mg.\nAmbil bubuk dari piring (trigger), lalu tuang ke mortar.";
            return false;
        }

        // Fase 2: bubuk sudah pindah → reset timbangan OTOMATIS (kembali sendiri).
        bool panCleared = IsRightPanCleared();
        if (!panCleared)
        {
            TriggerAutoBalanceReset();
            SetMortarMoveResetGuidance(false);
            if (progressText != null)
                progressText.text = $"{displayName} sudah masuk ke mortar.\nMereset timbangan otomatis\u2026";
            return false;
        }

        SetMortarMoveResetGuidance(false);
        if (progressText != null)
            progressText.text = $"{displayName} sudah masuk ke mortar dan timbangan sudah direset.";
        return true;
    }

    private bool IsRightPanCleared()
    {
        if (rightWeighingZone == null)
            return true;

        return rightWeighingZone.TotalGrams <= rightPanClearedGrams;
    }

    // Auto-reset timbangan saat step penimbangan selesai: anak timbangan kembali sendiri
    // ke tempatnya tanpa harus menekan tombol RESET manual. Hanya sekali per step.
    private void TriggerAutoBalanceReset()
    {
        int stepIndex = GetStepIndex(currentStep);
        if (autoResetTriggeredStep == stepIndex)
            return;

        if (balanceResetter == null)
            balanceResetter = FindFirstObjectByType<BalanceWeightResetter>(FindObjectsInactive.Include);

        if (balanceResetter != null)
        {
            balanceResetter.ResetAllWeights();
            autoResetTriggeredStep = stepIndex;
        }
    }

    // Fase 1 (pindah bubuk): sorot mortar. Fase 2 (reset): sorot tombol reset + arahkan arrow ke sana.
    private void SetMortarMoveResetGuidance(bool resetPhase)
    {
        int index = GetStepIndex(currentStep);

        if (stepHighlights != null && index >= 0 && index < stepHighlights.Length && stepHighlights[index] != null)
            SetHighlight(stepHighlights[index], !resetPhase);

        if (balanceResetHighlight != null)
            balanceResetHighlight.enabled = resetPhase;

        if (stepArrows == null || index < 0 || index >= stepArrows.Length || stepArrows[index] == null)
            return;

        WorldStepArrow arrow = stepArrows[index];
        if (resetPhase)
        {
            if (balanceResetTarget != null)
            {
                arrow.Configure(balanceResetTarget, "\u2193\nTekan RESET\ntimbangan", new Vector3(0f, 0.3f, 0f));
                arrow.SetVisible(true);
            }
        }
        else if (mortarController != null)
        {
            string ingredient = currentStep == SalepStep.Step_03_MoveAsamToMortar
                ? "Asam"
                : currentStep == SalepStep.Step_07_WeighVaselinAlbum
                    ? "Vaselin"
                    : "Sulfur";
            arrow.Configure(mortarController.transform, $"\u2193\nMortar\nMasukkan {ingredient}", new Vector3(0f, 0.42f, 0f));
            arrow.SetVisible(true);
        }
    }

    private bool EvaluateMix(bool isVaselinPhase)
    {
        if (mortarMixZone == null)
            return false;

        float p = mortarMixZone.Progress01;
        UpdateMixVisual(isVaselinPhase, p);

        if (progressText != null)
            progressText.text = $"Proses {(isVaselinPhase ? "mengaduk salep" : "menggerus serbuk")}: {p * 100f:0}%.\nGerakkan stamper memutar di dalam mortar.";

        return p >= MixRequired;
    }

    private bool EvaluatePotTransfer()
    {
        // FASE 1: Bersihkan ujung stamper pakai sudip.
        if (potPhase == PotPhase.CleanStamper)
        {
            bool cleaned = stamperResidue == null || stamperResidue.IsCleaned;
            if (!cleaned)
            {
                if (progressText != null)
                    progressText.text = "Stamper penuh salep di ujungnya.\nBersihkan dengan Sudip (pegang sudip, gosok ujung stamper).";
                return false;
            }

            // Sudah bersih → salep berpindah ke sudip (muncul visual di ujung sudip).
            if (sudipSalepVisual != null && !sudipSalepVisual.IsLoaded)
                sudipSalepVisual.Load();

            potPhase = PotPhase.OpenPot;
        }

        // FASE 2: Buka tutup Pot Salep.
        if (potPhase == PotPhase.OpenPot)
        {
            bool potOpen = potLid == null || potLid.IsOpen;
            if (!potOpen)
            {
                if (progressText != null)
                    progressText.text = "Salep sudah di Sudip.\nAmbil & dekatkan Pot Salep, lalu BUKA tutupnya.";
                return false;
            }

            // Pot terbuka → aktifkan zona dwell pot untuk menerima sudip.
            if (potTransferZone != null && !potTransferZone.IsActive)
            {
                potTransferZone.ResetZone();
                potTransferZone.SetRequiredIngredient(VaselinId, true);
                potTransferZone.SetActive(true);
            }

            potPhase = PotPhase.DepositToPot;
        }

        // FASE 3: Keruk salep dari mortar pakai sudip → tuang ke pot (dwell).
        // Mortar menyusut (sedikit demi sedikit) & pot terisi (sedikit → penuh) seiring progress.
        if (potPhase == PotPhase.DepositToPot)
        {
            if (potTransferZone == null)
                return false;

            float p = potTransferZone.Progress01;
            UpdatePotVisual(p);

            // Salep di ujung sudip ikut menyusut → kesan "terambil sedikit demi sedikit".
            if (sudipSalepVisual != null)
                sudipSalepVisual.SetFill(1f - p);

            if (progressText != null)
                progressText.text = $"Keruk salep dari mortar pakai Sudip lalu isi ke Pot: {p * 100f:0}%.\nTahan Sudip di atas Pot sampai penuh.";

            if (p < 0.999f)
                return false;

            // Pot penuh → sudip kosong, lanjut ke menutup pot.
            if (sudipSalepVisual != null)
                sudipSalepVisual.Unload();
            potPhase = PotPhase.CloseLid;
        }

        // FASE 4: Tutup Pot Salep sebelum step selesai (lanjut ke Etiket).
        // Jika lid tidak ada referensinya, jangan blokir (anggap tertutup).
        bool potClosed = potLid == null || !potLid.IsOpen;
        if (!potClosed)
        {
            if (progressText != null)
                progressText.text = "Pot sudah penuh salep.\nTUTUP kembali Pot Salep (pasang tutupnya) untuk lanjut ke Etiket.";
            return false;
        }

        return true;
    }

    private void UpdateMixVisual(bool isVaselinPhase, float progress)
    {
        if (bench == null)
            return;

        if (isVaselinPhase)
        {
            SalepMortarPhase phase = progress < 0.95f
                ? SalepMortarPhase.CreamAdded
                : SalepMortarPhase.SalepHomogeneous;
            bench.SetMortarPhase(phase, Mathf.Lerp(0.7f, 1f, progress));
        }
        else
        {
            // fill01 = tingkat homogen: 0 (terpisah) → 1 (menyatu). Saat menggerus,
            // mound putih & kuning saling mendekat lalu warnanya berbaur bertahap.
            SalepMortarPhase phase = progress < 0.95f
                ? SalepMortarPhase.PowderMix
                : SalepMortarPhase.PowdersHomogeneous;
            bench.SetMortarPhase(phase, progress);
        }
    }

    private void UpdatePotVisual(float progress)
    {
        if (bench != null)
            bench.SetMortarPhase(SalepMortarPhase.SalepHomogeneous, Mathf.Lerp(1f, 0.05f, progress));

        if (potContentVisual != null)
        {
            if (progress > 0.02f && !potContentVisual.activeSelf)
                potContentVisual.SetActive(true);
            potContentVisual.transform.localScale = potContentFullScale * Mathf.Clamp01(progress);
        }
    }

    private bool HasParchmentReady()
    {
        if (parchmentPan != null)
            return parchmentPan.HasParchment;

        WeightingZone[] zones = FindObjectsByType<WeightingZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (WeightingZone zone in zones)
        {
            if (zone != null && zone.HasParchment)
                return true;
        }
        return false;
    }

    public void CompleteCurrentStep()
    {
        int stepIndex = GetStepIndex(currentStep);
        if (stepIndex < 0)
            return;

        SetAllGuidance(false);
        DeactivateZones();

        if (doneIcon != null)
            doneIcon.SetActive(true);

        UpdateChecklist();

        SalepStep next = stepIndex >= StepCount - 1
            ? SalepStep.Done
            : (SalepStep)((int)SalepStep.Step_01_PrepareParchmentOnBalance + stepIndex + 1);

        StartCoroutine(AdvanceRoutine(next));
    }

    private IEnumerator AdvanceRoutine(SalepStep next)
    {
        isAnimating = true;
        yield return new WaitForSeconds(stepGapSeconds);
        isAnimating = false;
        ShowStep(next);
    }

    public void RequestBack()
    {
        if (resetManager == null)
            resetManager = GetComponentInParent<SimulationResetManager>(true);

        if (resetManager != null)
            resetManager.ResetAllAndReturnToMainMenu();
    }

    public void ResetProcedureStateFromGlobal()
    {
        StopAllCoroutines();
        currentStep = SalepStep.Idle;
        stableTimer = 0f;
        isAnimating = false;
        etiketAttached = false;

        SetAllGuidance(false);
        DeactivateZones();
        ResetZones();

        if (bench != null)
            bench.ResetAll();
        else if (SalepBench.Instance != null)
            SalepBench.Instance.ResetAll();

        if (mortarController != null)
            mortarController.ResetMortar();

        if (depositZone != null)
            depositZone.ResetDeposit();

        if (etiketWorkflow != null)
            etiketWorkflow.ResetWorkflow();

        if (potContentVisual != null)
            potContentVisual.SetActive(false);

        if (sudipSalepVisual != null)
            sudipSalepVisual.Unload();

        if (stamperResidue != null)
            stamperResidue.ClearResidue();

        if (doneIcon != null)
            doneIcon.SetActive(false);

        if (instructionText != null)
            instructionText.text = "Simulasi Salep belum dimulai";

        if (progressText != null)
            progressText.text = "Pilih Salep dari panel awal untuk membuka resep.";

        if (stepUiRoot != null)
            stepUiRoot.SetActive(false);

        UpdateChecklist();
    }

    private void UpdateChecklist()
    {
        if (checklistTexts == null)
            return;

        int activeIndex = GetStepIndex(currentStep);
        for (int index = 0; index < checklistTexts.Length; index++)
        {
            TMP_Text item = checklistTexts[index];
            if (item == null)
                continue;

            bool complete = currentStep == SalepStep.Done || (activeIndex >= 0 && index < activeIndex);
            bool active = activeIndex == index;
            string mark = complete ? "\u2713" : active ? "\u25b6" : "\u25a1";
            string title = index < StepCount ? GetStepTitle(index) : $"Step {index + 1}";
            item.text = $"{mark} {index + 1}. {title}";

            if (complete)
                item.fontStyle |= FontStyles.Strikethrough;
            else
                item.fontStyle &= ~FontStyles.Strikethrough;
        }
    }

    // Konfigurasi timbangan + zona + visual mortar sesuai step aktif.
    private void ApplyStepSetup(SalepStep step)
    {
        if (bench != null)
            bench.SetScoopableIngredient(null);

        switch (step)
        {
            case SalepStep.Step_01_PrepareParchmentOnBalance:
                if (bench != null)
                    bench.SetMortarPhase(SalepMortarPhase.Empty, 0f);
                break;

            case SalepStep.Step_02_WeighAsamSalisilat:
                bench?.BeginWeighing(AsamId);
                break;

            case SalepStep.Step_03_MoveAsamToMortar:
                // Mortar KOSONG saat masuk step. Serbuk baru muncul saat benar-benar dituang
                // (lihat EvaluateTransferToMortar — visual tumbuh sesuai jumlah yang masuk).
                bench?.SetMortarPhase(SalepMortarPhase.Empty, 0f);
                ActivateMortarTransfer(AsamId);
                break;

            case SalepStep.Step_04_WeighSulfurPP:
                // Batch 1/2 Sulfur. Mortar masih berisi Asam (putih) dari Step 3.
                bench?.SetMortarPhase(SalepMortarPhase.AsamPowder, 0.5f);
                SetupWeighAndPour(SulfurId, SulfurTargetMg * 0.5f, 1);
                break;

            case SalepStep.Step_05_MoveSulfurToMortar:
                // Batch 2/2 Sulfur. Mortar sudah berisi Asam + 200 mg Sulfur dari batch 1.
                bench?.SetMortarPhase(SalepMortarPhase.PowderMix, 0f);
                SetupWeighAndPour(SulfurId, SulfurTargetMg * 0.5f, 1);
                break;

            case SalepStep.Step_06_GrindPowders:
                bench?.SetMortarPhase(SalepMortarPhase.PowderMix, 0.5f);
                ActivateMixZone();
                break;

            case SalepStep.Step_07_WeighVaselinAlbum:
                // Dua batch 5 g (pan maks 5 g). Mortar berisi serbuk homogen dari Step 6.
                bench?.SetMortarPhase(SalepMortarPhase.PowdersHomogeneous, 1f);
                SetupWeighAndPour(VaselinId, VaselinTargetMg * 0.5f, 2);
                break;

            case SalepStep.Step_08_MixOintment:
                bench?.SetMortarPhase(SalepMortarPhase.CreamAdded, 0.7f);
                ActivateMixZone();
                break;

            case SalepStep.Step_09_MoveOintmentToPot:
                bench?.SetMortarPhase(SalepMortarPhase.SalepHomogeneous, 1f);
                ActivatePotTransfer();
                break;

            case SalepStep.Step_10_AttachEtiket:
                bench?.SetMortarPhase(SalepMortarPhase.SalepHomogeneous, 0.05f);
                BeginEtiket();
                break;
        }
    }

    private void ActivateMortarTransfer(string ingredientId)
    {
        // Catat isi mortar saat ini → progres step dihitung dari delta (kumulatif aman
        // untuk Step 3 Asam lalu Step 5 Sulfur yang menumpuk di mortar yang sama).
        mortarBaselineMg = mortarController != null ? mortarController.CurrentPowderMg : 0f;

        // Saat memindahkan ke mortar, piring TIDAK menerima deposit baru (cegah dobel mass).
        if (depositZone != null)
            depositZone.SetAcceptingDeposits(false);

        // Pola Sirup: pengambilan bubuk dari piring pakai TRIGGER di sendok
        // (L Mouse di editor / trigger controller di VR), bukan dwell.
        if (spoonPlateTransfer != null)
        {
            spoonPlateTransfer.ConfigurePlateSource(depositZone, null);
            if (mortarController != null)
                spoonPlateTransfer.ConfigureMortarReceiver(mortarController, mortarController.transform);
            spoonPlateTransfer.SetTransferStepMg(GetScoopStepMg(ingredientId));
            spoonPlateTransfer.SetFxColor(GetIngredientColor(ingredientId));
            spoonPlateTransfer.SetTransferEnabled(true);
        }

        // Dwell zone lama dinonaktifkan: collider-nya ikut skala mortar (~45x) sehingga
        // jadi kotak raksasa yang offset dari mangkuk dan tidak andal.
        if (mortarTransferZone != null)
            mortarTransferZone.SetActive(false);
    }

    // Jumlah bubuk per trigger dari piring sesuai bahan (Asam 50 mg, Sulfur 100 mg).
    private float GetScoopStepMg(string ingredientId)
    {
        if (!string.IsNullOrEmpty(ingredientId) && ingredientId == SulfurId)
            return 100f;
        return 50f;
    }

    // Warna FX/visual per bahan (Asam putih, Sulfur kuning, Vaselin ivory).
    private Color GetIngredientColor(string ingredientId)
    {
        if (recipe != null)
        {
            SalepRecipeDefinition.Ingredient ing = recipe.GetById(ingredientId);
            if (ing != null)
                return ing.color;
        }

        if (!string.IsNullOrEmpty(ingredientId) && ingredientId == SulfurId)
            return new Color(1f, 0.9f, 0.46f, 1f);
        return new Color(0.97f, 0.975f, 0.96f, 1f);
    }

    private void ActivateMixZone()
    {
        if (mortarMixZone == null)
            return;

        mortarMixZone.ResetZone();
        if (stamper != null)
            mortarMixZone.ConfigureStamper(stamper);
        mortarMixZone.SetActive(true);

        // Panduan memutar (pola Sirup): lingkaran + indikator berputar di atas mortar.
        if (mortarStirGuide != null)
        {
            if (mortarController != null)
                mortarStirGuide.SetTarget(mortarController.transform);
            mortarStirGuide.SetVisible(true);
        }
    }

    private void ActivatePotTransfer()
    {
        // Mulai dari fase bersihkan stamper. Salep jadi masih ada di mortar/ujung stamper.
        potPhase = PotPhase.CleanStamper;

        if (bench != null)
            bench.SetMortarPhase(SalepMortarPhase.SalepHomogeneous, 1f);

        // Tampilkan sisa salep di ujung stamper untuk dibersihkan pakai sudip.
        // Warnai jadi salep (kuning) + sedikit perbesar — bukan putih Difenhidramin.
        if (stamperResidue != null)
        {
            stamperResidue.ApplySalepAppearance(new Color(0.80f, 0.60f, 0.22f), 1.6f);
            stamperResidue.ShowResidue();
        }

        // Sudip masih kosong; pot belum perlu zona dwell sampai fase deposit.
        if (sudipSalepVisual != null)
            sudipSalepVisual.Unload();

        if (potTransferZone != null)
        {
            potTransferZone.ResetZone();
            potTransferZone.SetRequiredIngredient(VaselinId, true);
            potTransferZone.SetActive(false);
        }

        if (potContentVisual != null)
            potContentVisual.SetActive(false);
    }

    private bool potContentLookApplied;

    // Set material isi pot ke krim salep pucat UNLIT supaya warnanya konsisten & terlihat
    // jelas (Lit + lampu terang bikin "blown out" putih). Dipanggil sekali.
    private void ApplyPotContentSalepLook()
    {
        if (potContentLookApplied || potContentVisual == null)
            return;

        var mr = potContentVisual.GetComponent<MeshRenderer>();
        if (mr == null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            return;

        Color salep = new Color(0.94f, 0.89f, 0.62f, 1f);
        var mat = new Material(shader) { name = "Runtime_SalepPotContent_Unlit" };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", salep);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", salep);

        Texture2D creamTex = Resources.Load<Texture2D>("SalepTex/cream_surface");
        if (creamTex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", creamTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", creamTex);
        }
        mr.sharedMaterial = mat;
        potContentLookApplied = true;
    }

    private void BeginEtiket()
    {
        if (etiketWorkflow == null)
        {
            if (progressText != null)
                progressText.text = "EtiketWorkflow Salep belum tersambung di Inspector.";
            return;
        }

        etiketAttached = false;

        if (!etiketBound)
        {
            etiketWorkflow.LabelAttached += HandleEtiketAttached;
            etiketWorkflow.BackRequested += RequestBack;
            etiketBound = true;
        }

        if (recipe != null)
            etiketWorkflow.ConfigureContent(recipe.etiketProductLine, recipe.etiketCompletionDetail);

        etiketWorkflow.BeginLabelSelection(etiketCanvasRoot, potSalep);
    }

    private void HandleEtiketAttached()
    {
        etiketAttached = true;
        if (etiketWorkflow != null)
            etiketWorkflow.ShowSuccess();
    }

    private void DeactivateZones()
    {
        if (mortarTransferZone != null)
            mortarTransferZone.SetActive(false);
        if (mortarMixZone != null)
            mortarMixZone.SetActive(false);
        if (potTransferZone != null)
            potTransferZone.SetActive(false);
        if (spoonPlateTransfer != null)
        {
            spoonPlateTransfer.SetTransferEnabled(false);
            spoonPlateTransfer.ClearFxColor();
        }
        if (mortarStirGuide != null)
            mortarStirGuide.SetVisible(false);
    }

    private void ResetZones()
    {
        if (mortarTransferZone != null)
            mortarTransferZone.ResetZone();
        if (mortarMixZone != null)
            mortarMixZone.ResetZone();
        if (potTransferZone != null)
            potTransferZone.ResetZone();
    }

    private void SetGuidanceForStep(int index, bool active)
    {
        if (stepHighlights != null && index < stepHighlights.Length && stepHighlights[index] != null)
            SetHighlight(stepHighlights[index], active);

        if (stepArrows != null && index < stepArrows.Length && stepArrows[index] != null)
            stepArrows[index].SetVisible(active);
    }

    // Nyalakan/matikan outline penanda step. Jika target punya HoverOutlineController
    // (mis. toples bahan), kunci outline lewat SetProcedureHold supaya event hover/grab
    // tidak mematikannya — outline tetap jadi penanda sepanjang step aktif.
    private void SetHighlight(Outlinable outline, bool active)
    {
        if (outline == null)
            return;

        HoverOutlineController hover = outline.GetComponent<HoverOutlineController>();
        if (hover != null)
            hover.SetProcedureHold(active);
        else
            outline.enabled = active;
    }

    private void SetAllGuidance(bool active)
    {
        if (stepHighlights != null)
        {
            foreach (Outlinable highlight in stepHighlights)
            {
                if (highlight != null)
                    SetHighlight(highlight, active);
            }
        }

        if (stepArrows != null)
        {
            foreach (WorldStepArrow arrow in stepArrows)
            {
                if (arrow != null)
                    arrow.SetVisible(active);
            }
        }

        if (balanceResetHighlight != null)
            balanceResetHighlight.enabled = active;
    }

    private void ResolveReferences()
    {
        if (resetManager == null)
            resetManager = GetComponentInParent<SimulationResetManager>(true);

        if (bench == null)
            bench = SalepBench.Instance != null
                ? SalepBench.Instance
                : FindFirstObjectByType<SalepBench>(FindObjectsInactive.Include);

        if (mortarController == null)
        {
            if (mortarSet != null)
                mortarController = mortarSet.GetComponentInChildren<MortarController>(true);
            if (mortarController == null)
                mortarController = FindFirstObjectByType<MortarController>(FindObjectsInactive.Include);
        }

        if (depositZone == null)
            depositZone = FindFirstObjectByType<PowderDepositZone>(FindObjectsInactive.Include);

        if (parchmentPan == null && depositZone != null)
            parchmentPan = depositZone.GetComponent<WeightingZone>();

        if (rightWeighingZone == null)
        {
            WeightingZone[] zones = FindObjectsByType<WeightingZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (WeightingZone zone in zones)
            {
                if (zone != null && zone.name == "Collider_Piring_Kanan")
                {
                    rightWeighingZone = zone;
                    break;
                }
            }
        }

        // Auto-assign zona berdasarkan mode jika belum di-wire di Inspector.
        if (mortarTransferZone == null || potTransferZone == null)
        {
            SalepTransferZone[] zones = FindObjectsByType<SalepTransferZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (SalepTransferZone zone in zones)
            {
                if (zone == null)
                    continue;
                if (zone.Mode == SalepTransferZone.TransferMode.SpoonPowderToMortar && mortarTransferZone == null)
                    mortarTransferZone = zone;
                else if (zone.Mode == SalepTransferZone.TransferMode.DwellToPot && potTransferZone == null)
                    potTransferZone = zone;
            }
        }

        if (mortarMixZone == null)
            mortarMixZone = FindFirstObjectByType<SalepMortarMixZone>(FindObjectsInactive.Include);

        if (spoonPlateTransfer == null)
        {
            if (hornSpoon != null)
                spoonPlateTransfer = hornSpoon.GetComponent<SpoonPowderPlateTransfer>();
            if (spoonPlateTransfer == null)
                spoonPlateTransfer = FindFirstObjectByType<SpoonPowderPlateTransfer>(FindObjectsInactive.Include);
        }

        if (mortarStirGuide == null)
        {
            MortarStirGuide[] guides = FindObjectsByType<MortarStirGuide>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MortarStirGuide guide in guides)
            {
                if (guide == null)
                    continue;
                if (guide.name == "VIS_MortarStirGuide")
                {
                    mortarStirGuide = guide;
                    break;
                }
                if (mortarStirGuide == null)
                    mortarStirGuide = guide;
            }
        }

        if (etiketWorkflow == null)
            etiketWorkflow = GetComponent<EtiketWorkflow>();

        // --- Step 9 Sudip -> Pot workflow ---
        if (stamperResidue == null && stamper != null)
            stamperResidue = stamper.GetComponent<StamperResidueController>();
        if (stamperResidue == null)
            stamperResidue = FindFirstObjectByType<StamperResidueController>(FindObjectsInactive.Include);
        if (stamperResidue != null && mortarController != null)
            stamperResidue.BindMortar(mortarController);

        if (sudipTip == null && sudip != null)
        {
            Transform tip = sudip.Find("SudipTip");
            sudipTip = tip != null ? tip : sudip;
        }

        if (sudipSalepVisual == null && sudip != null)
        {
            sudipSalepVisual = sudip.GetComponentInChildren<SudipSalepVisual>(true);
            if (sudipSalepVisual == null)
                sudipSalepVisual = sudip.gameObject.AddComponent<SudipSalepVisual>();
        }
        if (sudipSalepVisual != null)
            sudipSalepVisual.Configure(sudipTip);

        if (potLid == null && potSalep != null)
            potLid = potSalep.GetComponentInChildren<BottleLid>(true);
    }

    private static int GetStepIndex(SalepStep step)
    {
        int index = (int)step - (int)SalepStep.Step_01_PrepareParchmentOnBalance;
        return index >= 0 && index < StepCount ? index : -1;
    }
}
