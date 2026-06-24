using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
public class PowderDepositZone : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeightingZone rightZone;

    [Header("Deposit Settings")]
    [SerializeField] private float depositStepMg = 50f;
    [SerializeField] private float maxDepositMg = 500f;
    [SerializeField] private bool acceptDeposits = true;

    [Header("Validation")]
    [SerializeField] private bool requireSpoonHeld = true;
    [SerializeField] private bool requireTipNearZone = true;
    [SerializeField] private float tipToleranceMeters = 0.08f;

    [SerializeField] private bool allowPhysicalRightPanTarget = true;
    [SerializeField] private float minimumRightPanTargetGrams = 0.001f;

    [Header("Powder Visual on Left Pan")]
    [SerializeField] private PowderVisualLevelSwitcher depositVisual;

    [Header("Warning Display")]
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private float warningDuration = 2.5f;

    [Header("Debug")]
    [SerializeField] private float depositedMg;
    [SerializeField] private bool debugLogs;

    [Header("Events")]
    public UnityEvent<float> onDepositChanged;
    public UnityEvent onTargetNotAccepted;

    [Tooltip("Dipanggil tiap deposit sukses dengan jumlah (mg) yang baru ditambahkan. " +
             "Dipakai untuk floating amount text Salep.")]
    public UnityEvent<float> onPowderDeposited;

    private float warningTimer;

    private readonly Dictionary<HornSpoon, int> spoonContactCounts = new Dictionary<HornSpoon, int>();
    private readonly HashSet<HornSpoon> depositedDuringCurrentContact = new HashSet<HornSpoon>();

    public float DepositedMg => depositedMg;
    public float DepositedGrams => depositedMg / 1000f;
    public bool HasPowder => depositedMg > 0.001f;

    public bool AcceptDeposits => acceptDeposits;

    public void SetAcceptingDeposits(bool value)
    {
        acceptDeposits = value;

        if (!acceptDeposits)
        {
            spoonContactCounts.Clear();
            depositedDuringCurrentContact.Clear();
        }
    }

    /// <summary>
    /// Salep memakai target resep tetap, jadi tidak wajib menaruh anak timbangan di pan
    /// kanan. Difenhidramin tetap pakai default (true) lewat restore di SalepBench.ResetAll.
    /// </summary>
    public void SetRequireRightPanTarget(bool require)
    {
        allowPhysicalRightPanTarget = require;
    }

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        if (rightZone == null)
            rightZone = FindSceneComponentByName<WeightingZone>("Collider_Piring_Kanan");

        if (rightZone == null)
            rightZone = FindSceneComponentByName<WeightingZone>("RightWeighingZone");

        UpdateVisualAndNotify();
    }

    private void Update()
    {
        UpdateWarningTimer();
    }

    private void OnTriggerEnter(Collider other)
    {
        HornSpoon spoon = other.GetComponentInParent<HornSpoon>();
        if (spoon == null)
            return;

        if (!acceptDeposits)
            return;

        if (!spoonContactCounts.ContainsKey(spoon))
            spoonContactCounts.Add(spoon, 0);

        spoonContactCounts[spoon]++;

        if (!HasAcceptedTarget())
        {
            ShowWarning("Taruh anak timbangan kanan dulu!");
            onTargetNotAccepted?.Invoke();
            return;
        }

        TryDepositFromSpoon(spoon);
    }

    private void OnTriggerStay(Collider other)
    {
        HornSpoon spoon = other.GetComponentInParent<HornSpoon>();
        if (spoon == null)
            return;

        if (!acceptDeposits)
            return;

        if (!HasAcceptedTarget())
            return;

        TryDepositFromSpoon(spoon);
    }

    private void OnTriggerExit(Collider other)
    {
        HornSpoon spoon = other.GetComponentInParent<HornSpoon>();
        if (spoon == null)
            return;

        if (!spoonContactCounts.ContainsKey(spoon))
            return;

        spoonContactCounts[spoon]--;

        if (spoonContactCounts[spoon] <= 0)
        {
            spoonContactCounts.Remove(spoon);
            depositedDuringCurrentContact.Remove(spoon);
        }
    }

    public bool TryDepositFromSpoon(HornSpoon spoon)
    {
        if (spoon == null)
            return false;

        if (!acceptDeposits)
            return false;

        if (depositedDuringCurrentContact.Contains(spoon))
            return false;

        if (!HasAcceptedTarget())
            return false;

        if (requireSpoonHeld && !IsSpoonHeld(spoon))
            return false;

        if (requireTipNearZone && !IsSpoonTipNearZone(spoon))
            return false;

        if (spoon.IsEmpty)
            return false;

        if (depositedMg >= maxDepositMg)
            return false;

        float remainingCapacity = Mathf.Max(0f, maxDepositMg - depositedMg);
        float amountToTakeMg = Mathf.Min(depositStepMg, remainingCapacity);

        float removedMg = spoon.RemovePowder(amountToTakeMg);

        if (removedMg <= 0.001f)
            return false;

        depositedMg = Mathf.Clamp(depositedMg + removedMg, 0f, maxDepositMg);
        depositedDuringCurrentContact.Add(spoon);

        UpdateVisualAndNotify();
        onPowderDeposited?.Invoke(removedMg);

        if (debugLogs)
            Debug.Log($"[PowderDepositZone] Deposit +{removedMg:0.###} mg. Total = {depositedMg:0.###} mg", this);

        return true;
    }

    public void ResetDeposit()
    {
        depositedMg = 0f;
        spoonContactCounts.Clear();
        depositedDuringCurrentContact.Clear();

        UpdateVisualAndNotify();

        if (debugLogs)
            Debug.Log("[PowderDepositZone] Reset deposit.", this);
    }

    public void SetDepositMg(float amountMg)
    {
        depositedMg = Mathf.Max(0f, amountMg);

        if (depositVisual != null)
            depositVisual.SetAmountMg(depositedMg);

        onDepositChanged?.Invoke(DepositedGrams);
    }
    public bool IsAtTargetMg(float targetMg, float toleranceMg)
    {
        return Mathf.Abs(depositedMg - targetMg) <= Mathf.Max(0f, toleranceMg);
    }

    /// <summary>Warnai visual bubuk di piring kiri sesuai bahan (Salep). Aman untuk Sirup.</summary>
    public void SetDepositVisualTint(Color color)
    {
        if (depositVisual != null)
            depositVisual.SetTint(color);
    }

    /// <summary>Kembalikan warna visual bubuk ke default material.</summary>
    public void ClearDepositVisualTint()
    {
        if (depositVisual != null)
            depositVisual.ClearTint();
    }

    public void ConfigureForRecipe(float stepMg, float maxMg, float visualMaxMg)
    {
        depositStepMg = Mathf.Max(1f, stepMg);
        maxDepositMg = Mathf.Max(depositStepMg, maxMg);

        if (depositVisual != null)
            depositVisual.SetMaxVisualMg(visualMaxMg);

        depositedMg = Mathf.Clamp(depositedMg, 0f, maxDepositMg);
        UpdateVisualAndNotify();
    }

    private void UpdateVisualAndNotify()
    {
        if (depositVisual != null)
            depositVisual.SetAmountMg(depositedMg);

        onDepositChanged?.Invoke(DepositedGrams);
    }

    private bool HasAcceptedTarget()
    {
        if (!allowPhysicalRightPanTarget)
            return true;

        return rightZone != null && rightZone.TotalGrams >= minimumRightPanTargetGrams;
    }

    private bool IsSpoonHeld(HornSpoon spoon)
    {
        XRGrabInteractable grab = spoon.GetComponent<XRGrabInteractable>();
        return grab != null && grab.isSelected;
    }

    private bool IsSpoonTipNearZone(HornSpoon spoon)
    {
        if (spoon == null || spoon.TipTransform == null)
            return true;

        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider == null)
            return true;

        Vector3 tipPosition = spoon.TipTransform.position;
        Vector3 closestPoint = zoneCollider.ClosestPoint(tipPosition);
        float distance = Vector3.Distance(tipPosition, closestPoint);

        return distance <= tipToleranceMeters;
    }

    private void ShowWarning(string message)
    {
        if (warningText == null)
            return;

        warningText.text = message;
        warningText.gameObject.SetActive(true);
        warningTimer = warningDuration;
    }

    private void UpdateWarningTimer()
    {
        if (warningTimer <= 0f)
            return;

        warningTimer -= Time.deltaTime;

        if (warningTimer <= 0f && warningText != null)
            warningText.gameObject.SetActive(false);
    }

    private T FindSceneComponentByName<T>(string objectName) where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();

        foreach (T component in components)
        {
            if (component == null || component.gameObject == null)
                continue;

            if (!component.gameObject.scene.IsValid())
                continue;

            if (component.name == objectName)
                return component;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        depositStepMg = Mathf.Max(1f, depositStepMg);
        maxDepositMg = Mathf.Max(depositStepMg, maxDepositMg);
        tipToleranceMeters = Mathf.Max(0.001f, tipToleranceMeters);
        minimumRightPanTargetGrams = Mathf.Max(0f, minimumRightPanTargetGrams);
        warningDuration = Mathf.Max(0.1f, warningDuration);
        depositedMg = Mathf.Clamp(depositedMg, 0f, maxDepositMg);
    }
#endif
}