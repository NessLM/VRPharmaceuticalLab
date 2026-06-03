using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Trigger zone positioned above a balance scale pan.
/// Detects WeightItem, HornSpoon, PowderPayload, and BalanceMassSource components
/// entering the pan area.
/// Sums logical gram values and fires onMassChanged events when the total changes.
/// Requires a BoxCollider with isTrigger = true on this GameObject.
///
/// Deduplication rule:
///   If an object has BOTH WeightItem and BalanceMassSource, only WeightItem is counted.
///   This prevents double-counting on objects with multiple mass components.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class WeightingZone : MonoBehaviour
{
    [Header("Zone Identity")]
    [Tooltip("Human-readable label used in debug output and UI displays.")]
    [SerializeField] private string zoneName = "Zone";

    [Header("Detection")]
    [Tooltip("Minimum gram change required to fire onMassChanged (reduces redundant events).")]
    [SerializeField] private float massChangeThreshold = 0.001f;

    [Header("Events")]
    public UnityEvent<float> onMassChanged;

    private readonly HashSet<WeightItem> trackedWeights = new HashSet<WeightItem>();
    private readonly HashSet<HornSpoon> trackedSpoons = new HashSet<HornSpoon>();
    private readonly HashSet<PowderPayload> trackedPayloads = new HashSet<PowderPayload>();
    private readonly HashSet<BalanceMassSource> trackedMassSources = new HashSet<BalanceMassSource>();

    private float lastReportedMass = -1f;

    /// <summary>
    /// Current total logical mass in grams inside this zone.
    /// Deduplicates: BalanceMassSource is skipped if the same object also has WeightItem.
    /// </summary>
    public float TotalGrams
    {
        get
        {
            float total = 0f;

            foreach (WeightItem w in trackedWeights)
                if (w != null) total += w.GramValue;

            foreach (HornSpoon s in trackedSpoons)
                if (s != null) total += s.CurrentAmountMg / 1000f; // mg → g

            foreach (PowderPayload p in trackedPayloads)
                if (p != null) total += p.GramValue;

            foreach (BalanceMassSource b in trackedMassSources)
            {
                // Skip if the same GameObject also contributes via WeightItem (prevents double-count)
                if (b != null && b.GetComponent<WeightItem>() == null)
                    total += b.Grams;
            }

            return total;
        }
    }

    /// <summary>Number of distinct weight items currently tracked in this zone.</summary>
    public int TrackedWeightCount => trackedWeights.Count + trackedMassSources.Count;

    public string ZoneName => zoneName;

    private void Awake()
    {
        // Ensure the collider is always a trigger
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        bool changed = false;

        WeightItem w = other.GetComponentInParent<WeightItem>();
        if (w != null && trackedWeights.Add(w)) changed = true;

        HornSpoon s = other.GetComponentInParent<HornSpoon>();
        if (s != null && trackedSpoons.Add(s)) changed = true;

        PowderPayload p = other.GetComponentInParent<PowderPayload>();
        if (p != null && trackedPayloads.Add(p)) changed = true;

        BalanceMassSource b = other.GetComponentInParent<BalanceMassSource>();
        if (b != null && trackedMassSources.Add(b)) changed = true;

        if (changed) NotifyMassChange();
    }

    private void OnTriggerExit(Collider other)
    {
        bool changed = false;

        WeightItem w = other.GetComponentInParent<WeightItem>();
        if (w != null && trackedWeights.Remove(w)) changed = true;

        HornSpoon s = other.GetComponentInParent<HornSpoon>();
        if (s != null && trackedSpoons.Remove(s)) changed = true;

        PowderPayload p = other.GetComponentInParent<PowderPayload>();
        if (p != null && trackedPayloads.Remove(p)) changed = true;

        BalanceMassSource b = other.GetComponentInParent<BalanceMassSource>();
        if (b != null && trackedMassSources.Remove(b)) changed = true;

        if (changed) NotifyMassChange();
    }

    private void Update()
    {
        // Poll for continuously-changing values (e.g. HornSpoon amount changing while inside zone)
        float current = TotalGrams;
        if (Mathf.Abs(current - lastReportedMass) >= massChangeThreshold)
            NotifyMassChange();
    }

    private void NotifyMassChange()
    {
        lastReportedMass = TotalGrams;
        onMassChanged?.Invoke(lastReportedMass);
    }

    /// <summary>Manually triggers a mass update notification (useful after external state changes).</summary>
    public void ForceRefresh() => NotifyMassChange();

    private void OnDrawGizmosSelected()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) return;
        Gizmos.color = new Color(0.1f, 0.9f, 0.4f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(col.center, col.size);
        Gizmos.color = new Color(0.1f, 0.9f, 0.4f, 0.85f);
        Gizmos.DrawWireCube(col.center, col.size);
    }
}
