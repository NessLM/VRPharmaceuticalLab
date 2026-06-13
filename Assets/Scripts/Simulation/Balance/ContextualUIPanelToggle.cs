using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Context-aware replacement for PanelToggleOnButton.
/// Button B only triggers a UI panel if the controller ray is pointing at a valid focus target:
///   - BalanceUIFocusTarget  → toggle balance lesson panel.
///   - SpoonUIFocusTarget    → toggle spoon info panel (also triggers when spoon is held).
///
/// If B is pressed while NOT targeting any valid object, nothing happens.
///
/// Attach to: XR Origin (XR Rig) or any persistent always-active GameObject.
/// Wire: balancePanel, spoonPanel.
/// Interactors are auto-discovered if not manually assigned.
/// </summary>
public class ContextualUIPanelToggle : MonoBehaviour
{
    [Header("Panel Controllers")]
    [Tooltip("Controller managing BalanceLessonCanvas and WeightSelectorCanvas.")]
    [SerializeField] private BalanceUIPanelController balancePanel;
    [Tooltip("Controller managing SpoonInfoCanvas on sendokTanduk.")]
    [SerializeField] private SpoonUIPanelController spoonPanel;

    [Header("Interactors (auto-discovered if empty)")]
    [Tooltip("Near-Far or Ray interactors to check for hover/selection. Auto-populated at runtime.")]
    [SerializeField] private List<NearFarInteractor> trackedInteractors = new();

    [Header("Hover Detection")]
    [Tooltip("Maximum raycast distance for detecting focus targets.")]
    [SerializeField] private float raycastMaxDistance = 6f;
    [Tooltip("Physics layers included in focus detection raycast. Use ~0 for all layers.")]
    [SerializeField] private LayerMask raycastMask = ~0;

    [Header("Custom Action (optional)")]
    [Tooltip("Leave empty to use default: B on right controller + keyboard B.")]
    [SerializeField] private InputActionReference customToggleAction;

    private InputAction _defaultAction;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (balancePanel == null)
            balancePanel = FindFirstObjectByType<BalanceUIPanelController>(FindObjectsInactive.Include);

        if (spoonPanel == null)
            spoonPanel = FindFirstObjectByType<SpoonUIPanelController>(FindObjectsInactive.Include);

        // Auto-discover NearFarInteractors if none were manually assigned.
        if (trackedInteractors.Count == 0)
        {
            var found = FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None);
            trackedInteractors.AddRange(found);
        }
    }

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

    // ── Input ─────────────────────────────────────────────────────────────────

    private static InputAction BuildDefaultAction()
    {
        var action = new InputAction("ContextualPanelToggle", InputActionType.Button);
        action.AddBinding("<XRController>{RightHand}/secondaryButton");
        action.AddBinding("<XRController>{LeftHand}/secondaryButton");
        action.AddBinding("<Keyboard>/b");
        return action;
    }

    private void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        RefreshInteractorsIfNeeded();
        bool balanceFocus = IsRayHitting<BalanceUIFocusTarget>();
        bool spoonFocus   = IsRayHitting<SpoonUIFocusTarget>()
                         || IsRayHitting<HornSpoon>()
                         || IsAnyInteractorSelecting<HornSpoon>()
                         || IsAnyInteractorSelecting<SpoonUIFocusTarget>();

        if (spoonPanel != null && spoonPanel.IsOpen && !balanceFocus)
            spoonPanel.Toggle();
        else if (balancePanel != null && balancePanel.IsOpen && !spoonFocus)
            balancePanel.Toggle();
        else if (balanceFocus && balancePanel != null)
            balancePanel.Toggle();
        else if (spoonFocus && spoonPanel != null)
            spoonPanel.Toggle();
        // No valid target → no action.
    }

    // ── Focus Detection ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if any tracked interactor's ray hits a Collider whose hierarchy
    /// contains a component of type T (includes trigger colliders).
    /// Also checks Camera.main's gaze direction as a fallback for HMD / gaze mode.
    /// </summary>
    private bool IsRayHitting<T>() where T : Component
    {
        // Controller rays (NearFarInteractor, active in controller and hand-tracking mode)
        foreach (var interactor in trackedInteractors)
        {
            if (interactor == null || !interactor.isActiveAndEnabled) continue;

            var ray = new Ray(
                interactor.transform.position,
                interactor.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, raycastMaxDistance,
                raycastMask, QueryTriggerInteraction.Collide))
            {
                if (hit.collider.GetComponentInParent<T>() != null)
                    return true;
            }
        }

        // Gaze / HMD fallback — works when using the XR Device Simulator in HMD mode
        // or any head-gaze scenario where the camera forward is the primary pointer.
        Camera cam = Camera.main;
        if (cam != null)
        {
            var gazeRay = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(gazeRay, out RaycastHit gazeHit, raycastMaxDistance,
                raycastMask, QueryTriggerInteraction.Collide))
            {
                if (gazeHit.collider.GetComponentInParent<T>() != null)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true if any tracked interactor currently has a selected interactable
    /// whose hierarchy contains a component of type T.
    /// </summary>
    private bool IsAnyInteractorSelecting<T>() where T : Component
    {
        foreach (var interactor in trackedInteractors)
        {
            if (interactor == null) continue;
            foreach (var sel in interactor.interactablesSelected)
            {
                if (sel is MonoBehaviour mb && mb.GetComponentInParent<T>() != null)
                    return true;
            }
        }
        return false;
    }

    private void RefreshInteractorsIfNeeded()
    {
        trackedInteractors.RemoveAll(interactor => interactor == null);
        if (trackedInteractors.Count > 0)
            return;

        var found = FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None);
        trackedInteractors.AddRange(found);
    }
}
