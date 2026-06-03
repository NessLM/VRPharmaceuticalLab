using System.Collections.Generic;
using UnityEngine;

public class WasherFillZone : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private LiquidContainer targetContainer;

    [Header("Optional Tilt Check")]
    [SerializeField] private bool requireMouthFacingUp = false;
    [SerializeField] private Transform mouthDirection;
    [SerializeField] private float maxReceiveTiltAngle = 65f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private int activeWaterSourceCount;

    private static WasherFillZone[] s_CachedZones;
    private static float s_LastCacheTime = -1f;
    private const float CACHE_REFRESH_INTERVAL = 2f;

    private readonly HashSet<WaterSource> activeSources = new HashSet<WaterSource>();

    private void Reset()
    {
        targetContainer = GetComponentInParent<LiquidContainer>();

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Awake()
    {
        if (targetContainer == null)
            targetContainer = GetComponentInParent<LiquidContainer>();
    }

    private void OnEnable() => InvalidateZoneCache();

    private void OnDisable()
    {
        activeSources.Clear();
        activeWaterSourceCount = 0;
        InvalidateZoneCache();
    }

    private void OnValidate()
    {
        maxReceiveTiltAngle = Mathf.Clamp(maxReceiveTiltAngle, 0f, 180f);

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    /// <summary>Sets the target container that will receive liquid.</summary>
    public void SetTargetContainer(LiquidContainer container)
    {
        targetContainer = container;
    }

    private void OnTriggerEnter(Collider other)
    {
        WaterSource source = FindWaterSource(other);
        if (source != null)
            RegisterSource(source);
    }

    private void OnTriggerExit(Collider other)
    {
        WaterSource source = FindWaterSource(other);
        if (source != null)
            UnregisterSource(source);
    }

    private void OnTriggerStay(Collider other)
    {
        WaterSource source = FindWaterSource(other);

        if (source == null || !source.IsFlowing)
            return;

        RegisterSource(source);

        if (targetContainer == null)
            return;

        if (requireMouthFacingUp && !CanReceiveLiquid())
            return;

        if (!IsPrimaryRecipient(source))
            return;

        float amount = source.FlowRateMlPerSecond * Time.deltaTime;
        targetContainer.AddLiquid(amount, source.LiquidData);

        if (debugLogs)
            Debug.Log($"{name} filled {targetContainer.name} by {amount:0.###} ml", this);
    }

    private bool CanReceiveLiquid()
    {
        Transform direction = mouthDirection != null ? mouthDirection : transform;
        float angle = Vector3.Angle(direction.up, Vector3.up);
        return angle <= maxReceiveTiltAngle;
    }

    /// <summary>
    /// Returns true only if this FillZone is the closest one to the given WaterSource.
    /// Prevents multiple containers from being filled by the same stream simultaneously.
    /// </summary>
    private bool IsPrimaryRecipient(WaterSource source)
    {
        float myDistSq = (transform.position - source.transform.position).sqrMagnitude;

        foreach (WasherFillZone zone in GetAllZones())
        {
            if (zone == this || !zone.isActiveAndEnabled)
                continue;

            if (!zone.HasActiveSource(source) || zone.targetContainer == null)
                continue;

            if (zone.requireMouthFacingUp && !zone.CanReceiveLiquid())
                continue;

            float otherDistSq = (zone.transform.position - source.transform.position).sqrMagnitude;
            if (otherDistSq < myDistSq)
                return false;
        }

        return true;
    }

    private WaterSource FindWaterSource(Collider other)
    {
        WaterSource source = other.GetComponent<WaterSource>();
        if (source == null)
            source = other.GetComponentInParent<WaterSource>();

        return source;
    }

    private void RegisterSource(WaterSource source)
    {
        if (source == null)
            return;

        activeSources.Add(source);
        activeWaterSourceCount = activeSources.Count;
    }

    private void UnregisterSource(WaterSource source)
    {
        if (source == null)
            return;

        activeSources.Remove(source);
        activeWaterSourceCount = activeSources.Count;
    }

    private bool HasActiveSource(WaterSource source)
    {
        if (source == null)
            return false;

        activeSources.RemoveWhere(activeSource => activeSource == null || !activeSource.isActiveAndEnabled);
        activeWaterSourceCount = activeSources.Count;
        return activeSources.Contains(source);
    }

    private static WasherFillZone[] GetAllZones()
    {
        if (s_CachedZones == null || Time.time - s_LastCacheTime > CACHE_REFRESH_INTERVAL)
        {
            s_CachedZones = FindObjectsByType<WasherFillZone>(FindObjectsSortMode.None);
            s_LastCacheTime = Time.time;
        }

        return s_CachedZones;
    }

    private static void InvalidateZoneCache() => s_CachedZones = null;
}
