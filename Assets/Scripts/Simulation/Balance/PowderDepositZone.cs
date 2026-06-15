using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Attached to Collider_Piring_Kiri. Detects a held HornSpoon above the left pan
/// and gradually transfers powder from the spoon to the pan.
/// A physical target weight must be present on the right pan before deposit is allowed.
///
/// Attach to: Collider_Piring_Kiri (which already has BoxCollider isTrigger).
/// Wire: rightZone, warningText (optional), panPowderLevels (optional).
/// </summary>
[RequireComponent(typeof(Collider))]
public class PowderDepositZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeightingZone rightZone;

    [Header("Pour Settings")]
    [Tooltip("How fast powder moves from spoon to pan (grams per second).")]
    [SerializeField] private float pourRateGramsPerSecond = 0.08f;
    [SerializeField] private bool allowPhysicalRightPanTarget = true;
    [SerializeField] private float minimumRightPanTargetGrams = 0.001f;

    [Header("Powder Visual on Left Pan")]
    [Tooltip("GameObjects representing powder level on the pan (index 0=none, last=full).")]
    [SerializeField] private GameObject[] panPowderLevels;
    [Tooltip("Gram value at which the last powder level visual is shown.")]
    [SerializeField] private float maxVisualGrams = 100f;

    [Header("Warning Display")]
    [Tooltip("Optional TMP_Text to show when user tries to pour before accepting target.")]
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private float warningDuration = 2.5f;

    [Header("Events")]
    public UnityEvent<float> onDepositChanged;
    public UnityEvent onTargetNotAccepted;

    private float depositedGrams = 0f;
    private HornSpoon spoonInZone = null;
    private float warningTimer = 0f;

    /// <summary>Total powder grams deposited on the left pan so far.</summary>
    public float DepositedGrams => depositedGrams;

    /// <summary>Resets the deposited powder to zero (call when restarting the lesson).</summary>
    public void ResetDeposit()
    {
        depositedGrams = 0f;
        UpdatePanVisual();
        onDepositChanged?.Invoke(depositedGrams);
    }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        if (rightZone == null)
            rightZone = FindSceneComponentByName<WeightingZone>("Collider_Piring_Kanan");

        if (rightZone == null)
            rightZone = FindSceneComponentByName<WeightingZone>("RightWeighingZone");
    }

    private void OnTriggerEnter(Collider other)
    {
        var spoon = other.GetComponentInParent<HornSpoon>();
        if (spoon == null) return;

        spoonInZone = spoon;

        if (!HasAcceptedTarget())
        {
            ShowWarning("Taruh atau terima anak timbangan kanan dulu!");
            onTargetNotAccepted?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var spoon = other.GetComponentInParent<HornSpoon>();
        if (spoon != null && spoon == spoonInZone)
            spoonInZone = null;
    }

    private void Update()
    {
        // Countdown warning display
        if (warningTimer > 0f)
        {
            warningTimer -= Time.deltaTime;
            if (warningTimer <= 0f && warningText != null)
                warningText.gameObject.SetActive(false);
        }

        // Skip if no spoon or target not accepted
        if (spoonInZone == null) return;
        if (!HasAcceptedTarget()) return;
        if (spoonInZone.IsEmpty) return;

        // Pour powder from spoon to pan
        float toTransferGrams = pourRateGramsPerSecond * Time.deltaTime;
        float spoonGrams = spoonInZone.CurrentAmountMg / 1000f;
        float actualGrams = Mathf.Min(toTransferGrams, spoonGrams);

        if (actualGrams <= 0f) return;

        float removedMg = spoonInZone.RemovePowder(actualGrams * 1000f);
        float removedGrams = removedMg / 1000f;

        if (removedGrams <= 0f) return;

        depositedGrams += removedGrams;
        UpdatePanVisual();
        onDepositChanged?.Invoke(depositedGrams);
    }

    private void UpdatePanVisual()
    {
        if (panPowderLevels == null || panPowderLevels.Length == 0) return;
        float ratio = maxVisualGrams > 0f ? Mathf.Clamp01(depositedGrams / maxVisualGrams) : 0f;
        int count = panPowderLevels.Length;

        // Index 0 = no powder, index count-1 = full
        int activeIndex = depositedGrams > 0f
            ? Mathf.Clamp(1 + Mathf.RoundToInt(ratio * (count - 2)), 1, count - 1)
            : 0;

        for (int i = 0; i < count; i++)
        {
            if (panPowderLevels[i] != null)
                panPowderLevels[i].SetActive(i == activeIndex);
        }
    }

    private void ShowWarning(string msg)
    {
        if (warningText == null) return;
        warningText.text = msg;
        warningText.gameObject.SetActive(true);
        warningTimer = warningDuration;
    }

    private bool HasAcceptedTarget()
    {
        return allowPhysicalRightPanTarget &&
               rightZone != null &&
               rightZone.TotalGrams >= minimumRightPanTargetGrams;
    }

    private T FindSceneComponentByName<T>(string objectName) where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();

        foreach (T component in components)
        {
            if (component == null || component.gameObject == null)
                continue;

            if (!component.gameObject.scene.IsValid())
                continue;

            if (component.name == objectName)
                return component;
        }

        return null;
    }
}
