using UnityEngine;

public class WasherWaterController : MonoBehaviour
{
    [Header("Water Particle")]
    public ParticleSystem waterParticle;

    [Header("Water Hit Zone")]
    public Collider waterHitZone;

    [Header("Water Source")]
    public WaterSource waterSource;

    [Header("Status Indicator")]
    public Renderer statusIndicatorRenderer;
    public Material waterOnMaterial;
    public Material waterOffMaterial;

    public bool IsWaterOn { get; private set; }

    private void Awake()
    {
        if (waterSource == null && waterHitZone != null)
            waterSource = waterHitZone.GetComponent<WaterSource>();

        if (waterSource == null)
            waterSource = GetComponent<WaterSource>();
    }

    private void Start()
    {
        SetWater(false);
    }

    public void ToggleWater()
    {
        SetWater(!IsWaterOn);
    }

    public void TurnOnWater()
    {
        SetWater(true);
    }

    public void TurnOffWater()
    {
        SetWater(false);
    }

    private void SetWater(bool active)
    {
        IsWaterOn = active;

        if (waterSource != null)
        {
            waterSource.SetFlow(active);
        }
        else if (waterParticle != null)
        {
            if (active)
                waterParticle.Play();
            else
                waterParticle.Stop();
        }

        if (waterSource == null && waterHitZone != null)
        {
            waterHitZone.enabled = active;
        }

        if (statusIndicatorRenderer != null)
        {
            statusIndicatorRenderer.material = active ? waterOnMaterial : waterOffMaterial;
        }
    }
}
