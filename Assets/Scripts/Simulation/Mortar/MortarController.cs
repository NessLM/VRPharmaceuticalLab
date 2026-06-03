using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Mortar that receives powder from HornSpoon and tracks grinding progress from StamperController.
/// When grinding progress reaches the threshold, material is marked as homogeneous.
/// Attach to: Mortar GameObject.
/// </summary>
public class MortarController : MonoBehaviour
{
    [Header("Capacity")]
    [SerializeField] private float maxCapacityMg = 3000f;
    [SerializeField] private float currentAmountMg = 0f;

    [Header("Grinding")]
    [SerializeField] private float grindingProgressRequired = 100f;
    [SerializeField] private float currentGrindingProgress = 0f;
    [SerializeField] private bool isHomogeneous = false;

    [Header("Powder Visual")]
    [Tooltip("Child Transform of the powder mesh inside the mortar bowl.")]
    [SerializeField] private Transform powderMesh;
    [SerializeField] private bool createDefaultVisualIfMissing = true;
    [SerializeField] private Vector3 emptyLocalScale = new Vector3(0.8f, 0.001f, 0.8f);
    [SerializeField] private Vector3 fullLocalScale = new Vector3(0.8f, 0.35f, 0.8f);
    [SerializeField] private Vector3 emptyLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 fullLocalPosition = new Vector3(0f, 0.175f, 0f);
    [SerializeField] private Renderer powderRenderer;
    [SerializeField] private Material powderMaterial;
    [SerializeField] private Color rawColor = Color.white;
    [SerializeField] private Color homogeneousColor = new Color(0.88f, 0.88f, 0.78f);

    [Header("Events")]
    public UnityEvent<float> onAmountChanged;
    public UnityEvent<float> onGrindingProgressChanged;
    public UnityEvent onBecameHomogeneous;

    public float MaxCapacityMg => maxCapacityMg;
    public float CurrentAmountMg => currentAmountMg;
    public float FillRatio => maxCapacityMg > 0f ? currentAmountMg / maxCapacityMg : 0f;
    public float GrindingProgressRatio => grindingProgressRequired > 0f ? currentGrindingProgress / grindingProgressRequired : 0f;
    public bool IsHomogeneous => isHomogeneous;
    public bool IsEmpty => currentAmountMg <= 0f;
    public bool IsFull => currentAmountMg >= maxCapacityMg;

    private void Start()
    {
        if (powderMesh == null && createDefaultVisualIfMissing)
            CreateDefaultPowderVisual();

        if (powderRenderer == null && powderMesh != null)
            powderRenderer = powderMesh.GetComponent<Renderer>();

        if (powderRenderer != null && powderMaterial != null)
            powderRenderer.sharedMaterial = powderMaterial;

        UpdateVisual();
    }

    /// <summary>Adds powder to the mortar. Returns actual amount accepted in mg.</summary>
    public float AddPowder(float amountMg)
    {
        float available = maxCapacityMg - currentAmountMg;
        float added = Mathf.Min(amountMg, available);
        currentAmountMg += added;
        UpdateVisual();
        onAmountChanged?.Invoke(currentAmountMg);
        return added;
    }

    /// <summary>Adds grinding progress. Called by StamperController each frame it detects movement.</summary>
    public void AddGrindingProgress(float amount)
    {
        if (isHomogeneous || IsEmpty) return;

        float prev = currentGrindingProgress;
        currentGrindingProgress = Mathf.Min(currentGrindingProgress + amount, grindingProgressRequired);

        if (!Mathf.Approximately(currentGrindingProgress, prev))
        {
            onGrindingProgressChanged?.Invoke(GrindingProgressRatio);

            if (currentGrindingProgress >= grindingProgressRequired)
            {
                isHomogeneous = true;
                onBecameHomogeneous?.Invoke();
                UpdateVisual();
            }
        }
    }

    /// <summary>Resets mortar contents and grinding progress.</summary>
    public void ResetMortar()
    {
        currentAmountMg = 0f;
        currentGrindingProgress = 0f;
        isHomogeneous = false;
        UpdateVisual();
        onAmountChanged?.Invoke(currentAmountMg);
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

            if (powderRenderer != null)
                powderRenderer.material.color = isHomogeneous ? homogeneousColor : rawColor;
        }
    }

    [ContextMenu("Create Default Powder Visual")]
    private void CreateDefaultPowderVisual()
    {
        GameObject powderObject = new GameObject("MortarPowderVisual");
        powderObject.transform.SetParent(transform, false);
        powderObject.transform.localPosition = fullLocalPosition;
        powderObject.transform.localRotation = Quaternion.identity;
        powderObject.transform.localScale = Vector3.one;

        MeshFilter meshFilter = powderObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = powderObject.AddComponent<MeshRenderer>();

        // Use a mound-shaped mesh instead of a primitive cylinder so it looks like actual powder
        meshFilter.sharedMesh = BuildPowderMoundMesh();

        powderMesh = powderObject.transform;
        powderRenderer = meshRenderer;

        if (powderMaterial != null)
        {
            powderRenderer.sharedMaterial = powderMaterial;
        }
        else
        {
            Material material = CreatePowderMaterial(rawColor, "Runtime_Mortar_Powder_Material");
            if (material != null)
                powderRenderer.sharedMaterial = material;
        }
    }

    /// <summary>Generates a dome-shaped powder mound mesh in unit coordinates (0–1 range),
    /// so it scales correctly via localScale just like Unity's built-in primitives.</summary>
    private Mesh BuildPowderMoundMesh()
    {
        // Defined in local unit coordinates (radius ~0.45, height 0–1)
        // Mortar's localScale of (0.0026, 0.00087, 0.0026) + mortar world scale ~45.82
        // gives a ~11cm wide, ~4cm tall mound at fullLocalScale.
        const float moundRadiusX = 0.45f;
        const float moundRadiusZ = 0.45f;
        const float baseThickness = 0.04f;
        const float moundPeakY = 0.90f;    // dome peak in unit Y (scaled by localScale.y)
        const float noiseAmount = 0.012f;
        const int segments = 32;
        const int rings = 6;

        var vertices = new System.Collections.Generic.List<Vector3>();
        var normals = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var triangles = new System.Collections.Generic.List<int>();

        // Bottom flat cap
        int bottomCenter = vertices.Count;
        vertices.Add(Vector3.zero);
        normals.Add(Vector3.down);
        uvs.Add(new Vector2(0.5f, 0.5f));

        int bottomRingStart = vertices.Count;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertices.Add(new Vector3(Mathf.Cos(angle) * moundRadiusX, 0f, Mathf.Sin(angle) * moundRadiusZ));
            normals.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
        }
        for (int i = 0; i < segments; i++)
        {
            triangles.Add(bottomCenter);
            triangles.Add(bottomRingStart + i);
            triangles.Add(bottomRingStart + (i + 1) % segments);
        }

        // Thin side wall
        int sideStart = vertices.Count;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float x = Mathf.Cos(angle) * moundRadiusX;
            float z = Mathf.Sin(angle) * moundRadiusZ;
            Vector3 normal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;

            vertices.Add(new Vector3(x, 0f, z));
            normals.Add(normal);
            uvs.Add(new Vector2((float)i / segments, 0f));

            vertices.Add(new Vector3(x, baseThickness, z));
            normals.Add(normal);
            uvs.Add(new Vector2((float)i / segments, 1f));
        }
        for (int i = 0; i < segments; i++)
        {
            int bl = sideStart + i * 2;
            int tl = bl + 1;
            int br = sideStart + (i + 1) * 2;
            int tr = br + 1;
            triangles.Add(bl); triangles.Add(tl); triangles.Add(tr);
            triangles.Add(bl); triangles.Add(tr); triangles.Add(br);
        }

        // Dome top — concentric rings collapsing to peak
        int[] ringStarts = new int[rings + 1];
        for (int ring = 0; ring <= rings; ring++)
        {
            ringStarts[ring] = vertices.Count;
            float inward = (float)ring / rings;
            float ringRadiusX = moundRadiusX * (1f - inward);
            float ringRadiusZ = moundRadiusZ * (1f - inward);
            float y = baseThickness + moundPeakY * (inward * inward);

            if (ring == rings)
            {
                vertices.Add(new Vector3(0f, baseThickness + moundPeakY, 0f));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(0.5f, 0.5f));
                continue;
            }

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float wave = Mathf.Sin(angle * 3.2f + ring * 0.8f) * noiseAmount;
                vertices.Add(new Vector3(
                    Mathf.Cos(angle) * ringRadiusX,
                    y + wave,
                    Mathf.Sin(angle) * ringRadiusZ));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(
                    0.5f + Mathf.Cos(angle) * 0.5f,
                    0.5f + Mathf.Sin(angle) * 0.5f));
            }
        }

        for (int ring = 0; ring < rings; ring++)
        {
            int outer = ringStarts[ring];
            int inner = ringStarts[ring + 1];
            bool last = ring == rings - 1;
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                if (last)
                {
                    triangles.Add(outer + i);
                    triangles.Add(inner);
                    triangles.Add(outer + next);
                }
                else
                {
                    triangles.Add(outer + i); triangles.Add(inner + i); triangles.Add(inner + next);
                    triangles.Add(outer + i); triangles.Add(inner + next); triangles.Add(outer + next);
                }
            }
        }

        Mesh mesh = new Mesh { name = "MortarPowderMound_Generated" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private Material CreatePowderMaterial(Color color, string materialName)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = materialName
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        return material;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        currentAmountMg = Mathf.Clamp(currentAmountMg, 0f, maxCapacityMg);
        currentGrindingProgress = Mathf.Clamp(currentGrindingProgress, 0f, grindingProgressRequired);
    }
#endif
}
