using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Listens for a controller button (default: B on right hand / Y on left hand + keyboard B)
/// and toggles the active state of all assigned panel GameObjects.
/// Each panel can also be closed individually via its own X button (call SetActive(false) on the panel).
///
/// Attach to: any always-active GameObject, e.g. XR Origin.
/// Wire: panels — the GameObjects to toggle (e.g. WeightSelectorCanvas, BalanceLessonCanvas).
/// </summary>
public class PanelToggleOnButton : MonoBehaviour
{
    [Header("Panels to Toggle")]
    [Tooltip("GameObjects to show/hide when the toggle button is pressed.")]
    [SerializeField] private GameObject[] panels;

    [Header("Optional: override button binding")]
    [Tooltip("Leave empty to use the default (B on right controller + keyboard B).")]
    [SerializeField] private InputActionReference customToggleAction;

    // Internal action (used when customToggleAction is not set)
    private InputAction _defaultAction;
    private bool _lastPressed;

    private void OnEnable()
    {
        if (customToggleAction != null)
        {
            customToggleAction.action.Enable();
            customToggleAction.action.performed += OnTogglePerformed;
        }
        else
        {
            _defaultAction = BuildDefaultAction();
            _defaultAction.Enable();
            _defaultAction.performed += OnTogglePerformed;
        }
    }

    private void OnDisable()
    {
        if (customToggleAction != null)
        {
            customToggleAction.action.performed -= OnTogglePerformed;
        }
        else if (_defaultAction != null)
        {
            _defaultAction.performed -= OnTogglePerformed;
            _defaultAction.Disable();
            _defaultAction.Dispose();
            _defaultAction = null;
        }
    }

    /// <summary>
    /// Builds the default input action:
    ///  - B button (secondaryButton) on right XR controller
    ///  - Y button (secondaryButton) on left XR controller
    ///  - B key on keyboard (for XR Device Simulator)
    /// </summary>
    private static InputAction BuildDefaultAction()
    {
        var action = new InputAction("TogglePanels", InputActionType.Button);
        action.AddBinding("<XRController>{RightHand}/secondaryButton");
        action.AddBinding("<XRController>{LeftHand}/secondaryButton");
        action.AddBinding("<Keyboard>/b");
        return action;
    }

    private void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        if (panels == null) return;
        foreach (var panel in panels)
        {
            if (panel != null)
                panel.SetActive(!panel.activeSelf);
        }
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>Forces all panels open (active).</summary>
    public void ShowAll()
    {
        foreach (var p in panels)
            if (p != null) p.SetActive(true);
    }

    /// <summary>Forces all panels closed.</summary>
    public void HideAll()
    {
        foreach (var p in panels)
            if (p != null) p.SetActive(false);
    }
}
