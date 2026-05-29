using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
[RequireComponent(typeof(Collider))]
public class WasherRedBallSwitch : MonoBehaviour
{
    [Header("Water")]
    public ParticleSystem waterParticle;
    public Collider waterHitZone;

    [Header("Ball Visual")]
    public Renderer ballRenderer;
    public Material waterOnMaterial;
    public Material waterOffMaterial;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;
    private bool isWaterOn = false;
    private bool isHovered = false;

    private void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

        if (ballRenderer == null)
            ballRenderer = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
    }

    private void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
    }

    private void Start()
    {
        SetWater(false);
    }

    private void Update()
    {
        if (!isHovered)
            return;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            ToggleWater();
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            ToggleWater();
        }
#endif
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        isHovered = true;
        Debug.Log("Ray kena bola merah");
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        isHovered = false;
    }

    public void ToggleWater()
    {
        Debug.Log("L MOUSE menekan bola merah");
        SetWater(!isWaterOn);
    }

    private void SetWater(bool active)
    {
        isWaterOn = active;

        if (waterParticle != null)
        {
            if (active)
                waterParticle.Play();
            else
                waterParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (waterHitZone != null)
        {
            waterHitZone.enabled = active;
        }

        if (ballRenderer != null)
        {
            if (active && waterOnMaterial != null)
                ballRenderer.material = waterOnMaterial;
            else if (!active && waterOffMaterial != null)
                ballRenderer.material = waterOffMaterial;
        }
    }
}