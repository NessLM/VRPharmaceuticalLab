using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public sealed class SpoonScoopActivator : MonoBehaviour
{
    [SerializeField] private HornSpoon hornSpoon;
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private ScoopBottleTarget currentTarget;
    [SerializeField] private bool requireSelected = true;
    [SerializeField] private bool debugLogs;

    private void Awake()
    {
        if (hornSpoon == null)
            hornSpoon = GetComponent<HornSpoon>();

        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
            grabInteractable.activated.AddListener(OnActivated);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
            grabInteractable.activated.RemoveListener(OnActivated);
    }

    public void SetCurrentTarget(ScoopBottleTarget target)
    {
        currentTarget = target;
    }

    private void OnActivated(ActivateEventArgs args)
    {
        if (requireSelected && (grabInteractable == null || !grabInteractable.isSelected))
        {
            Log("Scoop rejected: spoon not selected");
            return;
        }

        if (currentTarget == null)
        {
            Log("Scoop rejected: no target");
            return;
        }

        if (currentTarget.TryScoop(hornSpoon))
            Log("Scoop success");
    }

    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"[SpoonScoopActivator] {message}", this);
    }
}
