using UnityEngine;
using TMPro;

/// <summary>
/// UI info panel for the sendokTanduk (Horn Spoon).
/// Shows the current powder amount on the spoon, the remaining target,
/// and a level indicator. Also toggles 5 powder level visual GameObjects.
///
/// Attach to: sendokTanduk or a child Canvas of sendokTanduk.
/// Wire: hornSpoon, rightZone, depositZone, and the TMP_Text fields.
/// </summary>
public class HornSpoonPowderMeter : MonoBehaviour
{
    // â”€â”€ Labels for each fill level â”€â”€
    private static readonly string[] LevelLabels = { "Kosong", "Sedikit", "Sedang", "Banyak", "Penuh" };

    [Header("References")]
    [SerializeField] private HornSpoon hornSpoon;
    [SerializeField] private WeightingZone rightZone;
    [SerializeField] private PowderDepositZone depositZone;

    [Header("UI Texts")]
    [SerializeField] private TMP_Text spoonAmountText;
    [SerializeField] private TMP_Text remainingTargetText;
    [SerializeField] private TMP_Text levelText;

    [Header("Powder Level Visuals on Spoon")]
    [Tooltip("5 child mesh GameObjects: index 0=Kosong, 1=Sedikit, 2=Sedang, 3=Banyak, 4=Penuh. " +
             "Only the matching index is active. All must be set; unused levels stay inactive.")]
    [SerializeField] private GameObject[] powderLevelObjects;

    private void Update()
    {
        if (hornSpoon == null) return;

        float spoonGrams = hornSpoon.CurrentAmountMg / 1000f;
        float lockedTarget = rightZone != null ? rightZone.TotalGrams : 0f;
        float deposited = depositZone != null ? depositZone.DepositedGrams : 0f;
        float remaining = Mathf.Max(0f, lockedTarget - deposited);

        UpdateAmountText(spoonGrams);
        UpdateRemainingText(remaining);
        UpdateLevelText();
        UpdatePowderLevelVisuals();
    }

    private void UpdateAmountText(float spoonGrams)
    {
        if (spoonAmountText != null)
            spoonAmountText.text = $"Isi sendok: {spoonGrams:F2} g";
    }

    private void UpdateRemainingText(float remaining)
    {
        if (remainingTargetText == null) return;

        if (rightZone == null || rightZone.TotalGrams <= 0.001f)
        {
            remainingTargetText.text = "Sisa: (taruh anak timbangan dulu)";
            remainingTargetText.color = new Color(0.8f, 0.8f, 0.8f);
            return;
        }

        remainingTargetText.text = $"Sisa target: {remaining:F2} g";
        remainingTargetText.color = remaining < 0.001f
            ? new Color(0.2f, 1f, 0.4f)  // green when done
            : Color.white;
    }

    private void UpdateLevelText()
    {
        if (levelText == null || hornSpoon == null) return;

        int index = GetLevelIndex();
        levelText.text = $"Level: {LevelLabels[index]}";
    }

    private void UpdatePowderLevelVisuals()
    {
        if (powderLevelObjects == null || hornSpoon == null) return;

        bool hasPowder = hornSpoon.CurrentAmountMg > 0f;
        int activeIndex = hasPowder ? GetLevelIndex() : 0;

        // Index 0 = Kosong. When empty, show index 0 (which represents "Kosong" visual).
        // When hasPowder is false, activeIndex 0 still represents "Kosong" visual,
        // but we only activate it if the array expects an explicit empty visual.
        for (int i = 0; i < powderLevelObjects.Length; i++)
        {
            if (powderLevelObjects[i] != null)
                powderLevelObjects[i].SetActive(i == activeIndex && (hasPowder || i == 0));
        }
    }

    private int GetLevelIndex()
    {
        if (hornSpoon == null) return 0;
        float ratio = hornSpoon.FillRatio;
        int count = LevelLabels.Length;
        return Mathf.Clamp(Mathf.RoundToInt(ratio * (count - 1)), 0, count - 1);
    }
}
