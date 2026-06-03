using EPOOutline;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Enables an EPO outline when the XRGrabInteractable is hovered,
/// and disables it when exited or selected.
/// Attach to any interactable that needs hover-outline visual feedback
/// without the return-to-start behavior of InteractableItem.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(Outlinable))]
public class HoverOutlineController : MonoBehaviour
{
    private Outlinable _outlinable;
    private XRGrabInteractable _grabInteractable;

    private void Awake()
    {
        _outlinable = GetComponent<Outlinable>();
        _grabInteractable = GetComponent<XRGrabInteractable>();

        // Auto-populate outline targets from all child mesh renderers if none are set.
        if (_outlinable.OutlineTargetsCount == 0)
            _outlinable.AddAllChildRenderersToRenderingList(RenderersAddingMode.MeshRenderer);

        _outlinable.enabled = false;
    }

    private void OnEnable()
    {
        _grabInteractable.hoverEntered.AddListener(OnHoverEnter);
        _grabInteractable.hoverExited.AddListener(OnHoverExit);
        _grabInteractable.selectEntered.AddListener(OnSelect);
    }

    private void OnDisable()
    {
        _grabInteractable.hoverEntered.RemoveListener(OnHoverEnter);
        _grabInteractable.hoverExited.RemoveListener(OnHoverExit);
        _grabInteractable.selectEntered.RemoveListener(OnSelect);
    }

    private void OnHoverEnter(HoverEnterEventArgs args) => _outlinable.enabled = true;
    private void OnHoverExit(HoverExitEventArgs args) => _outlinable.enabled = false;
    private void OnSelect(SelectEnterEventArgs args) => _outlinable.enabled = false;
}
