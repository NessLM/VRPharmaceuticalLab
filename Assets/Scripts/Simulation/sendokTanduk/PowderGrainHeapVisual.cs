using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual tumpukan bubuk yang benar-benar tampak granular: alih-alih satu mound mulus
/// (yang terlihat seperti pil), komponen ini menyebar BANYAK butiran kecil (kubus mini
/// dengan rotasi/ukuran acak) membentuk gundukan, lalu menggabungnya jadi satu mesh
/// (hemat performa, tetap satu draw). Hasilnya terbaca jelas sebagai serbuk/granul.
/// Dipakai SalepMortarVisual untuk isi mortar (Asam putih, Sulfur kuning, campuran).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class PowderGrainHeapVisual : MonoBehaviour
{
    private float radiusX = 0.4f;
    private float radiusZ = 0.36f;
    private float heapHeight = 0.12f;
    private float grainSize = 0.05f;
    private int grainCount = 160;
    private int seed = 1337;
    private Material material;

    /// <summary>Konfigurasi heap. Ukuran dalam unit lokal (mound mortar pakai skala kecil).</summary>
    public void Configure(
        float newRadiusX,
        float newRadiusZ,
        float newHeapHeight,
        float newGrainSize,
        int newGrainCount,
        int newSeed,
        Material newMaterial)
    {
        radiusX = Mathf.Max(0.01f, newRadiusX);
        radiusZ = Mathf.Max(0.01f, newRadiusZ);
        heapHeight = Mathf.Max(0.01f, newHeapHeight);
        grainSize = Mathf.Max(0.001f, newGrainSize);
        grainCount = Mathf.Clamp(newGrainCount, 8, 600);
        seed = newSeed;
        material = newMaterial;
        Rebuild();
    }

    public void Rebuild()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>();

        Mesh old = mf.sharedMesh;
        mf.sharedMesh = BuildHeapMesh();
        if (old != null && old.name.StartsWith("Generated_PowderGrainHeap"))
        {
            if (Application.isPlaying) Destroy(old);
            else DestroyImmediate(old);
        }

        if (material != null)
            mr.sharedMaterial = material;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
    }

    private System.Random rng;

    private float Rand(float min, float max) => (float)(rng.NextDouble() * (max - min) + min);

    private Mesh BuildHeapMesh()
    {
        rng = new System.Random(seed);

        var vertices = new List<Vector3>(grainCount * 12 + 256);
        var normals = new List<Vector3>(grainCount * 12 + 256);
        var triangles = new List<int>(grainCount * 60 + 1024);

        // 1) DASAR PADAT: kubah halus rendah agar tumpukan terbaca sebagai SATU gundukan
        // bubuk yang menyatu (bukan butiran melayang terpisah / "partikel"). Butiran lalu
        // ditabur di atasnya untuk tekstur granular.
        AddBaseDome(vertices, triangles);

        for (int g = 0; g < grainCount; g++)
        {
            // Distribusi radial bias ke tengah (lebih padat di puncak) untuk profil gundukan.
            float u = (float)rng.NextDouble();
            float r = Mathf.Sqrt(u);                 // 0..1
            float angle = Rand(0f, Mathf.PI * 2f);
            float px = Mathf.Cos(angle) * r * radiusX;
            float pz = Mathf.Sin(angle) * r * radiusZ;

            // Butiran DUDUK di permukaan kubah (profil sama dgn dome) lalu sedikit naik —
            // jadi menempel pada gundukan padat, bukan melayang terpisah seperti partikel.
            float domeY = (1f - r * r) * heapHeight;
            float py = domeY + Rand(-grainSize * 0.15f, grainSize * 0.5f);

            // Ukuran butiran bervariasi; cukup besar agar saling tumpang-tindih → menyatu.
            float s = grainSize * Rand(0.75f, 1.35f);

            Quaternion rot = Quaternion.Euler(Rand(0f, 360f), Rand(0f, 360f), Rand(0f, 360f));
            // Sedikit pipihkan acak agar butiran tidak bola sempurna (lebih natural).
            Vector3 grainScale = new Vector3(s, s * Rand(0.7f, 1f), s);
            AddGrain(vertices, triangles, new Vector3(px, py, pz), rot, grainScale);
        }

        Mesh mesh = new Mesh { name = "Generated_PowderGrainHeap" };
        mesh.indexFormat = vertices.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        // Smooth normals → butiran tampak BULAT (bukan kotak bersudut).
        mesh.RecalculateNormals();
        return mesh;
    }

    // Kubah dasar halus (setengah ellipsoid rendah). Memberi permukaan menerus sehingga
    // tumpukan terbaca sebagai gundukan bubuk padat; butiran di atasnya menambah tekstur.
    private void AddBaseDome(List<Vector3> verts, List<int> tris)
    {
        const int seg = 28;   // segmen radial
        const int rings = 6;  // cincin dari tepi ke puncak

        int[] ringStart = new int[rings + 1];
        for (int ring = 0; ring <= rings; ring++)
        {
            ringStart[ring] = verts.Count;
            float inward = (float)ring / rings;       // 0 tepi → 1 puncak
            if (ring == rings)
            {
                verts.Add(new Vector3(0f, heapHeight, 0f));
                continue;
            }
            float rf = 1f - inward;
            float y = heapHeight * Mathf.Sqrt(1f - rf * rf); // profil ellipsoid (kubah)
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                verts.Add(new Vector3(Mathf.Cos(a) * rf * radiusX, y, Mathf.Sin(a) * rf * radiusZ));
            }
        }
        for (int ring = 0; ring < rings; ring++)
        {
            int outer = ringStart[ring];
            int inner = ringStart[ring + 1];
            bool last = ring == rings - 1;
            for (int i = 0; i < seg; i++)
            {
                int next = (i + 1) % seg;
                if (last)
                {
                    tris.Add(outer + i); tris.Add(inner); tris.Add(outer + next);
                }
                else
                {
                    tris.Add(outer + i); tris.Add(inner + i); tris.Add(inner + next);
                    tris.Add(outer + i); tris.Add(inner + next); tris.Add(outer + next);
                }
            }
        }
    }

    // ===== Butiran BULAT (icosahedron) =====
    // 12 vertex, 20 segitiga. Dengan smooth normals tampak seperti butiran bulat kecil,
    // jauh lebih natural daripada kubus. Murah: 12 vert/butiran.
    private static readonly Vector3[] _icoVerts = BuildIcoVerts();
    private static readonly int[] _icoTris =
    {
        0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
        1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
        3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
        4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
    };

    private static Vector3[] BuildIcoVerts()
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        Vector3[] v =
        {
            new Vector3(-1,  t,  0), new Vector3( 1,  t,  0), new Vector3(-1, -t,  0), new Vector3( 1, -t,  0),
            new Vector3( 0, -1,  t), new Vector3( 0,  1,  t), new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
            new Vector3( t,  0, -1), new Vector3( t,  0,  1), new Vector3(-t,  0, -1), new Vector3(-t,  0,  1)
        };
        for (int i = 0; i < v.Length; i++)
            v[i] = v[i].normalized * 0.5f; // radius 0.5 (diameter = ukuran butiran)
        return v;
    }

    private void AddGrain(List<Vector3> verts, List<int> tris, Vector3 center, Quaternion rot, Vector3 scale)
    {
        int baseIndex = verts.Count;
        for (int i = 0; i < _icoVerts.Length; i++)
        {
            Vector3 p = _icoVerts[i];
            p = new Vector3(p.x * scale.x, p.y * scale.y, p.z * scale.z);
            verts.Add(center + rot * p);
        }
        for (int i = 0; i < _icoTris.Length; i++)
            tris.Add(baseIndex + _icoTris[i]);
    }
}