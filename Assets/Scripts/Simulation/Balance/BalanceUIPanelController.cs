using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages the visibility of the balance lesson panel and the weight selector panel.
/// Called by ContextualUIPanelToggle when the player focuses the timbanganNeraca and presses B.
///
/// Toggle() shows both panels if they are closed, or hides them if open.
/// Each panel can also be closed individually via its own X button (calling Hide() directly).
///
/// Attach to: timbanganNeraca or any persistent manager GameObject.
/// Wire: lessonCanvas, weightSelectorCanvas.
/// </summary>
public class BalanceUIPanelController : MonoBehaviour
{
    [Header("Managed Panel Roots")]
    [Tooltip("BalanceLessonCanvas root GameObject.")]
    [SerializeField] private GameObject lessonCanvas;
    [Tooltip("WeightSelectorCanvas root GameObject.")]
    [SerializeField] private GameObject weightSelectorCanvas;

    [Header("Behaviour")]
    [Tooltip("Show both panels together on Toggle(). If false, only the weight selector is shown.")]
    [SerializeField] private bool showBothTogether = true;

    [Header("Events")]
    public UnityEvent onPanelOpened;
    public UnityEvent onPanelClosed;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Guarantee both panels are hidden at scene start regardless of Editor state.
        if (lessonCanvas != null)         lessonCanvas.SetActive(false);
        if (weightSelectorCanvas != null) weightSelectorCanvas.SetActive(false);
    }

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>True if at least one managed panel is currently visible.</summary>
    public bool IsOpen =>
        (lessonCanvas != null          && lessonCanvas.activeSelf) ||
        (weightSelectorCanvas != null  && weightSelectorCanvas.activeSelf);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Shows both managed panels.</summary>
    public void Show()
    {
        bool changed = false;
        if (weightSelectorCanvas != null && !weightSelectorCanvas.activeSelf)
        {
            weightSelectorCanvas.SetActive(true);
            changed = true;
        }
        if (showBothTogether && lessonCanvas != null && !lessonCanvas.activeSelf)
        {
            lessonCanvas.SetActive(true);
            changed = true;
        }
        if (changed) onPanelOpened?.Invoke();
    }

    /// <summary>Hides both managed panels.</summary>
    public void Hide()
    {
        bool changed = false;
        if (lessonCanvas != null && lessonCanvas.activeSelf)
        {
            lessonCanvas.SetActive(false);
            changed = true;
        }
        if (weightSelectorCanvas != null && weightSelectorCanvas.activeSelf)
        {
            weightSelectorCanvas.SetActive(false);
            changed = true;
        }
        if (changed) onPanelClosed?.Invoke();
    }

    /// <summary>Toggles the panels. Opens if closed; closes if any panel is open.</summary>
    public void Toggle()
    {
        if (IsOpen) Hide(); else Show();
    }

    /// <summary>Hides only the lesson canvas (e.g., from its own X button).</summary>
    public void HideLessonPanel()
    {
        if (lessonCanvas != null) lessonCanvas.SetActive(false);
        if (!IsOpen) onPanelClosed?.Invoke();
    }

    /// <summary>Hides only the weight selector canvas (e.g., from its own X button).</summary>
    public void HideWeightSelectorPanel()
    {
        if (weightSelectorCanvas != null) weightSelectorCanvas.SetActive(false);
        if (!IsOpen) onPanelClosed?.Invoke();
    }
}
