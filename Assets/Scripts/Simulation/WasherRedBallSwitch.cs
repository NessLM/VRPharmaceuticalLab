using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
[RequireComponent(typeof(Collider))]
public class WasherRedBallSwitch : MonoBehaviour
{
    [Header("Water Visual")]
    public ParticleSystem waterParticle;

    [Header("Water Hitbox")]
    [Tooltip("Object trigger air. Nanti ini yang akan dipakai untuk mengisi gelas.")]
    public GameObject waterHitboxObject;

    [Tooltip("Optional. Kalau kamu langsung mau enable/disable collider saja.")]
    public Collider waterHitZone;

    [Tooltip("Optional. Kalau diisi, tombol ini akan menyalakan/mematikan WaterSource juga.")]
    public WaterSource waterSource;

    [Header("Ball Visual")]
    public Renderer ballRenderer;
    public Material waterOnMaterial;
    public Material waterOffMaterial;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable _interactable;
    private bool _isWaterOn = false;
    private bool _isHovered = false;
    private float _lastToggleTime = -999f;
    private const float ToggleCooldown = 0.15f;

    private void Awake()
    {
        _interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

        if (ballRenderer == null)
            ballRenderer = GetComponent<Renderer>();

        if (waterHitZone == null && waterHitboxObject != null)
            waterHitZone = waterHitboxObject.GetComponent<Collider>();

        if (waterSource == null && waterHitZone != null)
            waterSource = waterHitZone.GetComponent<WaterSource>();

        if (waterSource == null && waterHitboxObject != null)
            waterSource = waterHitboxObject.GetComponent<WaterSource>();
    }

    private void OnEnable()
    {
        _interactable.hoverEntered.AddListener(OnHoverEntered);
        _interactable.hoverExited.AddListener(OnHoverExited);
        _interactable.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDisable()
    {
        _interactable.hoverEntered.RemoveListener(OnHoverEntered);
        _interactable.hoverExited.RemoveListener(OnHoverExited);
        _interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

    private void Start()
    {
        SetWater(false);
    }

    private void Update()
    {
        if (!_isHovered)
            return;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            TryToggleWater();
#else
        if (Input.GetMouseButtonDown(0))
            TryToggleWater();
#endif
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        _isHovered = true;
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        _isHovered = false;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        TryToggleWater();
    }

    private void TryToggleWater()
    {
        if (Time.unscaledTime - _lastToggleTime < ToggleCooldown)
            return;

        _lastToggleTime = Time.unscaledTime;
        ToggleWater();
    }

    public void ToggleWater()
    {
        SetWater(!_isWaterOn);
    }

    private void SetWater(bool active)
    {
        _isWaterOn = active;

        if (waterSource != null)
        {
            waterSource.SetFlow(active);
        }
        else if (waterParticle != null)
        {
            if (active)
                waterParticle.Play();
            else
                waterParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (waterSource == null && waterHitboxObject != null)
        {
            waterHitboxObject.SetActive(active);
        }

        if (waterSource == null && waterHitZone != null)
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
