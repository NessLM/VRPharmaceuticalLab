using UnityEngine;

/// <summary>
/// ScriptableObject representing a single step in the lab procedure.
/// Create via: Assets > Create > VRPharmacy > Procedure Step.
/// </summary>
[CreateAssetMenu(fileName = "NewProcedureStep", menuName = "VRPharmacy/Procedure Step")]
public class ProcedureStep : ScriptableObject
{
    [Header("Step Information")]
    [Tooltip("Short title displayed as the step heading.")]
    public string stepTitle = "Langkah";

    [TextArea(3, 8)]
    [Tooltip("Detailed instruction text for this step.")]
    public string stepDescription = "Deskripsi langkah...";

    [Header("Action Key")]
    [Tooltip("Unique string identifier. Call ProcedureManager.TryCompleteByAction() with this key to complete the step.")]
    public string actionKey = "";

    [Header("Scoring")]
    [Tooltip("Score awarded when this step is completed correctly.")]
    public int scoreOnComplete = 10;

    [Tooltip("Penalty applied when a wrong action is reported during this step.")]
    public int penaltyOnMistake = -5;
}
