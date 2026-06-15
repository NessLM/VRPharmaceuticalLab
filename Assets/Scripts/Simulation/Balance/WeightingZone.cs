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
    private enum AcceptedPanContent
    {
        Any = 0,
        MaterialOnly = 1,
        WeightsOnly = 2
    }

    [Header("Zone Identity")]
    [Tooltip("Human-readable label used in debug output and UI displays.")]
    [SerializeField] private string zoneName = "Zone";

    [Header("Detection")]
    [Tooltip("Minimum gram change required to fire onMassChanged (reduces redundant events).")]
    [SerializeField] private float massChangeThreshold = 0.001f;
    [SerializeField] private bool requireParchmentBeforeCounting;
    [SerializeField] private PerkamenSnapTarget parchmentSnapTarget;
    [SerializeField] private AcceptedPanContent acceptedContent = AcceptedPanContent.Any;

    [Header("Debug")]
    [SerializeField] private float debugTotalGrams;
    [SerializeField] private int debugItemCount;
    [SerializeField] private List<string> debugItemNames = new List<string>();

    [Header("Events")]
    public UnityEvent<float> onMassChanged;

    private readonly HashSet<WeightItem> trackedWeights = new HashSet<WeightItem>();
    private readonly Dictionary<WeightItem, HashSet<Collider>> trackedWeightColliders = new Dictionary<WeightItem, HashSet<Collider>>();
    private readonly HashSet<HornSpoon> trackedSpoons = new HashSet<HornSpoon>();
    private readonly HashSet<PowderPayload> trackedPayloads = new HashSet<PowderPayload>();
    private readonly HashSet<BalanceMassSource> trackedMassSources = new HashSet<BalanceMassSource>();

    private bool reportedParchmentPresent;
    private float lastReportedMass = -1f;

    /// <summary>
    /// Current total logical mass in grams inside this zone.
    /// Deduplicates: BalanceMassSource is skipped if the same object also has WeightItem.
    /// </summary>
    public float TotalGrams => ComputeTotalGrams(true);

    /// <summary>Number of distinct weight items currently tracked in this zone.</summary>
    public int TrackedWeightCount => trackedWeights.Count + trackedMassSources.Count;

    public string ZoneName => zoneName;
    public bool RequireParchmentFirst
    {
        get => requireParchmentBeforeCounting;
        set
        {
            if (requireParchmentBeforeCounting == value)
                return;

            requireParchmentBeforeCounting = value;
            RecalculateMass();
        }
    }

    public bool HasParchment => reportedParchmentPresent || (parchmentSnapTarget != null && parchmentSnapTarget.HasSnapped);
    public GameObject ParchmentObject => parchmentSnapTarget != null ? parchmentSnapTarget.SnappedParchment : null;

    private void Awake()
    {
        // Ensure the collider is always a trigger
        GetComponent<BoxCollider>().isTrigger = true;
        ResolveParchmentSnapTarget();
    }

    private void OnEnable()
    {
        ResolveParchmentSnapTarget();
        SubscribeToParchmentTarget();
    }

    private void OnDisable()
    {
        UnsubscribeFromParchmentTarget();
        trackedWeights.Clear();
        trackedWeightColliders.Clear();
        trackedSpoons.Clear();
        trackedPayloads.Clear();
        trackedMassSources.Clear();
        UpdateDebugInfo(0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        TrackCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TrackCollider(other);
    }

    private void TrackCollider(Collider other)
    {
        if (!CanTrackCollider(other))
            return;

        bool changed = false;

        WeightItem w = other.GetComponentInParent<WeightItem>();
        if (w != null && TrackWeightCollider(w, other)) changed = true;

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
        if (w != null && UntrackWeightCollider(w, other)) changed = true;

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
        ClearInvalidItemsInternal();
        float current = ComputeTotalGrams(true);
        if (Mathf.Abs(current - lastReportedMass) >= massChangeThreshold)
            NotifyMassChange(current);
    }

    private void NotifyMassChange()
    {
        NotifyMassChange(ComputeTotalGrams(true));
    }

    private void NotifyMassChange(float grams)
    {
        UpdateDebugInfo(grams);
        lastReportedMass = grams;
        onMassChanged?.Invoke(lastReportedMass);
    }

    /// <summary>Manually triggers a mass update notification (useful after external state changes).</summary>
    public void ForceRefresh() => NotifyMassChange();

    public void SetParchmentPresent(bool present)
    {
        if (reportedParchmentPresent == present)
            return;

        reportedParchmentPresent = present;
        RecalculateMass();
    }

    public void RecalculateMass()
    {
        bool removedInvalidItems = ClearInvalidItemsInternal();
        float current = ComputeTotalGrams(true);

        if (removedInvalidItems || Mathf.Abs(current - lastReportedMass) >= massChangeThreshold)
            NotifyMassChange(current);
    }

    public void ClearInvalidItems()
    {
        if (ClearInvalidItemsInternal())
            RecalculateMass();
        else
            UpdateDebugInfo(ComputeTotalGrams(false));
    }

    private bool HasRequiredParchment()
    {
        return !requireParchmentBeforeCounting || HasParchment;
    }

    private bool CanTrackCollider(Collider other)
    {
        if (other == null)
            return false;

        if (IsParchmentCollider(other))
            return false;

        switch (acceptedContent)
        {
            case AcceptedPanContent.MaterialOnly:
                return other.GetComponentInParent<HornSpoon>() != null ||
                       other.GetComponentInParent<PowderPayload>() != null;
            case AcceptedPanContent.WeightsOnly:
                return other.GetComponentInParent<WeightItem>() != null ||
                       other.GetComponentInParent<BalanceMassSource>() != null;
            default:
                return true;
        }
    }

    private bool IsParchmentCollider(Collider other)
    {
        WeightItem item = other.GetComponentInParent<WeightItem>();
        if (item != null && item.IsParchment)
            return true;

        if (other.GetComponentInParent<PerkamenNoGravity>() != null)
            return true;

        Transform root = other.transform;
        while (root.parent != null)
            root = root.parent;

        return root.name.IndexOf("perkamen", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ResolveParchmentSnapTarget()
    {
        if (parchmentSnapTarget == null)
            parchmentSnapTarget = GetComponent<PerkamenSnapTarget>();
    }

    private void SubscribeToParchmentTarget()
    {
        if (parchmentSnapTarget == null)
            return;

        parchmentSnapTarget.onParchmentObjectSnapped.RemoveListener(HandleParchmentStateChanged);
        parchmentSnapTarget.onParchmentRemoved.RemoveListener(HandleParchmentStateChanged);
        parchmentSnapTarget.onParchmentObjectSnapped.AddListener(HandleParchmentStateChanged);
        parchmentSnapTarget.onParchmentRemoved.AddListener(HandleParchmentStateChanged);
    }

    private void UnsubscribeFromParchmentTarget()
    {
        if (parchmentSnapTarget == null)
            return;

        parchmentSnapTarget.onParchmentObjectSnapped.RemoveListener(HandleParchmentStateChanged);
        parchmentSnapTarget.onParchmentRemoved.RemoveListener(HandleParchmentStateChanged);
    }

    private void HandleParchmentStateChanged(GameObject parchment)
    {
        RecalculateMass();
    }

    private float ComputeTotalGrams(bool updateDebugInfo)
    {
        if (!HasRequiredParchment())
        {
            if (updateDebugInfo)
                UpdateDebugInfo(0f);

            return 0f;
        }

        float total = 0f;
        List<string> countedNames = updateDebugInfo ? new List<string>() : null;

        foreach (WeightItem w in trackedWeights)
        {
            if (!IsValidTrackedComponent(w) || !w.ShouldContributeMass)
                continue;

            total += w.Grams;
            countedNames?.Add($"{w.name} ({w.Grams:0.###}g)");
        }

        foreach (HornSpoon s in trackedSpoons)
        {
            if (!IsValidTrackedComponent(s))
                continue;

            float spoonGrams = s.CurrentAmountMg / 1000f;
            total += spoonGrams;
            countedNames?.Add($"{s.name} ({spoonGrams:0.###}g)");
        }

        foreach (PowderPayload p in trackedPayloads)
        {
            if (!IsValidTrackedComponent(p))
                continue;

            total += p.GramValue;
            countedNames?.Add($"{p.name} ({p.GramValue:0.###}g)");
        }

        foreach (BalanceMassSource b in trackedMassSources)
        {
            if (!IsValidTrackedComponent(b))
                continue;

            // Skip if the same GameObject also contributes via WeightItem (prevents double-count)
            if (b.GetComponent<WeightItem>() != null)
                continue;

            total += b.Grams;
            countedNames?.Add($"{b.name} ({b.Grams:0.###}g)");
        }

        if (updateDebugInfo)
            UpdateDebugInfo(total, countedNames);

        return total;
    }

    private bool ClearInvalidItemsInternal()
    {
        bool changed = false;
        changed |= trackedWeights.RemoveWhere(item => !IsValidTrackedComponent(item)) > 0;
        List<WeightItem> invalidWeightEntries = null;
        foreach (KeyValuePair<WeightItem, HashSet<Collider>> entry in trackedWeightColliders)
        {
            if (!IsValidTrackedComponent(entry.Key))
            {
                if (invalidWeightEntries == null)
                    invalidWeightEntries = new List<WeightItem>();

                invalidWeightEntries.Add(entry.Key);
                continue;
            }

            entry.Value.RemoveWhere(collider => collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy);
            if (entry.Value.Count == 0)
            {
                if (invalidWeightEntries == null)
                    invalidWeightEntries = new List<WeightItem>();

                invalidWeightEntries.Add(entry.Key);
            }
        }

        if (invalidWeightEntries != null)
        {
            foreach (WeightItem item in invalidWeightEntries)
            {
                trackedWeightColliders.Remove(item);
                trackedWeights.Remove(item);
            }

            changed = true;
        }

        changed |= trackedSpoons.RemoveWhere(item => !IsValidTrackedComponent(item)) > 0;
        changed |= trackedPayloads.RemoveWhere(item => !IsValidTrackedComponent(item)) > 0;
        changed |= trackedMassSources.RemoveWhere(item => !IsValidTrackedComponent(item)) > 0;
        return changed;
    }

    private bool TrackWeightCollider(WeightItem item, Collider sourceCollider)
    {
        if (item == null || sourceCollider == null)
            return false;

        HashSet<Collider> colliders;
        if (!trackedWeightColliders.TryGetValue(item, out colliders))
        {
            colliders = new HashSet<Collider>();
            trackedWeightColliders.Add(item, colliders);
        }

        bool colliderWasAdded = colliders.Add(sourceCollider);
        bool itemWasAdded = trackedWeights.Add(item);
        return colliderWasAdded || itemWasAdded;
    }

    private bool UntrackWeightCollider(WeightItem item, Collider sourceCollider)
    {
        if (item == null || sourceCollider == null)
            return false;

        HashSet<Collider> colliders;
        if (!trackedWeightColliders.TryGetValue(item, out colliders))
            return trackedWeights.Remove(item);

        bool changed = colliders.Remove(sourceCollider);
        if (colliders.Count > 0)
            return changed;

        trackedWeightColliders.Remove(item);
        return trackedWeights.Remove(item) || changed;
    }

    private bool IsValidTrackedComponent(Component component)
    {
        return component != null &&
               component.gameObject != null &&
               component.gameObject.activeInHierarchy &&
               component is Behaviour behaviour &&
               behaviour.enabled;
    }

    private void UpdateDebugInfo(float totalGrams, List<string> itemNames = null)
    {
        debugTotalGrams = totalGrams;

        if (itemNames == null)
        {
            debugItemCount = 0;
            debugItemNames.Clear();
            return;
        }

        debugItemCount = itemNames.Count;
        debugItemNames.Clear();
        debugItemNames.AddRange(itemNames);
    }

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
