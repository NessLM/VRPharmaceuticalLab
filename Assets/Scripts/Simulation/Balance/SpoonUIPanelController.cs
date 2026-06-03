using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages the visibility of the sendokTanduk (Horn Spoon) info panel.
/// Called by ContextualUIPanelToggle when the player hovers or holds the spoon and presses B.
///
/// Attach to: sendokTanduk or any persistent manager GameObject.
/// Wire: spoonInfoCanvas.
/// </summary>
public class SpoonUIPanelController : MonoBehaviour
{
    [Header("Panel Root")]
    [Tooltip("SpoonInfoCanvas root GameObject on the sendokTanduk.")]
    [SerializeField] private GameObject spoonInfoCanvas;

    [Header("Events")]
    public UnityEvent onPanelOpened;
    public UnityEvent onPanelClosed;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Guarantee panel is hidden at scene start regardless of Editor state.
        if (spoonInfoCanvas != null) spoonInfoCanvas.SetActive(false);
    }

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>True if the spoon info panel is currently visible.</summary>
    public bool IsOpen => spoonInfoCanvas != null && spoonInfoCanvas.activeSelf;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Shows the spoon info panel.</summary>
    public void Show()
    {
        if (spoonInfoCanvas == null || spoonInfoCanvas.activeSelf) return;
        spoonInfoCanvas.SetActive(true);
        onPanelOpened?.Invoke();
    }

    /// <summary>Hides the spoon info panel.</summary>
    public void Hide()
    {
        if (spoonInfoCanvas == null || !spoonInfoCanvas.activeSelf) return;
        spoonInfoCanvas.SetActive(false);
        onPanelClosed?.Invoke();
    }

    /// <summary>Toggles the spoon info panel visibility.</summary>
    public void Toggle()
    {
        if (IsOpen) Hide(); else Show();
    }
}
