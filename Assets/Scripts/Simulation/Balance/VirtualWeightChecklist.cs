using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Companion component to VirtualWeightSelector. Exposes additional change events and
/// per-frame selection queries so external systems (e.g. WeightVisualSpawner) can react
/// to checklist state without polling VirtualWeightSelector directly.
///
/// Requirement:
///   - totalMax: maximum allowed right-pan mass in grams (default 500 g).
///   - onSelectionTotalChanged: fires every frame the running total changes.
///   - onAccepted(float grams): fires when the user presses Terima.
///   - onReset: fires when the user presses Reset.
///
/// Attach on the same GameObject as VirtualWeightSelector.
/// </summary>
[RequireComponent(typeof(VirtualWeightSelector))]
public class VirtualWeightChecklist : MonoBehaviour
{
    [Header("Constraints")]
    [Tooltip("Maximum selectable total in grams. Selections that would exceed this are prevented " +
             "by VirtualWeightSelector's own 500 g cap.")]
    [SerializeField] private float maxTotalGrams = 500f;

    [Header("Events")]
    [Tooltip("Fires whenever the running selection total changes (pre-accept).")]
    public UnityEvent<float> onSelectionTotalChanged;
    [Tooltip("Fires when the user presses Terima (accepts the selection).")]
    public UnityEvent<float> onAccepted;
    [Tooltip("Fires when the user presses Reset (clears the selection).")]
    public UnityEvent onReset;

    private VirtualWeightSelector _selector;
    private float _cachedTotal = -1f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake() => _selector = GetComponent<VirtualWeightSelector>();

    private void OnEnable()
    {
        _selector.onTargetAccepted.AddListener(HandleAccepted);
        _selector.onTargetCleared.AddListener(HandleReset);
    }

    private void OnDisable()
    {
        _selector.onTargetAccepted.RemoveListener(HandleAccepted);
        _selector.onTargetCleared.RemoveListener(HandleReset);
    }

    private void Update()
    {
        float current = _selector.SelectedTotalGrams;
        if (Mathf.Abs(current - _cachedTotal) > 0.0001f)
        {
            _cachedTotal = current;
            onSelectionTotalChanged?.Invoke(current);
        }
    }

    // ── Event Handlers ────────────────────────────────────────────────────────

    private void HandleAccepted(float grams) => onAccepted?.Invoke(grams);
    private void HandleReset() => onReset?.Invoke();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Currently selected running total in grams (pre-accept).</summary>
    public float CurrentTotalGrams => _selector != null ? _selector.SelectedTotalGrams : 0f;

    /// <summary>Locked right-pan mass in grams after Terima is pressed.</summary>
    public float LockedTotalGrams => _selector != null ? _selector.LockedRightMassGrams : 0f;

    /// <summary>True if Terima has been pressed and the selection is locked.</summary>
    public bool IsLocked => _selector != null && _selector.IsLocked;

    /// <summary>Maximum allowed total in grams.</summary>
    public float MaxTotalGrams => maxTotalGrams;
}
