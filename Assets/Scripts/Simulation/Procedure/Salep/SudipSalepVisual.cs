using UnityEngine;

/// <summary>
/// Visual salep yang menempel di ujung Sudip.
/// Bentuknya patch/olesan di mata sudip, bukan blob kecil.
/// Ujung bawah dibuat rounded supaya tidak tajam.
/// </summary>
[DisallowMultipleComponent]
public sealed class SudipSalepVisual : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform sudipTip;

    [Header("Pose blob di SudipTip")]
    [SerializeField] private Vector3 blobLocalPosition = new Vector3(-0.000226f, -0.002427f, 0.0000966f);
    [SerializeField] private Vector3 blobLocalEuler = new Vector3(-0.135f, -0.282f, 0f);

    // Angka ini mengikuti pose visual yang kamu tunjukkan di gambar 2.
    // Jangan pakai inverse parent scale lagi.
    [SerializeField] private Vector3 fullBlobLocalScale = new Vector3(0.8226929f, 0.5069892f, 0.0930039f);
    [Header("Blob tebal")]
    [SerializeField] private float blobRadiusX = 0.0092f;
    [SerializeField] private float blobRadiusZ = 0.008f;
    [SerializeField] private float blobBaseHeight = 0.0022f;
    [SerializeField] private float blobMoundHeight = 0.012f;
    [SerializeField] private float blobNoise = 0.0006f;

    [Header("Bentuk patch salep")]
    [Tooltip("Lebar patch salep di permukaan sudip.")]
    [SerializeField] private float patchWidth = 0.018f;

    [Tooltip("Panjang patch salep ke arah ujung sudip.")]
    [SerializeField] private float patchLength = 0.014f;

    [Tooltip("Radius pembulatan ujung. Makin besar = ujung makin tumpul.")]
    [SerializeField] private float roundedTipRadius = 0.0038f;

    [Tooltip("Sedikit tonjolan supaya tidak terlalu flat seperti plane.")]
    [SerializeField] private float centerBulge = 0.0012f;

    [SerializeField] private int roundedTipSegments = 10;

    [Header("Material")]
    [SerializeField] private Material salepMaterialTemplate;
    [SerializeField] private Color salepColor = new Color(0.90f, 0.89f, 0.58f, 1f);

    private Transform blob;
    private MeshFilter blobMeshFilter;
    private MeshRenderer blobRenderer;
    private Material runtimeMaterial;
    private bool loaded;

    public bool IsLoaded => loaded;

    public void Configure(Transform tip)
    {
        if (tip != null)
            sudipTip = tip;

        EnsureBlob();
    }

    private void EnsureBlob()
    {
        if (blob != null)
            return;

        Transform parent = sudipTip != null ? sudipTip : transform;

        GameObject go = new GameObject("SudipSalepBlob");
        blobMeshFilter = go.AddComponent<MeshFilter>();
        blobRenderer = go.AddComponent<MeshRenderer>();

        blob = go.transform;
        blob.SetParent(parent, false);

        ApplyBlobPose();
        blob.localScale = fullBlobLocalScale;

        runtimeMaterial = CreateRuntimeMaterial();
        blobRenderer.sharedMaterial = runtimeMaterial;

        var mound = go.AddComponent<SpoonPowderMoundVisual>();
        mound.Configure(
            blobRadiusX,
            blobRadiusZ,
            blobBaseHeight,
            blobMoundHeight,
            blobNoise,
            runtimeMaterial
        );

        go.SetActive(false);
    }

    private void ApplyBlobPose()
    {
        if (blob == null)
            return;

        blob.localPosition = blobLocalPosition;
        blob.localRotation = Quaternion.Euler(blobLocalEuler);
    }

    private Material CreateRuntimeMaterial()
    {
        Material mat;

        if (salepMaterialTemplate != null)
        {
            mat = new Material(salepMaterialTemplate);
            mat.name = salepMaterialTemplate.name;
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            mat = new Material(shader) { name = "Runtime_SudipSalep" };
        }

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", salepColor);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", salepColor);

        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0f);

        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0f);

        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0f);

        // URP Lit biasanya punya _Cull. 0 = Off, supaya patch tetap kelihatan dari dua sisi.
        if (mat.HasProperty("_Cull"))
            mat.SetFloat("_Cull", 0f);

        return mat;
    }

    private Mesh BuildRoundedSudipPatchMesh()
    {
        float halfWidth = patchWidth * 0.5f;
        float length = Mathf.Max(0.001f, patchLength);
        float radius = Mathf.Clamp(roundedTipRadius, 0.0005f, halfWidth * 0.95f);
        int seg = Mathf.Clamp(roundedTipSegments, 4, 24);

        // Mesh dibuat di bidang local XY.
        // Bagian atas agak lebar, sisi turun ke ujung bawah yang rounded.
        // Ini menghasilkan bentuk seperti isi salep di mata sudip, bukan bola/gundukan kecil.
        int perimeterCount = 2 + (seg + 1);
        Vector3[] vertices = new Vector3[1 + perimeterCount];
        Vector2[] uvs = new Vector2[vertices.Length];

        // Center dibuat sedikit menonjol supaya salep tidak terlalu flat.
        vertices[0] = new Vector3(0f, -length * 0.42f, -centerBulge);
        uvs[0] = new Vector2(0.5f, 0.5f);

        int v = 1;

        // Top kiri dan top kanan.
        vertices[v++] = new Vector3(-halfWidth, 0f, 0f);
        vertices[v++] = new Vector3(halfWidth, 0f, 0f);

        // Ujung bawah rounded.
        // Arc dari kanan ke kiri, dengan titik bawah melengkung/tumpul.
        float tipCenterY = -length + radius;
        for (int i = 0; i <= seg; i++)
        {
            float t = i / (float)seg;
            float angle = Mathf.Lerp(0f, 180f, t) * Mathf.Deg2Rad;

            float x = Mathf.Cos(angle) * radius;
            float y = tipCenterY - Mathf.Sin(angle) * radius;

            vertices[v++] = new Vector3(x, y, 0f);
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            float ux = Mathf.InverseLerp(-halfWidth, halfWidth, vertices[i].x);
            float uy = Mathf.InverseLerp(-length, 0f, vertices[i].y);
            uvs[i] = new Vector2(ux, uy);
        }

        // Triangle fan, dibuat dua sisi supaya aman dari masalah backface culling.
        int edgeCount = perimeterCount;
        int[] triangles = new int[edgeCount * 6];

        int ti = 0;
        for (int i = 1; i <= edgeCount; i++)
        {
            int next = i == edgeCount ? 1 : i + 1;

            triangles[ti++] = 0;
            triangles[ti++] = i;
            triangles[ti++] = next;

            triangles[ti++] = 0;
            triangles[ti++] = next;
            triangles[ti++] = i;
        }

        Mesh mesh = new Mesh { name = "SudipRoundedSalepPatch" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    public void Load()
    {
        EnsureBlob();

        loaded = true;

        if (blob != null)
        {
            blob.gameObject.SetActive(true);
            ApplyBlobPose();
            blob.localScale = fullBlobLocalScale;
        }
    }

    /// <summary>
    /// Sisa salep di ujung sudip.
    /// 1 = penuh, 0 = habis.
    /// </summary>
    public void SetFill(float t)
    {
        EnsureBlob();

        if (blob == null)
            return;

        float k = Mathf.Clamp01(t);
        bool visible = loaded && k > 0.02f;

        blob.gameObject.SetActive(visible);

        if (!visible)
            return;

        ApplyBlobPose();

        // Menyusut tapi tidak jadi titik kecil banget.
        float shrink = Mathf.Lerp(0.35f, 1f, k);
        blob.localScale = new Vector3(
            fullBlobLocalScale.x * shrink,
            fullBlobLocalScale.y * shrink,
            fullBlobLocalScale.z
        );
    }

    public void Unload()
    {
        loaded = false;

        if (blob != null)
            blob.gameObject.SetActive(false);
    }
}