using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// World-space educational UI panel for the MG balance scale simulation.
/// Displays live left/right mass readings, difference, balance status, and guided lesson steps.
/// Subscribe to MG_BalanceController events for balance-state-driven step progression.
///
/// Attach to: LessonPanel child of MG_BalanceScale.
/// Requires a World Space Canvas with TMP_Text children wired in the Inspector.
/// </summary>
public class BalanceLessonUI : MonoBehaviour
{
    [Header("Balance Reference")]
    [SerializeField] private MG_BalanceController balanceController;

    [Header("Mass Display Texts")]
    [SerializeField] private TMP_Text leftMassText;
    [SerializeField] private TMP_Text rightMassText;
    [SerializeField] private TMP_Text differenceText;
    [SerializeField] private TMP_Text statusText;

    [Header("Lesson Step Texts")]
    [SerializeField] private TMP_Text stepText;
    [SerializeField] private TMP_Text stepCounterText;

    [Header("Lesson Steps")]
    [SerializeField]
    private List<string> lessonSteps = new List<string>
    {
        "Langkah 1: Pastikan timbangan kosong dan lengan dalam posisi seimbang.",
        "Langkah 2: Letakkan anak timbangan di piringan kanan sesuai berat yang dibutuhkan.",
        "Langkah 3: Tambahkan serbuk ke piringan kiri menggunakan sendok tanduk.",
        "Langkah 4: Sesuaikan jumlah serbuk hingga lengan timbangan mendatar.",
        "Langkah 5: Timbangan seimbang! Catat berat dan simpan serbuk dengan benar."
    };

    [Header("Visual Indicators")]
    [Tooltip("Optional: activated when the scale is balanced.")]
    [SerializeField] private GameObject balancedIndicator;
    [Tooltip("Optional: activated when the scale is not balanced.")]
    [SerializeField] private GameObject unbalancedIndicator;

    [Header("Status Colors")]
    [SerializeField] private Color balancedColor = new Color(0.22f, 0.78f, 0.22f, 1f);
    [SerializeField] private Color unbalancedColor = new Color(0.90f, 0.40f, 0.10f, 1f);
    [SerializeField] private Color neutralColor = Color.white;

    [Header("Events")]
    public UnityEvent onFinalStepReached;
    public UnityEvent<int> onStepChanged;

    private int currentStep;

    // --- Lifecycle ---

    private void Start()
    {
        if (balanceController != null)
        {
            balanceController.onBalanced.AddListener(OnScaleBalanced);
            balanceController.onUnbalanced.AddListener(OnScaleUnbalanced);
        }

        SetIndicators(false);
        RefreshStepDisplay();
    }

    private void OnDestroy()
    {
        if (balanceController == null) return;
        balanceController.onBalanced.RemoveListener(OnScaleBalanced);
        balanceController.onUnbalanced.RemoveListener(OnScaleUnbalanced);
    }

    private void Update()
    {
        if (balanceController != null)
            RefreshMassDisplay();
    }

    // --- Display ---

    private void RefreshMassDisplay()
    {
        float left = balanceController.LeftMassGrams;
        float right = balanceController.RightMassGrams;
        float diff = right - left;

        if (leftMassText != null) leftMassText.text = $"Kiri:   {left:F2} g";
        if (rightMassText != null) rightMassText.text = $"Kanan: {right:F2} g";
        if (differenceText != null) differenceText.text = $"Selisih: {Mathf.Abs(diff):F2} g";

        if (statusText == null) return;

        bool bothEmpty = Mathf.Approximately(left, 0f) && Mathf.Approximately(right, 0f);
        if (bothEmpty)
        {
            statusText.text = "Kosong";
            statusText.color = neutralColor;
        }
        else if (balanceController.IsBalanced)
        {
            statusText.text = "SEIMBANG";
            statusText.color = balancedColor;
        }
        else if (diff > 0f)
        {
            statusText.text = $"Kanan lebih berat ({diff:F2} g)";
            statusText.color = unbalancedColor;
        }
        else
        {
            statusText.text = $"Kiri lebih berat ({Mathf.Abs(diff):F2} g)";
            statusText.color = unbalancedColor;
        }
    }

    private void RefreshStepDisplay()
    {
        if (stepText != null)
        {
            stepText.text = currentStep < lessonSteps.Count
                ? lessonSteps[currentStep]
                : "Simulasi selesai. Semua langkah telah dikerjakan.";
        }

        if (stepCounterText != null)
            stepCounterText.text = $"Langkah {currentStep + 1} dari {lessonSteps.Count}";
    }

    // --- Balance Events ---

    private void OnScaleBalanced()
    {
        SetIndicators(true);
        if (currentStep < lessonSteps.Count - 1)
            AdvanceStep();
        else
            onFinalStepReached?.Invoke();
    }

    private void OnScaleUnbalanced()
    {
        SetIndicators(false);
    }

    private void SetIndicators(bool balanced)
    {
        if (balancedIndicator != null) balancedIndicator.SetActive(balanced);
        if (unbalancedIndicator != null) unbalancedIndicator.SetActive(!balanced);
    }

    // --- Public Step API ---

    /// <summary>Manually advances to the next lesson step.</summary>
    public void AdvanceStep()
    {
        if (currentStep >= lessonSteps.Count - 1)
        {
            onFinalStepReached?.Invoke();
            return;
        }

        currentStep++;
        onStepChanged?.Invoke(currentStep);
        RefreshStepDisplay();
    }

    /// <summary>Returns to the previous lesson step.</summary>
    public void PreviousStep()
    {
        if (currentStep <= 0) return;
        currentStep--;
        onStepChanged?.Invoke(currentStep);
        RefreshStepDisplay();
    }

    /// <summary>Resets to the first lesson step.</summary>
    public void ResetLesson()
    {
        currentStep = 0;
        onStepChanged?.Invoke(0);
        RefreshStepDisplay();
    }
}
