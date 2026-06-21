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

    [Header("Runtime State")]
    [SerializeField] private SalepStep currentStep = SalepStep.Idle;

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

    public void BeginSalepProcedure()
    {
        if (salepIngredientsRoot != null)
            salepIngredientsRoot.SetActive(true);

        if (stepUiRoot != null)
            stepUiRoot.SetActive(true);

        ShowStep(SalepStep.Step_01_PrepareParchmentOnBalance);
    }

    public void ShowStep(SalepStep step)
    {
        currentStep = step;
        SetAllGuidance(false);

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
