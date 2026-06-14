using TMPro;
using UnityEngine;

/// <summary>
/// Displays a warning inside SpoonInfoCanvas when the player opens the spoon panel
/// but has not yet placed a physical weight on the right pan.
///
/// Attach to: sendokTanduk (or any persistent GameObject).
/// Wire: warningText (the WarningText TMP child inside SpoonInfoPanel),
///       rightZone (Collider_Piring_Kanan).
/// </summary>
public class SpoonWeightWarning : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TMP_Text inside SpoonInfoPanel that shows the warning message.")]
    [SerializeField] private TMP_Text warningText;

    [Tooltip("Right weighing zone used to query whether a physical weight is already on the pan.")]
    [SerializeField] private WeightingZone rightZone;

    private const string WarningMessage =
        "<color=#FFB347>âš  Terima anak timbangan dulu\nsebelum menakar bubuk ke cawan kiri!</color>";

    // â”€â”€ Lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void OnEnable()
    {
        RefreshWarning();
    }

    private void OnDisable()
    {
        if (warningText != null) warningText.gameObject.SetActive(false);
    }

    // â”€â”€ Public API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Shows the warning if weights have not been accepted yet, hides it otherwise.
    /// Called automatically on OnEnable; also callable externally after checklist state changes.
    /// </summary>
    public void RefreshWarning()
    {
        if (warningText == null) return;

        bool weightsAccepted = rightZone != null && rightZone.TotalGrams > 0.001f;
        warningText.gameObject.SetActive(!weightsAccepted);

        if (!weightsAccepted)
            warningText.text = WarningMessage;
    }
}
