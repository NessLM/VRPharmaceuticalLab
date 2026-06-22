using EPOOutline;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class SalepProcedureManager : MonoBehaviour
{
    public enum SalepStep
    {
        Idle,
        Intro,
        Step_01_PrepareParchmentOnBalance,
        Step_02_WeighAsamSalisilat200mg,
        Step_03_MoveAsamSalisilatToMortar,
        Step_04_ResetBalance,
        Step_05_WeighSulfurPP400mg,
        Step_06_MoveSulfurPPToMortar,
        Step_07_MixPowdersInMortar,
        Step_08_WeighVaselinAlbum,
        Step_09_MixVaselinWithPowders,
        Step_10_MoveOintmentToPot,
        Done
    }

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

    [Header("Reset")]
    [SerializeField] private SimulationResetManager resetManager;

    [Header("Step Flow")]
    [Tooltip("SCAFFOLD sementara: step yang belum punya deteksi interaksi (perkamen, " +
             "pindah ke mortar, mixing, isi pot) maju otomatis setelah jeda. Matikan kalau " +
             "deteksi interaksi sudah dibuat. Step menimbang (2/5/8) TIDAK ikut ini — " +
             "mereka maju saat target gram tercapai.")]
    [SerializeField] private bool autoAdvanceNonInteractiveSteps = true;
    [SerializeField] private float gapStepSeconds = 5f;

    [Header("Runtime State")]
    [SerializeField] private SalepStep currentStep = SalepStep.Idle;

    private float stepEnterTime;

    private static readonly string[] StepTitles =
    {
        "Siapkan perkamen di timbangan",
        "Timbang Asam Salisilat 200 mg",
        "Pindahkan Asam Salisilat ke mortar",
        "Reset timbangan",
        "Timbang Sulfur PP 400 mg",
        "Pindahkan Sulfur PP ke mortar",
        "Campur dua bubuk di mortar",
        "Timbang Vaselin Album",
        "Campur Vaselin dengan bubuk",
        "Masukkan salep ke pot"
    };

    private static readonly string[] StepInstructions =
    {
        "Ambil kertas perkamen dan letakkan pada piring timbangan sampai posisinya stabil.",
        "Gunakan anak timbangan dan timbang Asam Salisilat hingga mencapai 200 mg.",
        "Pindahkan seluruh Asam Salisilat yang sudah ditimbang ke dalam mortar.",
        "Tekan tombol reset timbangan dan pastikan nilai serta posisi neraca kembali netral.",
        "Gunakan perkamen bersih lalu timbang Sulfur PP hingga mencapai 400 mg.",
        "Pindahkan seluruh Sulfur PP yang sudah ditimbang ke dalam mortar.",
        "Aduk Asam Salisilat dan Sulfur PP di mortar sampai campuran bubuk merata.",
        "Timbang Vaselin Album sebagai basis hingga bobot akhir formula mencapai 10 gram.",
        "Masukkan Vaselin Album bertahap dan aduk sampai terbentuk salep yang homogen.",
        "Pindahkan seluruh salep dari mortar ke pot salep bening sampai selesai."
    };

    public SalepStep CurrentStep => currentStep;

    private void OnEnable()
    {
        if (resetManager == null)
            resetManager = GetComponentInParent<SimulationResetManager>(true);

        SetAllGuidance(false);
        UpdateChecklist();
    }

    private void Update()
    {
        SalepBench bench = SalepBench.Instance;
        if (bench == null)
            return;

        // Selama step menimbang, progressText menampilkan amount live dari timbangan
        // (mis. "Asam Salisilat: 50 / 200 mg"), menimpa kalimat instruksi detail.
        if (progressText != null && bench.IsWeighingActive)
        {
            string progress = bench.GetWeighingProgressText();
            if (!string.IsNullOrEmpty(progress))
                progressText.text = progress;
        }

        // Auto-advance step menimbang saat target tercapai (Step 2/5/8).
        if (IsWeighingStep(currentStep) && bench.WeighingTargetReached)
        {
            CompleteCurrentStep();
            return;
        }

        // Scaffold: step non-interaktif maju otomatis setelah jeda agar prosedur mengalir
        // sampai deteksi interaksi sungguhan dibuat.
        if (autoAdvanceNonInteractiveSteps &&
            IsGapStep(currentStep) &&
            Time.time - stepEnterTime >= gapStepSeconds)
        {
            CompleteCurrentStep();
        }
    }

    private static bool IsWeighingStep(SalepStep step)
    {
        return step == SalepStep.Step_02_WeighAsamSalisilat200mg
            || step == SalepStep.Step_05_WeighSulfurPP400mg
            || step == SalepStep.Step_08_WeighVaselinAlbum;
    }

    // Step tanpa deteksi interaksi yang dimajukan otomatis oleh scaffold.
    private static bool IsGapStep(SalepStep step)
    {
        return step == SalepStep.Step_01_PrepareParchmentOnBalance
            || step == SalepStep.Step_03_MoveAsamSalisilatToMortar
            || step == SalepStep.Step_04_ResetBalance
            || step == SalepStep.Step_06_MoveSulfurPPToMortar
            || step == SalepStep.Step_07_MixPowdersInMortar
            || step == SalepStep.Step_09_MixVaselinWithPowders
            || step == SalepStep.Step_10_MoveOintmentToPot;
    }

    public void BeginSalepProcedure()
    {
        // Fallback runtime setup jika editor repair belum pernah dijalankan.
        // Tidak mengubah layout; hanya memastikan IngredientVisualProfile & HornSpoon terkonfigurasi.
        SalepIngredientRuntimeSetup.ConfigureScene();

        if (salepIngredientsRoot != null)
            salepIngredientsRoot.SetActive(true);

        if (stepUiRoot != null)
            stepUiRoot.SetActive(true);

        ShowStep(SalepStep.Step_01_PrepareParchmentOnBalance);
    }

    public void ShowStep(SalepStep step)
    {
        currentStep = step;
        stepEnterTime = Time.time;
        SetAllGuidance(false);
        ApplyBenchForStep(step);

        int stepIndex = GetStepIndex(step);
        if (stepIndex >= 0)
        {
            if (instructionText != null)
                instructionText.text = $"Step {stepIndex + 1}: {StepTitles[stepIndex]}";

            if (progressText != null)
                progressText.text = StepInstructions[stepIndex];

            SetGuidanceForStep(stepIndex, true);

            if (doneIcon != null)
                doneIcon.SetActive(false);
        }
        else if (step == SalepStep.Done)
        {
            if (instructionText != null)
                instructionText.text = "Pembuatan salep selesai";

            if (progressText != null)
                progressText.text = "Semua tahap fondasi prosedur Salep telah diselesaikan.";

            if (doneIcon != null)
                doneIcon.SetActive(true);
        }

        UpdateChecklist();
    }

    public void CompleteCurrentStep()
    {
        int stepIndex = GetStepIndex(currentStep);
        if (stepIndex < 0)
            return;

        SalepStep next = stepIndex >= StepTitles.Length - 1
            ? SalepStep.Done
            : (SalepStep)((int)SalepStep.Step_01_PrepareParchmentOnBalance + stepIndex + 1);

        ShowStep(next);
    }

    public void RequestBack()
    {
        if (resetManager != null)
            resetManager.ResetAllAndReturnToMainMenu();
    }

    public void ResetProcedureStateFromGlobal()
    {
        StopAllCoroutines();
        currentStep = SalepStep.Idle;
        SetAllGuidance(false);

        if (SalepBench.Instance != null)
            SalepBench.Instance.ResetAll();

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
            string title = index < StepTitles.Length ? StepTitles[index] : $"Step {index + 1}";
            item.text = $"{mark} {index + 1}. {title}";
        }
    }

    // Drive SalepBench (timbangan + visual mortar) sesuai step aktif.
    // Defensif: bench mungkin belum ada jika ConfigureScene belum jalan.
    private void ApplyBenchForStep(SalepStep step)
    {
        SalepBench bench = SalepBench.Instance;
        if (bench == null)
            return;

        // Default: tidak ada bahan yang boleh di-scoop. Step menimbang akan
        // mengaktifkan bahannya sendiri lewat BeginWeighing.
        bench.SetScoopableIngredient(null);

        switch (step)
        {
            case SalepStep.Step_02_WeighAsamSalisilat200mg:
                bench.BeginWeighing("AsamSalisilat");
                break;

            case SalepStep.Step_03_MoveAsamSalisilatToMortar:
                bench.SetMortarPhase(SalepMortarPhase.AsamPowder, 0.35f);
                bench.ClearPan();
                break;

            case SalepStep.Step_04_ResetBalance:
                bench.ClearPan();
                break;

            case SalepStep.Step_05_WeighSulfurPP400mg:
                bench.BeginWeighing("SulfurPP");
                break;

            case SalepStep.Step_06_MoveSulfurPPToMortar:
                bench.SetMortarPhase(SalepMortarPhase.PowderMix, 0.55f);
                bench.ClearPan();
                break;

            case SalepStep.Step_07_MixPowdersInMortar:
                bench.SetMortarPhase(SalepMortarPhase.PowdersHomogeneous, 0.55f);
                break;

            case SalepStep.Step_08_WeighVaselinAlbum:
                bench.BeginWeighing("VaselinAlbum");
                break;

            case SalepStep.Step_09_MixVaselinWithPowders:
                bench.SetMortarPhase(SalepMortarPhase.CreamAdded, 0.8f);
                break;

            case SalepStep.Step_10_MoveOintmentToPot:
                bench.SetMortarPhase(SalepMortarPhase.SalepHomogeneous, 1f);
                break;

            case SalepStep.Done:
                bench.SetMortarPhase(SalepMortarPhase.SalepHomogeneous, 1f);
                break;
        }
    }

    private void SetGuidanceForStep(int index, bool active)
    {
        if (stepHighlights != null && index < stepHighlights.Length && stepHighlights[index] != null)
            stepHighlights[index].enabled = active;

        if (stepArrows != null && index < stepArrows.Length && stepArrows[index] != null)
            stepArrows[index].gameObject.SetActive(active);
    }

    private void SetAllGuidance(bool active)
    {
        if (stepHighlights != null)
        {
            foreach (Outlinable highlight in stepHighlights)
            {
                if (highlight != null)
                    highlight.enabled = active;
            }
        }

        if (stepArrows != null)
        {
            foreach (WorldStepArrow arrow in stepArrows)
            {
                if (arrow != null)
                    arrow.gameObject.SetActive(active);
            }
        }
    }

    private static int GetStepIndex(SalepStep step)
    {
        int index = (int)step - (int)SalepStep.Step_01_PrepareParchmentOnBalance;
        return index >= 0 && index < StepTitles.Length ? index : -1;
    }
}
