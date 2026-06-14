using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Context-aware panel toggle for the horn spoon info panel.
/// Press B only toggles the panel when the controller ray, HMD gaze, or current
/// selection is related to sendokTanduk.
/// </summary>
public class ContextualUIPanelToggle : MonoBehaviour
{
    [Header("Panel Controller")]
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

    private InputAction defaultAction;

    private void Awake()
    {
        if (spoonPanel == null)
            spoonPanel = FindFirstObjectByType<SpoonUIPanelController>(FindObjectsInactive.Include);

        if (trackedInteractors.Count == 0)
        {
            NearFarInteractor[] found = FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None);
            trackedInteractors.AddRange(found);
        }
    }

    private void OnEnable()
    {
        if (customToggleAction != null)
        {
            customToggleAction.action.Enable();
            customToggleAction.action.performed += OnTogglePerformed;
            return;
        }

        defaultAction = BuildDefaultAction();
        defaultAction.Enable();
        defaultAction.performed += OnTogglePerformed;
    }

    private void OnDisable()
    {
        if (customToggleAction != null)
        {
            customToggleAction.action.performed -= OnTogglePerformed;
            return;
        }

        if (defaultAction == null)
            return;

        defaultAction.performed -= OnTogglePerformed;
        defaultAction.Disable();
        defaultAction.Dispose();
        defaultAction = null;
    }

    private static InputAction BuildDefaultAction()
    {
        InputAction action = new InputAction("ContextualPanelToggle", InputActionType.Button);
        action.AddBinding("<XRController>{RightHand}/secondaryButton");
        action.AddBinding("<XRController>{LeftHand}/secondaryButton");
        action.AddBinding("<Keyboard>/b");
        return action;
    }

    private void OnTogglePerformed(InputAction.CallbackContext context)
    {
        RefreshInteractorsIfNeeded();

        bool spoonFocus = IsRayHitting<SpoonUIFocusTarget>() ||
                          IsRayHitting<HornSpoon>() ||
                          IsAnyInteractorSelecting<HornSpoon>() ||
                          IsAnyInteractorSelecting<SpoonUIFocusTarget>();

        if (spoonFocus && spoonPanel != null)
            spoonPanel.Toggle();
    }

    private bool IsRayHitting<T>() where T : Component
    {
        foreach (NearFarInteractor interactor in trackedInteractors)
        {
            if (interactor == null || !interactor.isActiveAndEnabled)
                continue;

            Ray ray = new Ray(interactor.transform.position, interactor.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, raycastMaxDistance, raycastMask, QueryTriggerInteraction.Collide) &&
                hit.collider.GetComponentInParent<T>() != null)
            {
                return true;
            }
        }

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Ray gazeRay = new Ray(cam.transform.position, cam.transform.forward);
        return Physics.Raycast(gazeRay, out RaycastHit gazeHit, raycastMaxDistance, raycastMask, QueryTriggerInteraction.Collide) &&
               gazeHit.collider.GetComponentInParent<T>() != null;
    }

    private bool IsAnyInteractorSelecting<T>() where T : Component
    {
        foreach (NearFarInteractor interactor in trackedInteractors)
        {
            if (interactor == null)
                continue;

            foreach (var selected in interactor.interactablesSelected)
            {
                if (selected is MonoBehaviour behaviour &&
                    behaviour.GetComponentInParent<T>() != null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void RefreshInteractorsIfNeeded()
    {
        trackedInteractors.RemoveAll(interactor => interactor == null);
        if (trackedInteractors.Count > 0)
            return;

        NearFarInteractor[] found = FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None);
        trackedInteractors.AddRange(found);
    }
}
