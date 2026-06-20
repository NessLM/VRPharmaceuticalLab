using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public sealed class SpoonScoopActivator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HornSpoon hornSpoon;
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private ScoopBottleTarget currentTarget;
    [SerializeField] private SpoonActionPrompt actionPrompt;

    [Header("Validation")]
    [SerializeField] private bool requireSelected = true;
    [SerializeField] private bool debugLogs;

    private void Awake()
    {
        if (hornSpoon == null)
            hornSpoon = GetComponent<HornSpoon>();

        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (actionPrompt == null)
            actionPrompt = GetComponentInChildren<SpoonActionPrompt>(true);
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.activated.AddListener(OnActivated);
            grabInteractable.selectEntered.AddListener(OnSelected);
            grabInteractable.selectExited.AddListener(OnDeselected);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.activated.RemoveListener(OnActivated);
            grabInteractable.selectEntered.RemoveListener(OnSelected);
            grabInteractable.selectExited.RemoveListener(OnDeselected);
        }
    }

    public void SetCurrentTarget(ScoopBottleTarget target)
    {
        currentTarget = target;

        if (actionPrompt != null)
            actionPrompt.SetScoopTarget(target);
    }

    public void ClearCurrentTarget(ScoopBottleTarget target)
    {
        if (currentTarget == target)
            currentTarget = null;

        if (actionPrompt != null)
            actionPrompt.ClearScoopTarget(target);
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        if (actionPrompt != null)
            actionPrompt.RefreshPrompt();
    }

    private void OnDeselected(SelectExitEventArgs args)
    {
        if (actionPrompt != null)
            actionPrompt.RefreshPrompt();
    }

    private void OnActivated(ActivateEventArgs args)
    {
        if (requireSelected && (grabInteractable == null || !grabInteractable.isSelected))
        {
            Log("Scoop rejected: spoon not selected.");
            return;
        }

        if (currentTarget == null)
        {
            Log("Scoop rejected: no ScoopBottleTarget.");
            return;
        }

        if (currentTarget.TryScoop(hornSpoon))
            Log("Scoop success.");

        if (actionPrompt != null)
            actionPrompt.RefreshPrompt();
    }

    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"[SpoonScoopActivator] {message}", this);
    }
}