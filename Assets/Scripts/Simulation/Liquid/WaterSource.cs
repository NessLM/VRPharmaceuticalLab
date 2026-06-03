using UnityEngine;

public class WaterSource : MonoBehaviour
{
    [Header("Liquid")]
    [SerializeField] private LiquidData liquidData;

    [Header("Flow")]
    [SerializeField] private bool startFlowing = false;
    [SerializeField] private float flowRateMlPerSecond = 25f;

    [Header("References")]
    [SerializeField] private ParticleSystem waterParticle;
    [SerializeField] private Collider sourceCollider;
    [SerializeField] private bool disableColliderWhenNotFlowing = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool debugIsFlowing = false;

    private bool isFlowing;

    public bool IsFlowing => isFlowing && gameObject.activeInHierarchy;
    public float FlowRateMlPerSecond => flowRateMlPerSecond;
    public LiquidData LiquidData => liquidData;

    private void Awake()
    {
        if (sourceCollider == null)
            sourceCollider = GetComponent<Collider>();

        if (sourceCollider != null)
            sourceCollider.isTrigger = true;

        SetFlow(startFlowing);
    }

    private void OnValidate()
    {
        flowRateMlPerSecond = Mathf.Max(0f, flowRateMlPerSecond);

        if (sourceCollider == null)
            sourceCollider = GetComponent<Collider>();

        if (sourceCollider != null)
            sourceCollider.isTrigger = true;
    }

    public void TurnOn()
    {
        SetFlow(true);
    }

    public void TurnOff()
    {
        SetFlow(false);
    }

    public void ToggleFlow()
    {
        SetFlow(!isFlowing);
    }

    public void SetFlow(bool active)
    {
        if (active && !gameObject.activeSelf)
            gameObject.SetActive(true);

        isFlowing = active;
        debugIsFlowing = isFlowing;

        if (sourceCollider != null)
            sourceCollider.enabled = active || !disableColliderWhenNotFlowing;

        if (debugLogs)
            Debug.Log($"{name} water flow: {(active ? "ON" : "OFF")}", this);

        if (waterParticle == null)
            return;

        if (active)
            waterParticle.Play();
        else
            waterParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
