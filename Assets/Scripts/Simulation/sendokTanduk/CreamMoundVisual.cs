using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class CreamMoundVisual : MonoBehaviour
{
    [SerializeField] private float radiusX = 0.035f;
    [SerializeField] private float radiusZ = 0.023f;
    [SerializeField] private float baseHeight = 0.004f;
    [SerializeField] private float moundHeight = 0.018f;
    [SerializeField] private float peakHeight = 0.018f;
    [SerializeField] private float foldAmount = 0.0025f;
    [SerializeField] private int radialSegments = 40;
    [SerializeField] private int rings = 7;
    [SerializeField] private Material creamMaterial;
    [SerializeField] private bool regenerateOnStart = true;

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
        float newPeakHeight,
        float newFoldAmount,
        Material newMaterial)
    {
        radiusX = Mathf.Max(0.001f, newRadiusX);
        radiusZ = Mathf.Max(0.001f, newRadiusZ);
        baseHeight = Mathf.Max(0.0001f, newBaseHeight);
        moundHeight = Mathf.Max(0.001f, newMoundHeight);
        peakHeight = Mathf.Max(0f, newPeakHeight);
        foldAmount = Mathf.Max(0f, newFoldAmount);
        creamMaterial = newMaterial;
        Rebuild();
    }

    public void Rebuild()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        Mesh oldMesh = meshFilter.sharedMesh;
        meshFilter.sharedMesh = BuildCreamMesh();
        if (oldMesh != null && oldMesh.name.StartsWith("Generated_CreamMound"))
        {
            if (Application.isPlaying)
                Destroy(oldMesh);
            else
                DestroyImmediate(oldMesh);
        }

        if (creamMaterial != null)
            meshRenderer.sharedMaterial = creamMaterial;

        Collider existingCollider = GetComponent<Collider>();
        if (existingCollider != null)
        {
            if (Application.isPlaying)
                Destroy(existingCollider);
            else
                DestroyImmediate(existingCollider);
        }
    }

    private Mesh BuildCreamMesh()
    {
        int segmentCount = Mathf.Clamp(radialSegments, 20, 72);
        int ringCount = Mathf.Clamp(rings, 3, 14);
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        int[] ringStarts = new int[ringCount + 1];
        for (int ring = 0; ring <= ringCount; ring++)
        {
            ringStarts[ring] = vertices.Count;
            float inward = (float)ring / ringCount;

            if (ring == ringCount)
            {
                vertices.Add(new Vector3(
                    radiusX * 0.08f,
                    baseHeight + moundHeight + peakHeight,
                    -radiusZ * 0.05f));
                uvs.Add(new Vector2(0.5f, 0.5f));
                continue;
            }

            float radiusFactor = 1f - inward;
            float smoothMound = Mathf.SmoothStep(0f, moundHeight, inward);
            float peak = peakHeight * Mathf.Pow(inward, 4f);

            for (int segment = 0; segment < segmentCount; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segmentCount;
                float broadFold = Mathf.Sin(angle * 3f + inward * 4.2f) * foldAmount;
                float fineFold = Mathf.Sin(angle * 5f - inward * 2.4f) * foldAmount * 0.35f;
                float x = Mathf.Cos(angle) * radiusX * radiusFactor;
                float z = Mathf.Sin(angle) * radiusZ * radiusFactor;
                float y = baseHeight + smoothMound + peak + broadFold + fineFold;

                vertices.Add(new Vector3(x, y, z));
                uvs.Add(new Vector2(
                    0.5f + Mathf.Cos(angle) * radiusFactor * 0.5f,
                    0.5f + Mathf.Sin(angle) * radiusFactor * 0.5f));
            }
        }

        for (int ring = 0; ring < ringCount; ring++)
        {
            int outer = ringStarts[ring];
            int inner = ringStarts[ring + 1];
            bool lastRing = ring == ringCount - 1;

            for (int segment = 0; segment < segmentCount; segment++)
            {
                int next = (segment + 1) % segmentCount;
                if (lastRing)
                {
                    triangles.Add(outer + segment);
                    triangles.Add(inner);
                    triangles.Add(outer + next);
                }
                else
                {
                    triangles.Add(outer + segment);
                    triangles.Add(inner + segment);
                    triangles.Add(inner + next);
                    triangles.Add(outer + segment);
                    triangles.Add(inner + next);
                    triangles.Add(outer + next);
                }
            }
        }

        int bottomCenter = vertices.Count;
        vertices.Add(Vector3.zero);
        uvs.Add(new Vector2(0.5f, 0.5f));
        int outerRing = ringStarts[0];
        for (int segment = 0; segment < segmentCount; segment++)
        {
            triangles.Add(bottomCenter);
            triangles.Add(outerRing + (segment + 1) % segmentCount);
            triangles.Add(outerRing + segment);
        }

        Mesh mesh = new Mesh { name = "Generated_CreamMound" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        return mesh;
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
