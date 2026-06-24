using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class SpoonPowderMoundVisual : MonoBehaviour
{
    [SerializeField] private float radiusX = 0.035f;
    [SerializeField] private float radiusZ = 0.022f;
    [SerializeField] private float baseHeight = 0.004f;
    [SerializeField] private float moundHeight = 0.012f;
    [SerializeField] private float noiseAmount = 0.0015f;
    [SerializeField] private int radialSegments = 32;
    [SerializeField] private int rings = 5;
    [SerializeField] private Material powderMaterial;
    [SerializeField] private bool regenerateOnStart = true;

    [Header("Granular (butiran bubuk)")]
    [Tooltip("Jika ON, permukaan diberi gundukan acak frekuensi tinggi agar tampak " +
             "seperti tumpukan bubuk butiran, bukan kubah halus.")]
    [SerializeField] private bool granular;
    [Tooltip("Tinggi butiran sebagai fraksi moundHeight (mis. 0.35 = bergerigi jelas).")]
    [SerializeField] private float grainAmount = 0.3f;
    [SerializeField] private int grainSeed = 1337;

    private void Start()
    {
        if (regenerateOnStart)
            Rebuild();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            Rebuild();
    }

    public void Configure(
        float newRadiusX,
        float newRadiusZ,
        float newBaseHeight,
        float newMoundHeight,
        float newNoiseAmount,
        Material newMaterial)
    {
        radiusX = Mathf.Max(0.001f, newRadiusX);
        radiusZ = Mathf.Max(0.001f, newRadiusZ);
        baseHeight = Mathf.Max(0.0001f, newBaseHeight);
        moundHeight = Mathf.Max(0.001f, newMoundHeight);
        noiseAmount = Mathf.Max(0f, newNoiseAmount);
        powderMaterial = newMaterial;
        Rebuild();
    }

    /// <summary>Konfigurasi dengan opsi butiran (granular) untuk tampilan bubuk tumpukan.</summary>
    public void Configure(
        float newRadiusX,
        float newRadiusZ,
        float newBaseHeight,
        float newMoundHeight,
        float newNoiseAmount,
        Material newMaterial,
        bool newGranular,
        float newGrainAmount,
        int newGrainSeed)
    {
        granular = newGranular;
        grainAmount = Mathf.Clamp01(newGrainAmount);
        grainSeed = newGrainSeed;
        if (granular)
        {
            // Lebih banyak segmen/cincin agar butiran terbaca jelas.
            radialSegments = Mathf.Max(radialSegments, 44);
            rings = Mathf.Max(rings, 7);
        }
        Configure(newRadiusX, newRadiusZ, newBaseHeight, newMoundHeight, newNoiseAmount, newMaterial);
    }

    // Pseudo-random hash [-1,1] stabil per (i, ring) untuk gundukan butiran.
    private float GrainHash(int i, int ring)
    {
        unchecked
        {
            int h = grainSeed;
            h = h * 73856093 ^ i * 19349663 ^ ring * 83492791;
            float v = ((h & 0x7fffffff) % 10000) / 10000f; // 0..1
            return v * 2f - 1f;
        }
    }

    public void Rebuild()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        Mesh oldMesh = meshFilter.sharedMesh;
        meshFilter.sharedMesh = BuildMoundMesh();
        if (oldMesh != null && oldMesh.name.StartsWith("Generated_SpoonPowderMound"))
        {
            if (Application.isPlaying)
                Destroy(oldMesh);
            else
                DestroyImmediate(oldMesh);
        }
        if (powderMaterial != null)
            meshRenderer.sharedMaterial = powderMaterial;

        Collider existingCollider = GetComponent<Collider>();
        if (existingCollider != null)
        {
            if (Application.isPlaying)
                Destroy(existingCollider);
            else
                DestroyImmediate(existingCollider);
        }
    }

    private Mesh BuildMoundMesh()
    {
        int segments = Mathf.Clamp(radialSegments, 16, 64);
        int ringCount = Mathf.Clamp(rings, 2, 12);
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        AddBottom(vertices, normals, uvs, triangles, segments);
        AddSide(vertices, normals, uvs, triangles, segments);
        AddTop(vertices, normals, uvs, triangles, segments, ringCount);

        Mesh mesh = new Mesh
        {
            name = "Generated_SpoonPowderMound"
        };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private void AddBottom(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, int segments)
    {
        int center = vertices.Count;
        vertices.Add(Vector3.zero);
        normals.Add(Vector3.down);
        uvs.Add(new Vector2(0.5f, 0.5f));

        int ringStart = vertices.Count;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radiusX, 0f, Mathf.Sin(angle) * radiusZ));
            normals.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
        }

        for (int i = 0; i < segments; i++)
        {
            triangles.Add(center);
            triangles.Add(ringStart + i);
            triangles.Add(ringStart + (i + 1) % segments);
        }
    }

    private void AddSide(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, int segments)
    {
        int start = vertices.Count;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float x = Mathf.Cos(angle) * radiusX;
            float z = Mathf.Sin(angle) * radiusZ;
            Vector3 normal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;

            vertices.Add(new Vector3(x, 0f, z));
            normals.Add(normal);
            uvs.Add(new Vector2((float)i / segments, 0f));

            vertices.Add(new Vector3(x, baseHeight, z));
            normals.Add(normal);
            uvs.Add(new Vector2((float)i / segments, 1f));
        }

        for (int i = 0; i < segments; i++)
        {
            int bottomLeft = start + i * 2;
            int topLeft = bottomLeft + 1;
            int bottomRight = start + (i + 1) * 2;
            int topRight = bottomRight + 1;

            triangles.Add(bottomLeft);
            triangles.Add(topLeft);
            triangles.Add(topRight);
            triangles.Add(bottomLeft);
            triangles.Add(topRight);
            triangles.Add(bottomRight);
        }
    }

    private void AddTop(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, int segments, int ringCount)
    {
        int[] ringStarts = new int[ringCount + 1];
        for (int ring = 0; ring <= ringCount; ring++)
        {
            ringStarts[ring] = vertices.Count;
            float inward = (float)ring / ringCount;
            float ringRadiusX = radiusX * (1f - inward);
            float ringRadiusZ = radiusZ * (1f - inward);

            // Tinggi linier (kerucut) + sedikit runcing di puncak (sudut runtuh/repose serbuk alami)
            // agar terlihat seperti tumpukan bubuk nyata, bukan pil kubah bulat.
            float y = baseHeight + moundHeight * Mathf.Pow(inward, 0.88f);

            if (ring == ringCount)
            {
                vertices.Add(new Vector3(0f, baseHeight + moundHeight, 0f));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(0.5f, 0.5f));
                continue;
            }

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;

                // Lipatan lereng alami (slip/collapse folds) pada tumpukan serbuk halus
                float slopeWave = Mathf.Sin(angle * 4.5f + ring * 0.5f) * noiseAmount * 0.8f * (1f - inward);
                float wave = Mathf.Sin(angle * 3.2f + ring * 0.8f) * noiseAmount + slopeWave;
                float jx = 0f, jz = 0f;

                if (granular)
                {
                    // Gundukan butiran acak; lebih kuat di tepi, menipis di puncak agar
                    // tetap berbentuk tumpukan namun bertekstur seperti bubuk.
                    float edgeFade = 1f - inward * 0.55f;
                    wave += GrainHash(i, ring) * moundHeight * grainAmount * edgeFade;
                    // Jitter horizontal kecil agar siluet tidak rapi seperti pil/elips,
                    // melainkan bergerigi seperti tumpukan serbuk.
                    float hj = Mathf.Min(ringRadiusX, ringRadiusZ) * 0.14f * grainAmount;
                    jx = GrainHash(i + 991, ring) * hj;
                    jz = GrainHash(i + 1733, ring) * hj;
                }

                vertices.Add(new Vector3(Mathf.Cos(angle) * ringRadiusX + jx, y + wave, Mathf.Sin(angle) * ringRadiusZ + jz));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
            }
        }

        for (int ring = 0; ring < ringCount; ring++)
        {
            int outer = ringStarts[ring];
            int inner = ringStarts[ring + 1];
            bool last = ring == ringCount - 1;

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
                    triangles.Add(outer + i);
                    triangles.Add(inner + i);
                    triangles.Add(inner + next);
                    triangles.Add(outer + i);
                    triangles.Add(inner + next);
                    triangles.Add(outer + next);
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && gameObject != null)
                Rebuild();
        };
    }
#endif
}
