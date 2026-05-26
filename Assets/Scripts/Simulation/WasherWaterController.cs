using UnityEngine;

public class WasherWaterController : MonoBehaviour
{
    [Header("Water Particle")]
    public ParticleSystem waterParticle;

    [Header("Water Hit Zone")]
    public Collider waterHitZone;

    [Header("Status Indicator")]
    public Renderer statusIndicatorRenderer;
    public Material waterOnMaterial;
    public Material waterOffMaterial;

    public bool IsWaterOn { get; private set; }

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

        if (waterParticle != null)
        {
            if (active)
                waterParticle.Play();
            else
                waterParticle.Stop();
        }

        if (waterHitZone != null)
        {
            waterHitZone.enabled = active;
        }

        if (statusIndicatorRenderer != null)
        {
            statusIndicatorRenderer.material = active ? waterOnMaterial : waterOffMaterial;
        }
    }
}