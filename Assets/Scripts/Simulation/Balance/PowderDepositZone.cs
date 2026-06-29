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

    [Tooltip("Margin (m) untuk deteksi LANGSUNG anak timbangan di piring kanan. Peringatan " +
             "'taruh anak timbangan' memakai overlap fisik langsung ke area piring kanan " +
             "(deterministik, tidak terpengaruh status kinematic/tidur rigidbody) sehingga " +
             "anak timbangan yang BENAR-BENAR ada di piring selalu terdeteksi.")]
    [SerializeField] private float rightPanWeightDetectionMargin = 0.05f;

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
    private WorldWarningLabel autoWarningLabel;

    private readonly Dictionary<HornSpoon, int> spoonContactCounts = new Dictionary<HornSpoon, int>();
    private readonly HashSet<HornSpoon> depositedDuringCurrentContact = new HashSet<HornSpoon>();

    public float DepositedMg => depositedMg;
    public float DepositedGrams => depositedMg / 1000f;
    public bool HasPowder => depositedMg > 0.001f;

    /// <summary>Kapasitas/target maksimum pan saat ini (mg). Dipakai visual krim untuk skala.</summary>
    public float MaxDepositMg => maxDepositMg;

    /// <summary>Switcher visual bubuk pada piring kiri (untuk di-suppress saat menampilkan krim).</summary>
    public PowderVisualLevelSwitcher DepositVisual => depositVisual;

    /// <summary>Jumlah (mg) yang dideposit per kontak sendok. Dipakai Salep untuk
    /// override target batch tanpa kehilangan per-scoop yang sudah dikonfigurasi.</summary>
    public float DepositStepMg => depositStepMg;

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
            ShowWarning("Taruh anak timbangan di piring kanan dulu ya, baru bisa menimbang.");
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

    /// <summary>Mesh granul bubuk dari plate (untuk dipakai ulang di visual mortar Salep).</summary>
    public Mesh GetDepositGranuleMesh()
    {
        return depositVisual != null ? depositVisual.GetRepresentativeGranuleMesh() : null;
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

    private static readonly Collider[] _rightPanOverlapBuffer = new Collider[32];

    private bool HasAcceptedTarget()
    {
        if (!allowPhysicalRightPanTarget)
            return true;

        // Peringatan "taruh anak timbangan dulu" HANYA boleh muncul kalau memang BELUM ada
        // anak timbangan sama sekali di piring kanan.
        //
        // PENTING: deteksi memakai OVERLAP FISIK LANGSUNG ke area piring kanan, BUKAN
        // WeightingZone.TrackedWeightCount maupun TotalGrams. Alasan:
        //  - TotalGrams di-gate perkamen (requireParchmentBeforeCounting) -> bisa 0 walau
        //    anak timbangan sudah ditaruh.
        //  - TrackedWeightCount bergantung callback trigger / reconcile yang TERNYATA flaky
        //    untuk anak timbangan dinamis yang tidur (sleeping rigidbody tidak memicu
        //    OnTriggerStay, dan kadang reconcile telat) -> peringatan salah muncul.
        // Physics.OverlapBox bersifat deterministik dan TIDAK terpengaruh status
        // kinematic/tidur, jadi anak timbangan yang benar-benar ada di piring SELALU terdeteksi.
        return HasPhysicalWeightOnRightPan();
    }

    private bool HasPhysicalWeightOnRightPan()
    {
        if (rightZone == null)
            return false;

        BoxCollider box = rightZone.GetComponent<BoxCollider>();
        if (box == null)
            return rightZone.TrackedWeightCount > 0; // fallback aman bila bukan BoxCollider

        Vector3 center = box.transform.TransformPoint(box.center);
        Vector3 lossy = box.transform.lossyScale;
        float margin = Mathf.Max(0f, rightPanWeightDetectionMargin);
        Vector3 half = new Vector3(
            Mathf.Abs(box.size.x * 0.5f * lossy.x) + margin,
            Mathf.Abs(box.size.y * 0.5f * lossy.y) + margin,
            Mathf.Abs(box.size.z * 0.5f * lossy.z) + margin);

        // QueryTriggerInteraction.Ignore -> hanya collider solid anak timbangan, abaikan
        // semua trigger (grab assist, snap zone, dll) supaya bersih & deterministik.
        int count = Physics.OverlapBoxNonAlloc(
            center, half, _rightPanOverlapBuffer, box.transform.rotation, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider c = _rightPanOverlapBuffer[i];
            if (c == null || c.isTrigger)
                continue;

            WeightItem weight = c.GetComponentInParent<WeightItem>();
            if (weight == null || !weight.isActiveAndEnabled)
                continue;

            // Perkamen bukan anak timbangan -> jangan dihitung sebagai target.
            if (weight.IsParchment)
                continue;

            return true;
        }

        return false;
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
        // Jika warningText tidak di-wire di scene, buat label peringatan world-space yang
        // ramah (panel amber + ikon "!") secara otomatis di atas piring kiri.
        if (warningText == null)
        {
            if (autoWarningLabel == null)
            {
                Collider col = GetComponent<Collider>();
                Vector3 pos = col != null ? col.bounds.center + Vector3.up * 0.18f
                                          : transform.position + Vector3.up * 0.18f;
                autoWarningLabel = WorldWarningLabel.Create(transform, pos);
            }
            autoWarningLabel.Show(message, warningDuration);
            warningTimer = warningDuration;
            return;
        }

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