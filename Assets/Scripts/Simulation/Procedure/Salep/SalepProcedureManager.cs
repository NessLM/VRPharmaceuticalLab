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

    [Header("Interaction Zones (snap/proximity)")]
    [SerializeField] private SalepTransferZone mortarTransferZone;
    [SerializeField] private SalepMortarMixZone mortarMixZone;
    [SerializeField] private SalepTransferZone potTransferZone;

    [Header("Etiket (Step 10)")]
    [SerializeField] private EtiketWorkflow etiketWorkflow;
    [SerializeField] private RectTransform etiketCanvasRoot;
    [SerializeField] private GameObject potContentVisual;
    [SerializeField] private Vector3 potContentFullScale = new Vector3(0.04f, 0.02f, 0.04f);

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

    private static readonly string[] StepTitles =
    {
        "Siapkan perkamen di timbangan",
        "Timbang Asam Salisilat 200 mg",
        "Pindahkan Asam Salisilat ke mortar",
        "Timbang Sulfur PP 400 mg",
        "Pindahkan Sulfur PP ke mortar",
        "Gerus campuran serbuk di mortar",
        "Timbang & tambahkan Vaselin Album",
        "Aduk sampai jadi salep homogen",
        "Pindahkan salep ke pot",
        "Pasang etiket pada pot"
    };

    private static readonly string[] StepInstructions =
    {
        "Ambil kertas perkamen dan letakkan pada piring timbangan sampai posisinya stabil.",
        "Letakkan anak timbangan 200 mg di piring kanan, lalu isi Asam Salisilat ke piring kiri per 50 mg sampai seimbang.",
        "Dekatkan sendok ke mortar untuk memindahkan Asam Salisilat, lalu tekan tombol reset timbangan.",
        "Letakkan anak timbangan 400 mg di piring kanan, lalu isi Sulfur PP ke piring kiri per 100 mg sampai seimbang.",
        "Dekatkan sendok ke mortar untuk memindahkan Sulfur PP, lalu tekan tombol reset timbangan.",
        "Gerus dua serbuk di mortar dengan stamper memutar sampai campuran homogen.",
        "Letakkan anak timbangan sesuai target di piring kanan, lalu ambil Vaselin Album per 2 g sampai 9,4 g (scoop terakhir otomatis 1,4 g).",
        "Aduk Vaselin dengan serbuk pakai stamper sampai terbentuk salep ivory homogen.",
        "Tahan sendok/mortar berisi salep di atas pot sampai seluruh salep berpindah.",
        "Pilih & isi etiket, lalu tempelkan ke pot salep sampai tersnap."
    };

    public SalepStep CurrentStep => currentStep;

    private float Tolerance => recipe != null ? Mathf.Max(0.001f, recipe.toleranceMg) : 1f;
    private float MixRequired => recipe != null ? Mathf.Clamp01(recipe.mixProgressRequired) : 1f;
    private float AsamTargetMg => recipe != null ? recipe.asamSalisilat.TargetTotalMg : 200f;
    private float SulfurTargetMg => recipe != null ? recipe.sulfurPP.TargetTotalMg : 400f;
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
        SetAllGuidance(false);
        DeactivateZones();
        ApplyStepSetup(step);

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
                            : StepInstructions[0];
                    return ready;
                }

            case SalepStep.Step_02_WeighAsamSalisilat:
                return EvaluateWeighing(2);

            case SalepStep.Step_03_MoveAsamToMortar:
                return EvaluateTransferToMortar(AsamTargetMg, recipe != null ? recipe.asamSalisilat.displayName : "Asam Salisilat");

            case SalepStep.Step_04_WeighSulfurPP:
                return EvaluateWeighing(4);

            case SalepStep.Step_05_MoveSulfurToMortar:
                return EvaluateTransferToMortar(SulfurTargetMg, recipe != null ? recipe.sulfurPP.displayName : "Sulfur PP");

            case SalepStep.Step_06_GrindPowders:
                return EvaluateMix(false);

            case SalepStep.Step_07_WeighVaselinAlbum:
                return EvaluateWeighing(7);

            case SalepStep.Step_08_MixOintment:
                return EvaluateMix(true);

            case SalepStep.Step_09_MoveOintmentToPot:
                return EvaluatePotTransfer();

            case SalepStep.Step_10_AttachEtiket:
                if (progressText != null && !etiketAttached)
                    progressText.text = StepInstructions[9];
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

    private bool EvaluateTransferToMortar(float targetMg, string displayName)
    {
        if (mortarTransferZone == null)
            return false;

        float moved = mortarTransferZone.ReceivedMg;
        bool movedDone = moved >= targetMg - Tolerance;

        if (!movedDone)
        {
            // Fase 1: pindahkan bubuk ke mortar (sorot mortar).
            SetMortarMoveResetGuidance(false);
            if (progressText != null)
                progressText.text = $"{displayName} ke mortar: {moved:0} / {targetMg:0} mg.\nDekatkan sendok ke mortar.";
            return false;
        }

        // Fase 2: bubuk sudah pindah, tapi anak timbangan masih di piring. Suruh reset.
        bool panCleared = IsRightPanCleared();
        SetMortarMoveResetGuidance(!panCleared);

        if (progressText != null)
            progressText.text = panCleared
                ? $"{displayName} sudah masuk ke mortar dan timbangan sudah direset."
                : $"{displayName} sudah masuk ke mortar.\nAnak timbangan masih di piring kanan — tekan tombol RESET timbangan.";

        return panCleared;
    }

    private bool IsRightPanCleared()
    {
        if (rightWeighingZone == null)
            return true;

        return rightWeighingZone.TotalGrams <= rightPanClearedGrams;
    }

    // Fase 1 (pindah bubuk): sorot mortar. Fase 2 (reset): sorot tombol reset + arahkan arrow ke sana.
    private void SetMortarMoveResetGuidance(bool resetPhase)
    {
        int index = GetStepIndex(currentStep);

        if (stepHighlights != null && index >= 0 && index < stepHighlights.Length && stepHighlights[index] != null)
            stepHighlights[index].enabled = !resetPhase;

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
            string ingredient = currentStep == SalepStep.Step_03_MoveAsamToMortar ? "Asam" : "Sulfur";
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
        if (potTransferZone == null)
            return false;

        float p = potTransferZone.Progress01;
        UpdatePotVisual(p);

        if (progressText != null)
            progressText.text = $"Memindahkan salep ke pot: {p * 100f:0}%.\nTahan sendok/mortar berisi salep di atas pot.";

        return p >= 0.999f;
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
            SalepMortarPhase phase = progress < 0.95f
                ? SalepMortarPhase.PowderMix
                : SalepMortarPhase.PowdersHomogeneous;
            bench.SetMortarPhase(phase, Mathf.Lerp(0.5f, 0.6f, progress));
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

        SalepStep next = stepIndex >= StepTitles.Length - 1
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
                bench?.SetMortarPhase(SalepMortarPhase.AsamPowder, 0.35f);
                ActivateMortarTransfer(AsamId);
                break;

            case SalepStep.Step_04_WeighSulfurPP:
                bench?.BeginWeighing(SulfurId);
                break;

            case SalepStep.Step_05_MoveSulfurToMortar:
                bench?.SetMortarPhase(SalepMortarPhase.PowderMix, 0.5f);
                ActivateMortarTransfer(SulfurId);
                break;

            case SalepStep.Step_06_GrindPowders:
                bench?.SetMortarPhase(SalepMortarPhase.PowderMix, 0.5f);
                ActivateMixZone();
                break;

            case SalepStep.Step_07_WeighVaselinAlbum:
                bench?.BeginWeighing(VaselinId);
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
        if (mortarTransferZone == null)
            return;

        mortarTransferZone.ResetZone();
        mortarTransferZone.ConfigureMortar(mortarController);
        mortarTransferZone.ConfigureSource(depositZone);
        mortarTransferZone.SetRequiredIngredient(ingredientId, false);
        mortarTransferZone.SetActive(true);
    }

    private void ActivateMixZone()
    {
        if (mortarMixZone == null)
            return;

        mortarMixZone.ResetZone();
        if (stamper != null)
            mortarMixZone.ConfigureStamper(stamper);
        mortarMixZone.SetActive(true);
    }

    private void ActivatePotTransfer()
    {
        if (potTransferZone == null)
            return;

        potTransferZone.ResetZone();
        potTransferZone.SetRequiredIngredient(VaselinId, true);
        potTransferZone.SetActive(true);
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
            stepHighlights[index].enabled = active;

        if (stepArrows != null && index < stepArrows.Length && stepArrows[index] != null)
            stepArrows[index].SetVisible(active);
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

        if (etiketWorkflow == null)
            etiketWorkflow = GetComponent<EtiketWorkflow>();
    }

    private static int GetStepIndex(SalepStep step)
    {
        int index = (int)step - (int)SalepStep.Step_01_PrepareParchmentOnBalance;
        return index >= 0 && index < StepTitles.Length ? index : -1;
    }
}
