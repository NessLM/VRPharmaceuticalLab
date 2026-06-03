using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Manages the step-by-step procedure for the VR lab simulation.
/// Handles step progression, scoring, and UI updates.
/// Attach to: a manager GameObject in the VRLabSimulation scene.
/// Wire up ProcedureStep assets in the Inspector, then call TryCompleteByAction()
/// from interaction scripts when the user performs an action.
/// </summary>
public class ProcedureManager : MonoBehaviour
{
    [Header("Procedure Steps")]
    [Tooltip("Ordered list of ProcedureStep ScriptableObjects defining the lab workflow.")]
    [SerializeField] private List<ProcedureStep> steps = new();

    [Header("UI References")]
    [SerializeField] private TMP_Text stepTitleText;
    [SerializeField] private TMP_Text stepDescriptionText;
    [SerializeField] private TMP_Text stepCounterText;
    [SerializeField] private TMP_Text scoreText;

    [Header("Events")]
    public UnityEvent<ProcedureStep> onStepStarted;
    public UnityEvent<ProcedureStep> onStepCompleted;
    public UnityEvent<int> onScoreChanged;
    public UnityEvent onAllStepsCompleted;

    private int currentStepIndex = 0;
    private int totalScore = 0;

    public int TotalScore => totalScore;
    public int CurrentStepIndex => currentStepIndex;
    public int StepCount => steps?.Count ?? 0;
    public ProcedureStep CurrentStep => (steps != null && currentStepIndex < steps.Count)
        ? steps[currentStepIndex]
        : null;

    private void Start()
    {
        ResetProcedure();
    }

    /// <summary>
    /// Tries to complete the current step by matching an action key.
    /// Call this from lab instrument scripts when the user performs an action.
    /// </summary>
    public void TryCompleteByAction(string actionKey)
    {
        if (CurrentStep == null) return;
        if (string.Equals(CurrentStep.actionKey, actionKey, System.StringComparison.OrdinalIgnoreCase))
            CompleteCurrentStep();
    }

    /// <summary>Marks the current step as completed and advances to the next.</summary>
    public void CompleteCurrentStep()
    {
        if (CurrentStep == null) return;

        AddScore(CurrentStep.scoreOnComplete);
        onStepCompleted?.Invoke(CurrentStep);
        currentStepIndex++;

        if (currentStepIndex >= StepCount)
        {
            DisplayUI("Selesai!", "Semua langkah prosedur telah diselesaikan. Selamat!", "");
            onAllStepsCompleted?.Invoke();
            return;
        }

        ShowCurrentStep();
    }

    /// <summary>Reports a wrong action, applying the penalty for the current step.</summary>
    public void ReportWrongAction()
    {
        if (CurrentStep == null) return;
        AddScore(CurrentStep.penaltyOnMistake);
    }

    /// <summary>Adds (or subtracts) points from the total score.</summary>
    public void AddScore(int points)
    {
        totalScore += points;
        onScoreChanged?.Invoke(totalScore);
        UpdateScoreUI();
    }

    /// <summary>Resets the entire procedure to the first step with zero score.</summary>
    public void ResetProcedure()
    {
        currentStepIndex = 0;
        totalScore = 0;
        UpdateScoreUI();
        ShowCurrentStep();
    }

    /// <summary>Jumps to a specific step by index (zero-based).</summary>
    public void GoToStep(int index)
    {
        if (steps == null || index < 0 || index >= steps.Count) return;
        currentStepIndex = index;
        ShowCurrentStep();
    }

    private void ShowCurrentStep()
    {
        if (CurrentStep == null) return;

        onStepStarted?.Invoke(CurrentStep);
        DisplayUI(
            CurrentStep.stepTitle,
            CurrentStep.stepDescription,
            $"{currentStepIndex + 1} / {StepCount}"
        );
    }

    private void DisplayUI(string title, string description, string counter)
    {
        if (stepTitleText != null)       stepTitleText.text = title;
        if (stepDescriptionText != null) stepDescriptionText.text = description;
        if (stepCounterText != null)     stepCounterText.text = counter;
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = $"Skor: {totalScore}";
    }
}
