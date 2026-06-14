using System.Collections.Generic;
using EPOOutline;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Keeps small XR grabbables visually unchanged while showing an outline only during XR hover/select.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
public class XRHoverOutlineFeedback : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private Outlinable[] outlineComponents;
    [SerializeField] private Renderer[] targetRenderers;

    [Header("Behavior")]
    [SerializeField] private bool startDisabled = true;
    [SerializeField] private bool keepOutlineWhileSelected = true;
    [SerializeField] private bool includeChildOutlineables = true;
    [SerializeField] private bool configureOutlineStyle = true;
    [SerializeField] private Color outlineColor = new Color(1f, 0.92156863f, 0.015686275f, 1f);

    private readonly List<Outlinable> resolvedOutlines = new List<Outlinable>();
    private bool hoverActive;
    private bool selectedActive;
    private bool forcedActive;
    private bool subscribed;

    public bool HoverActive => hoverActive;
    public bool SelectedActive => selectedActive;
    public bool ForcedActive => forcedActive;

    private void Awake()
    {
        ResolveReferences();

        if (startDisabled)
        {
            hoverActive = false;
            selectedActive = false;
            SetOutlineEnabled(false);
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        ApplyOutlineState();
    }

    private void OnDisable()
    {
        Unsubscribe();
        hoverActive = false;
        selectedActive = false;
        SetOutlineEnabled(false);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        SetOutlineEnabled(false);
    }

    public void SetForcedOutline(bool active)
    {
        forcedActive = active;
        ApplyOutlineState();
    }

    public void ForceOutlineOn() => SetForcedOutline(true);

    public void ForceOutlineOff() => SetForcedOutline(false);

    [ContextMenu("Refresh Outline Targets")]
    public void RefreshOutlineTargets()
    {
        ResolveReferences();
        ApplyOutlineState();
    }

    private void Subscribe()
    {
        if (subscribed || grabInteractable == null)
            return;

        grabInteractable.hoverEntered.AddListener(OnHoverEntered);
        grabInteractable.hoverExited.AddListener(OnHoverExited);
        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || grabInteractable == null)
            return;

        grabInteractable.hoverEntered.RemoveListener(OnHoverEntered);
        grabInteractable.hoverExited.RemoveListener(OnHoverExited);
        grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
        grabInteractable.selectExited.RemoveListener(OnSelectExited);
        subscribed = false;
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        hoverActive = true;
        ApplyOutlineState();
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        hoverActive = false;
        ApplyOutlineState();
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        selectedActive = true;
        ApplyOutlineState();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        selectedActive = false;
        ApplyOutlineState();
    }

    private void ApplyOutlineState()
    {
        bool shouldShow = forcedActive || hoverActive || (keepOutlineWhileSelected && selectedActive);
        SetOutlineEnabled(shouldShow);
    }

    private void ResolveReferences()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        resolvedOutlines.Clear();

        if (outlineComponents != null)
        {
            foreach (Outlinable outline in outlineComponents)
                AddResolvedOutline(outline);
        }

        if (includeChildOutlineables)
        {
            Outlinable[] childOutlines = GetComponentsInChildren<Outlinable>(true);
            foreach (Outlinable outline in childOutlines)
                AddResolvedOutline(outline);
        }

        if (resolvedOutlines.Count == 0)
            AddResolvedOutline(gameObject.AddComponent<Outlinable>());

        Renderer[] renderers = ResolveTargetRenderers();
        foreach (Outlinable outline in resolvedOutlines)
        {
            if (outline == null)
                continue;

            ConfigureOutline(outline);

            if (outline.OutlineTargetsCount > 0)
                continue;

            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer != null)
                    outline.AddRenderer(targetRenderer);
            }
        }

        outlineComponents = resolvedOutlines.ToArray();
    }

    private Renderer[] ResolveTargetRenderers()
    {
        if (targetRenderers != null && targetRenderers.Length > 0)
            return FilterTargetRenderers(targetRenderers);

        Renderer[] childRenderers = GetComponentsInChildren<Renderer>(true);
        targetRenderers = FilterTargetRenderers(childRenderers);
        return targetRenderers;
    }

    private Renderer[] FilterTargetRenderers(Renderer[] renderers)
    {
        List<Renderer> validRenderers = new List<Renderer>();

        if (renderers == null)
            return validRenderers.ToArray();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || IsIgnoredVisual(renderer.transform))
                continue;

            validRenderers.Add(renderer);
        }

        return validRenderers.ToArray();
    }

    private void AddResolvedOutline(Outlinable outline)
    {
        if (outline == null || IsIgnoredVisual(outline.transform) || resolvedOutlines.Contains(outline))
            return;

        resolvedOutlines.Add(outline);
    }

    private bool IsIgnoredVisual(Transform candidate)
    {
        Transform current = candidate;
        while (current != null && current != transform.parent)
        {
            string objectName = current.name;
            if (objectName.Contains("GrabCollider") ||
                objectName.Contains("AttachPoint") ||
                objectName.Contains("PhysicsCollider"))
                return true;

            if (current == transform)
                break;

            current = current.parent;
        }

        return false;
    }

    private void ConfigureOutline(Outlinable outline)
    {
        if (!configureOutlineStyle || outline == null)
            return;

        outline.RenderStyle = RenderStyle.Single;
        outline.OutlineParameters.Enabled = true;
        outline.OutlineParameters.Color = outlineColor;
        outline.BackParameters.Enabled = true;
        outline.BackParameters.Color = outlineColor;
        outline.FrontParameters.Enabled = true;
        outline.FrontParameters.Color = outlineColor;
    }

    private void SetOutlineEnabled(bool enabled)
    {
        foreach (Outlinable outline in resolvedOutlines)
        {
            if (outline != null)
                outline.enabled = enabled;
        }
    }
}
