using TMPro;
using UnityEngine;

/// <summary>
/// Displays a warning inside SpoonInfoCanvas when the player opens the spoon panel
/// but has not yet accepted (Terima) a weight selection on the balance scale.
///
/// Attach to: sendokTanduk (or any persistent GameObject).
/// Wire: warningText (the WarningText TMP child inside SpoonInfoPanel),
///       checklist (VirtualWeightChecklist on WeightSelectorPanel).
/// </summary>
public class SpoonWeightWarning : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TMP_Text inside SpoonInfoPanel that shows the warning message.")]
    [SerializeField] private TMP_Text warningText;

    [Tooltip("VirtualWeightChecklist on WeightSelectorPanel. " +
             "Used to query whether weights have been accepted.")]
    [SerializeField] private VirtualWeightChecklist checklist;

    private const string WarningMessage =
        "<color=#FFB347>⚠ Terima anak timbangan dulu\nsebelum menakar bubuk ke cawan kiri!</color>";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        RefreshWarning();
    }

    private void OnDisable()
    {
        if (warningText != null) warningText.gameObject.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the warning if weights have not been accepted yet, hides it otherwise.
    /// Called automatically on OnEnable; also callable externally after checklist state changes.
    /// </summary>
    public void RefreshWarning()
    {
        if (warningText == null) return;

        bool weightsAccepted = checklist != null && checklist.IsLocked;
        warningText.gameObject.SetActive(!weightsAccepted);

        if (!weightsAccepted)
            warningText.text = WarningMessage;
    }
}
