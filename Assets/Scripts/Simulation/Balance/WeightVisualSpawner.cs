using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages visual weight GameObjects on the right pan of the analytical balance.
/// Creates a "RightWeightVisualAnchor" child under Balance_WeightRight with named slots,
/// then re-parents placed weight objects into the anchor on Accept so they tilt with the pan.
///
/// Works in conjunction with VirtualWeightSelector (with reparentToRightPan enabled).
/// Subscribe to onTargetAccepted / onTargetCleared on VirtualWeightSelector.
///
/// Attach to: timbanganNeraca.
/// Wire: weightSelector, rightPan (Balance_WeightRight), weightEntries.
/// </summary>
public class WeightVisualSpawner : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("VirtualWeightSelector on WeightSelectorCanvas/WeightSelectorPanel.")]
    [SerializeField] private VirtualWeightSelector weightSelector;
    [Tooltip("Transform of Balance_WeightRight (auto-found if not set).")]
    [SerializeField] private Transform rightPan;

    [Header("Weight Object Entries")]
    [Tooltip("Map gram values to their physical weight GameObjects. " +
             "Add one entry per available weight denomination.")]
    [SerializeField] private WeightEntry[] weightEntries = new WeightEntry[]
    {
        new WeightEntry { grams = 0.200f, label = "200mg" },
        new WeightEntry { grams = 0.500f, label = "500mg" },
        new WeightEntry { grams = 1f,     label = "1g"    },
        new WeightEntry { grams = 2f,     label = "2g"    },
        new WeightEntry { grams = 5f,     label = "5g"    },
        new WeightEntry { grams = 10f,    label = "10g"   },
        new WeightEntry { grams = 20f,    label = "20g"   },
        new WeightEntry { grams = 50f,    label = "50g"   },
        new WeightEntry { grams = 100f,   label = "100g"  },
        new WeightEntry { grams = 200f,   label = "200g"  },
        new WeightEntry { grams = 500f,   label = "500g"  },
    };

    [Header("Anchor Layout")]
    [Tooltip("Number of named slot children to pre-create under the anchor.")]
    [SerializeField] private int slotCount = 12;
    [Tooltip("Y-spacing between stacked weight visuals (local units of Balance_WeightRight).")]
    [SerializeField] private float slotSpacingY = 0.005f;
    [Tooltip("Local offset from Balance_WeightRight origin where the first slot is placed.")]
    [SerializeField] private Vector3 anchorLocalOffset = new Vector3(0f, 0.012f, 0f);

    // ── Runtime ───────────────────────────────────────────────────────────────

    [System.Serializable]
    public class WeightEntry
    {
        public float       grams;
        public string      label;
        [Tooltip("The weight GameObject. Must be a child of timbanganNeraca.")]
        public GameObject  weightObject;

        [HideInInspector] public Transform originalParent;
    }

    private Transform _anchor;
    private Transform[] _slots;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-find Balance_WeightRight in hierarchy if not set.
        if (rightPan == null)
            rightPan = transform.Find("Balance_WeightRight");

        // Save original parents before anything moves.
        foreach (var entry in weightEntries)
        {
            if (entry.weightObject != null)
                entry.originalParent = entry.weightObject.transform.parent;
        }

        CreateAnchorAndSlots();
    }

    private void OnEnable()
    {
        if (weightSelector != null)
        {
            weightSelector.onTargetAccepted.AddListener(OnAccepted);
            weightSelector.onTargetCleared.AddListener(OnReset);
        }
    }

    private void OnDisable()
    {
        if (weightSelector != null)
        {
            weightSelector.onTargetAccepted.RemoveListener(OnAccepted);
            weightSelector.onTargetCleared.RemoveListener(OnReset);
        }
    }

    // ── Anchor Setup ──────────────────────────────────────────────────────────

    private void CreateAnchorAndSlots()
    {
        if (rightPan == null) return;

        // Find or create RightWeightVisualAnchor.
        _anchor = rightPan.Find("RightWeightVisualAnchor");
        if (_anchor == null)
        {
            _anchor = new GameObject("RightWeightVisualAnchor").transform;
            _anchor.SetParent(rightPan, false);
        }
        _anchor.localPosition = anchorLocalOffset;
        _anchor.localRotation = Quaternion.identity;

        // Create or reuse named slot children.
        _slots = new Transform[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            string slotName = $"RightWeightSlot_{i + 1:D2}";
            Transform existing = _anchor.Find(slotName);
            if (existing != null)
            {
                _slots[i] = existing;
            }
            else
            {
                var slot = new GameObject(slotName).transform;
                slot.SetParent(_anchor, false);
                slot.localPosition = new Vector3(0f, i * slotSpacingY, 0f);
                slot.localRotation = Quaternion.identity;
                _slots[i] = slot;
            }
        }
    }

    // ── Event Handlers ────────────────────────────────────────────────────────

    /// <summary>
    /// Called after VirtualWeightSelector places weights.
    /// Re-parents active weight GOs into the anchor so they tilt with the pan.
    /// </summary>
    private void OnAccepted(float _)
    {
        // Delay one frame to let VirtualWeightSelector finish placement first.
        StartCoroutine(ReparentAfterPlacement());
    }

    private IEnumerator ReparentAfterPlacement()
    {
        yield return null;

        if (_anchor == null) yield break;

        int slotIndex = 0;
        foreach (var entry in weightEntries)
        {
            if (entry.weightObject == null) continue;
            if (!entry.weightObject.activeSelf) continue;
            if (slotIndex >= _slots.Length) break;

            // Re-parent to the slot so the weight moves with the pan.
            entry.weightObject.transform.SetParent(_slots[slotIndex], false);
            entry.weightObject.transform.localPosition = Vector3.zero;
            entry.weightObject.transform.localRotation = Quaternion.identity;

            // Disable physics — visual only.
            if (entry.weightObject.TryGetComponent<Rigidbody>(out var rb))
                rb.isKinematic = true;

            slotIndex++;
        }
    }

    /// <summary>
    /// Called when VirtualWeightSelector resets.
    /// Restores weight GOs to their original parents before VirtualWeightSelector hides them.
    /// </summary>
    private void OnReset()
    {
        foreach (var entry in weightEntries)
        {
            if (entry.weightObject == null) continue;
            if (entry.originalParent != null
                && entry.weightObject.transform.parent != entry.originalParent)
            {
                entry.weightObject.transform.SetParent(entry.originalParent, false);
            }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns the anchor transform for manual placement if needed.</summary>
    public Transform GetAnchor() => _anchor;

    /// <summary>Returns the local position of slot at index (0-based).</summary>
    public Vector3 GetSlotLocalPosition(int slotIndex)
    {
        if (_slots != null && slotIndex < _slots.Length)
            return _slots[slotIndex].localPosition;
        return anchorLocalOffset + new Vector3(0f, slotIndex * slotSpacingY, 0f);
    }
}
