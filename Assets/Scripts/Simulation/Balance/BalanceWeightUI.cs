using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// World-space UI for selecting weight pieces (anak timbangan) for the analytical balance.
/// Each available weight has a 3D GameObject that teleports to the right pan when selected.
/// Supports: 5, 10, 20, 50, 100, 200, 500 mg.
/// Attach to: UI Canvas next to the Timbangan Neraca.
/// </summary>
public class BalanceWeightUI : MonoBehaviour
{
    [Header("Balance Reference")]
    [SerializeField] private BalanceController balanceController;

    [Header("Weight Entries")]
    [Tooltip("Configure each weight piece: its value in mg, its 3D object, and its slot on the right pan.")]
    [SerializeField] private WeightEntry[] weightEntries = new WeightEntry[]
    {
        new WeightEntry { weightMg = 5 },
        new WeightEntry { weightMg = 10 },
        new WeightEntry { weightMg = 20 },
        new WeightEntry { weightMg = 50 },
        new WeightEntry { weightMg = 100 },
        new WeightEntry { weightMg = 200 },
        new WeightEntry { weightMg = 500 }
    };

    [Header("UI Display")]
    [SerializeField] private TMP_Text totalWeightText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text leftWeightText;

    [Header("Events")]
    public UnityEvent<float> onTotalWeightChanged;
    public UnityEvent onBalanceAchieved;

    private float totalRightWeightMg = 0f;

    [Serializable]
    public class WeightEntry
    {
        [Tooltip("Weight value in milligrams")]
        public int weightMg;
        [Tooltip("3D GameObject of this weight piece. It will be teleported to the right pan when active.")]
        public GameObject weightObject;
        [Tooltip("Position and rotation target on the right pan for this specific weight.")]
        public Transform panSlot;
        [HideInInspector]
        public bool isActive;
    }

    private void Start()
    {
        // Deactivate all weight objects at startup
        foreach (var entry in weightEntries)
        {
            if (entry.weightObject != null)
                entry.weightObject.SetActive(false);
        }

        UpdateUI();
    }

    /// <summary>Adds a weight piece to the right pan. Called by UI buttons passing the mg value.</summary>
    public void AddWeight(int weightMg)
    {
        WeightEntry entry = FindEntry(weightMg);
        if (entry == null || entry.isActive) return;

        entry.isActive = true;
        totalRightWeightMg += weightMg;

        if (entry.weightObject != null)
        {
            entry.weightObject.SetActive(true);
            TeleportToPan(entry);
        }

        ApplyToBalance();
        UpdateUI();
    }

    /// <summary>Removes a weight piece from the right pan.</summary>
    public void RemoveWeight(int weightMg)
    {
        WeightEntry entry = FindEntry(weightMg);
        if (entry == null || !entry.isActive) return;

        entry.isActive = false;
        totalRightWeightMg = Mathf.Max(0f, totalRightWeightMg - weightMg);

        if (entry.weightObject != null)
            entry.weightObject.SetActive(false);

        ApplyToBalance();
        UpdateUI();
    }

    /// <summary>Toggles a weight piece on or off. Convenient for toggle buttons in UI.</summary>
    public void ToggleWeight(int weightMg)
    {
        WeightEntry entry = FindEntry(weightMg);
        if (entry == null) return;

        if (entry.isActive) RemoveWeight(weightMg);
        else AddWeight(weightMg);
    }

    // String overloads for UnityEvent binding in Inspector
    public void AddWeightString(string weightMgStr)
    {
        if (int.TryParse(weightMgStr, out int w)) AddWeight(w);
    }

    public void RemoveWeightString(string weightMgStr)
    {
        if (int.TryParse(weightMgStr, out int w)) RemoveWeight(w);
    }

    public void ToggleWeightString(string weightMgStr)
    {
        if (int.TryParse(weightMgStr, out int w)) ToggleWeight(w);
    }

    /// <summary>Clears all weights from the right pan.</summary>
    public void ClearAllWeights()
    {
        foreach (var entry in weightEntries)
        {
            if (!entry.isActive) continue;
            entry.isActive = false;
            if (entry.weightObject != null)
                entry.weightObject.SetActive(false);
        }

        totalRightWeightMg = 0f;
        ApplyToBalance();
        UpdateUI();
    }

    private void TeleportToPan(WeightEntry entry)
    {
        if (entry.panSlot != null)
        {
            entry.weightObject.transform.position = entry.panSlot.position;
            entry.weightObject.transform.rotation = entry.panSlot.rotation;
        }
    }

    private void ApplyToBalance()
    {
        if (balanceController != null)
            balanceController.SetRightWeight(totalRightWeightMg);

        onTotalWeightChanged?.Invoke(totalRightWeightMg);

        if (balanceController != null && balanceController.IsBalanced && totalRightWeightMg > 0f)
            onBalanceAchieved?.Invoke();
    }

    private void UpdateUI()
    {
        if (totalWeightText != null)
            totalWeightText.text = $"Kanan: {totalRightWeightMg:F0} mg";

        if (leftWeightText != null && balanceController != null)
            leftWeightText.text = $"Kiri: {balanceController.LeftWeightMg:F0} mg";

        if (statusText != null && balanceController != null)
        {
            if (balanceController.IsBalanced && totalRightWeightMg > 0f)
            {
                statusText.text = "SEIMBANG";
            }
            else
            {
                float diff = balanceController.LeftWeightMg - totalRightWeightMg;
                statusText.text = diff > 0f
                    ? $"Kurang {diff:F0} mg"
                    : $"Lebih {Mathf.Abs(diff):F0} mg";
            }
        }
    }

    private WeightEntry FindEntry(int weightMg)
    {
        return Array.Find(weightEntries, e => e.weightMg == weightMg);
    }

    /// <summary>Returns the list of currently active (placed) weights.</summary>
    public List<int> GetActiveWeights()
    {
        var result = new List<int>();
        foreach (var entry in weightEntries)
            if (entry.isActive) result.Add(entry.weightMg);
        return result;
    }
}
