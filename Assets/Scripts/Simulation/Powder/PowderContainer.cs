using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Container that stores powder/solid material with visual feedback.
/// Attach to: Difenhidramin container, or any powder storage.
/// </summary>
public class PowderContainer : MonoBehaviour
{
    [Header("Material Info")]
    [SerializeField] private string materialName = "Difenhidramin";
    [SerializeField] private float maxAmountMg = 2000f;
    [SerializeField] private float currentAmountMg = 1000f;

    [Header("Access")]
    [Tooltip("Optional lid that must be open before powder can be taken.")]
    [SerializeField] private BottleLid requiredOpenLid;

    [Header("Powder Visual")]
    [Tooltip("Child Transform of the powder mesh inside the container.")]
    [SerializeField] private Transform powderMesh;
    [SerializeField] private bool createDefaultVisualIfMissing = true;
    [SerializeField] private Vector3 emptyLocalScale = new Vector3(1f, 0.001f, 1f);
    [SerializeField] private Vector3 fullLocalScale = new Vector3(1f, 0.6f, 1f);
    [SerializeField] private Vector3 emptyLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 fullLocalPosition = new Vector3(0f, 0.3f, 0f);
    [SerializeField] private Renderer powderRenderer;
    [SerializeField] private Color powderColor = Color.white;

    [Header("Events")]
    public UnityEvent<float> onAmountChanged;

    public string MaterialName => materialName;
    public float MaxAmountMg => maxAmountMg;
    public float CurrentAmountMg => currentAmountMg;
    public float FillRatio => maxAmountMg > 0f ? currentAmountMg / maxAmountMg : 0f;
    public bool IsAccessible => requiredOpenLid == null || requiredOpenLid.IsOpen || requiredOpenLid.transform.parent == null;
    public bool IsEmpty => currentAmountMg <= 0f;
    public bool IsFull => currentAmountMg >= maxAmountMg;

    private float initialAmountMg;
    private bool initialAmountCaptured;

    private void Awake()
    {
        CaptureInitialAmount();
    }

    private void Start()
    {
        if (powderMesh == null && createDefaultVisualIfMissing)
            CreateDefaultPowderVisual();

        if (powderRenderer == null && powderMesh != null)
            powderRenderer = powderMesh.GetComponent<Renderer>();

        ApplyPowderColor();

        UpdateVisual();
    }

    public void ConfigureMaterial(string newMaterialName, Color newPowderColor)
    {
        if (!string.IsNullOrWhiteSpace(newMaterialName))
            materialName = newMaterialName.Trim();

        powderColor = newPowderColor;
        ApplyPowderColor();
        UpdateVisual();
    }

    /// <summary>Pastikan jar punya stok cukup untuk total target bahan (mis. Vaselin 9.4 g).</summary>
    public void EnsureStock(float requiredMg, float headroomMultiplier = 1.25f)
    {
        float required = Mathf.Max(0f, requiredMg) * Mathf.Max(1f, headroomMultiplier);
        if (maxAmountMg < required)
            maxAmountMg = required;
        if (currentAmountMg < required)
            currentAmountMg = required;

        initialAmountMg = currentAmountMg;
        initialAmountCaptured = true;
        UpdateVisual();
        onAmountChanged?.Invoke(currentAmountMg);
    }

    public void ResetAmount()
    {
        CaptureInitialAmount();
        currentAmountMg = Mathf.Clamp(initialAmountMg, 0f, maxAmountMg);
        UpdateVisual();
        onAmountChanged?.Invoke(currentAmountMg);
    }

    /// <summary>Takes powder from this container. Returns actual amount taken in mg.</summary>
    public float TakePowder(float amountMg)
    {
        if (amountMg <= 0f || !IsAccessible)
            return 0f;

        float taken = Mathf.Min(amountMg, currentAmountMg);
        currentAmountMg -= taken;
        UpdateVisual();
        onAmountChanged?.Invoke(currentAmountMg);
        return taken;
    }

    /// <summary>Adds powder back into this container. Returns actual amount added in mg.</summary>
    public float AddPowder(float amountMg)
    {
        float available = maxAmountMg - currentAmountMg;
        float added = Mathf.Min(amountMg, available);
        currentAmountMg += added;
        UpdateVisual();
        onAmountChanged?.Invoke(currentAmountMg);
        return added;
    }

    private void UpdateVisual()
    {
        if (powderMesh == null) return;

        bool hasPowder = currentAmountMg > 0f;
        powderMesh.gameObject.SetActive(hasPowder);

        if (hasPowder)
        {
            float t = FillRatio;
            powderMesh.localScale = Vector3.Lerp(emptyLocalScale, fullLocalScale, t);
            powderMesh.localPosition = Vector3.Lerp(emptyLocalPosition, fullLocalPosition, t);
        }
    }

    private void CaptureInitialAmount()
    {
        if (initialAmountCaptured)
            return;

        initialAmountMg = currentAmountMg;
        initialAmountCaptured = true;
    }

    private void ApplyPowderColor()
    {
        if (powderRenderer == null && powderMesh != null)
            powderRenderer = powderMesh.GetComponentInChildren<Renderer>(true);

        if (powderRenderer == null)
            return;

        Material material = powderRenderer.material;
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", powderColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", powderColor);
    }

    [ContextMenu("Create Default Powder Visual")]
    private void CreateDefaultPowderVisual()
    {
        GameObject powderObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        powderObject.name = "PowderVisual";
        powderObject.transform.SetParent(transform, false);
        powderObject.transform.localPosition = fullLocalPosition;
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

        Material material = CreatePowderMaterial();
        if (powderRenderer != null && material != null)
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
            name = "Runtime_Powder_Material"
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", powderColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", powderColor);

        return material;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        currentAmountMg = Mathf.Clamp(currentAmountMg, 0f, maxAmountMg);
    }
#endif
}
