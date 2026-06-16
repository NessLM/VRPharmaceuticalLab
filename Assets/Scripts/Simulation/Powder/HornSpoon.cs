using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Horn spoon (Sendok Tanduk) that scoops powder from a PowderContainer
/// and pours it into a MortarController via tip proximity detection.
/// Priority: pour into mortar > scoop from container.
/// Attach to: Sendok Tanduk GameObject.
/// Set tipTransform to a child empty at the scoop tip.
/// </summary>
public class HornSpoon : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private bool requireHeldToTransfer = true;
    [SerializeField] private bool allowDirectPowderContainerScoop;

    [Header("Capacity")]
    [SerializeField] private float maxCapacityMg = 200f;
    [SerializeField] private float currentAmountMg = 0f;
    [SerializeField] private float scoopRateMgPerSecond = 80f;
    [SerializeField] private float pourRateMgPerSecond = 120f;

    [Header("Tip Detection")]
    [Tooltip("Empty child Transform at the tip of the spoon head.")]
    [SerializeField] private Transform tipTransform;
    [SerializeField] private float detectionRadius = 0.06f;
    [SerializeField] private LayerMask detectionLayerMask = ~0;

    [Header("Powder Visual on Spoon")]
    [Tooltip("Child Transform of the powder mesh sitting on the spoon.")]
    [SerializeField] private Transform powderMesh;
    [SerializeField] private bool createDefaultVisualIfMissing = true;
    [SerializeField] private Vector3 emptyLocalScale = new Vector3(0.8f, 0.001f, 0.8f);
    [SerializeField] private Vector3 fullLocalScale = new Vector3(0.8f, 0.25f, 0.8f);
    [SerializeField] private Vector3 emptyLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 fullLocalPosition = new Vector3(0f, 0.125f, 0f);
    [SerializeField] private bool preserveAuthoredPowderVisualTransform = true;
    [SerializeField] private Renderer powderRenderer;
    [SerializeField] private Material powderMaterial;
    [SerializeField] private Color powderColor = new Color(0.93f, 0.93f, 0.93f, 1f);

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    [Header("Events")]
    public UnityEvent<float> onAmountChanged;
    public UnityEvent onSpoonFull;
    public UnityEvent onSpoonEmpty;

    public float MaxCapacityMg => maxCapacityMg;
    public float CurrentAmountMg => currentAmountMg;
    public float FillRatio => maxCapacityMg > 0f ? currentAmountMg / maxCapacityMg : 0f;
    public bool IsEmpty => currentAmountMg <= 0f;
    public bool IsFull => currentAmountMg >= maxCapacityMg;
    public bool CanReceivePowder => !IsFull;
    public Transform TipTransform => tipTransform != null ? tipTransform : transform;

    private XRGrabInteractable grabInteractable;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (tipTransform == null)
            tipTransform = transform;
    }

    private void Start()
    {
        if (powderMesh == null && createDefaultVisualIfMissing)
            CreateDefaultPowderVisual();

        if (powderRenderer == null && powderMesh != null)
            powderRenderer = powderMesh.GetComponent<Renderer>();

        if (powderRenderer != null)
            ApplyPowderMaterial();

        UpdateVisual();
    }

    private void Update()
    {
        if (tipTransform == null || !CanTransfer())
            return;

        Collider[] hits = Physics.OverlapSphere(tipTransform.position, detectionRadius, detectionLayerMask);

        PowderContainer nearestPowder = null;
        MortarController nearestMortar = null;

        foreach (var hit in hits)
        {
            if (nearestPowder == null)
                nearestPowder = hit.GetComponentInParent<PowderContainer>();
            if (nearestMortar == null)
                nearestMortar = hit.GetComponentInParent<MortarController>();
            if (nearestPowder != null && nearestMortar != null)
                break;
        }

        // Priority: pour into mortar first if spoon has content
        if (nearestMortar != null && !IsEmpty && !nearestMortar.IsFull)
        {
            float amount = pourRateMgPerSecond * Time.deltaTime;
            float toGive = Mathf.Min(amount, currentAmountMg);
            float accepted = nearestMortar.AddPowder(toGive);
            float prev = currentAmountMg;
            currentAmountMg = Mathf.Max(currentAmountMg - accepted, 0f);

            if (!Mathf.Approximately(currentAmountMg, prev))
            {
                UpdateVisual();
                onAmountChanged?.Invoke(currentAmountMg);
                if (IsEmpty) onSpoonEmpty?.Invoke();
            }
        }
        // Secondary legacy direct scoop. Phase 2 uses XR Activate via ScoopBottleTarget by default.
        else if (allowDirectPowderContainerScoop && nearestPowder != null && !IsFull && !nearestPowder.IsEmpty)
        {
            float availableSpace = Mathf.Max(0f, maxCapacityMg - currentAmountMg);
            float amount = Mathf.Min(scoopRateMgPerSecond * Time.deltaTime, availableSpace);
            float taken = nearestPowder.TakePowder(amount);
            float prev = currentAmountMg;
            currentAmountMg = Mathf.Min(currentAmountMg + taken, maxCapacityMg);

            if (!Mathf.Approximately(currentAmountMg, prev))
            {
                UpdateVisual();
                onAmountChanged?.Invoke(currentAmountMg);
                if (IsFull) onSpoonFull?.Invoke();

                if (debugLogs)
                    Debug.Log($"{name} scooped {taken:0.###} mg from {nearestPowder.name}", this);
            }
        }
    }

    private bool CanTransfer()
    {
        if (!requireHeldToTransfer)
            return true;

        return grabInteractable != null && grabInteractable.isSelected;
    }

    private void UpdateVisual()
    {
        if (powderMesh == null) return;

        bool hasPowder = currentAmountMg > 0f;
        powderMesh.gameObject.SetActive(hasPowder);

        if (hasPowder && !preserveAuthoredPowderVisualTransform)
        {
            float t = FillRatio;
            powderMesh.localScale = Vector3.Lerp(emptyLocalScale, fullLocalScale, t);
            powderMesh.localPosition = Vector3.Lerp(emptyLocalPosition, fullLocalPosition, t);
        }
    }

    private void CreateDefaultPowderVisual()
    {
        GameObject powderObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        powderObject.name = "PowderOnSpoonVisual";
        powderObject.transform.SetParent(tipTransform != null ? tipTransform : transform, false);
        powderObject.transform.localPosition = Vector3.zero;
        powderObject.transform.localRotation = Quaternion.identity;
        powderObject.transform.localScale = fullLocalScale;

        Collider powderCollider = powderObject.GetComponent<Collider>();
        if (powderCollider != null)
        {
            if (Application.isPlaying)
                Destroy(powderCollider);
            else
                DestroyImmediate(powderCollider);
        }

        powderMesh = powderObject.transform;
        powderRenderer = powderObject.GetComponent<Renderer>();
        ApplyPowderMaterial();
    }

    public float AddPowder(float amountMg)
    {
        if (amountMg <= 0f || IsFull)
            return 0f;

        float accepted = Mathf.Min(amountMg, maxCapacityMg - currentAmountMg);
        if (accepted <= 0f)
            return 0f;

        currentAmountMg = Mathf.Min(currentAmountMg + accepted, maxCapacityMg);
        UpdateVisual();
        onAmountChanged?.Invoke(currentAmountMg);

        if (IsFull)
            onSpoonFull?.Invoke();

        return accepted;
    }

    /// <summary>
    /// Removes powder from the spoon. Used by external deposit zones (e.g. PowderDepositZone on the balance pan).
    /// Returns the actual milligrams removed (may be less than requested if spoon is nearly empty).
    /// </summary>
    public float RemovePowder(float amountMg)
    {
        if (amountMg <= 0f || IsEmpty) return 0f;

        float removed = Mathf.Min(amountMg, currentAmountMg);
        currentAmountMg = Mathf.Max(0f, currentAmountMg - removed);
        UpdateVisual();
        onAmountChanged?.Invoke(currentAmountMg);

        if (IsEmpty) onSpoonEmpty?.Invoke();

        return removed;
    }

    public void AddPowder()
    {
        AddPowder(maxCapacityMg);
    }

    private void ApplyPowderMaterial()
    {
        if (powderRenderer == null)
            return;

        if (powderMaterial != null)
        {
            powderRenderer.sharedMaterial = powderMaterial;
            return;
        }

        Material material = CreatePowderMaterial();
        if (material != null)
            powderRenderer.sharedMaterial = material;
    }

    private Material CreatePowderMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = "Runtime_Spoon_Powder_Material"
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", powderColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", powderColor);

        return material;
    }

    public void ClearPowder()
    {
        if (currentAmountMg <= 0f)
            return;

        currentAmountMg = 0f;
        UpdateVisual();
        onAmountChanged?.Invoke(currentAmountMg);
        onSpoonEmpty?.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        if (tipTransform == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(tipTransform.position, detectionRadius);
    }
}
