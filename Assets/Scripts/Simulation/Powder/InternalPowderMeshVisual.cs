using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates one contained powder mesh inside the Difenhidramin bottle.
/// No particles, no grain GameObjects, no physics, and no pour effect.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class InternalPowderMeshVisual : MonoBehaviour
{
    [Header("Fit Reference")]
    [SerializeField] private Transform cylinderReference;
    [SerializeField] private float radiusMultiplier = 0.94f;
    [SerializeField] private float heightMultiplier = 0.62f;
    [SerializeField] private float bottomOffset = 0.02f;
    [SerializeField] private float moundHeight = 0.04f;
    [SerializeField] private float topNoiseAmount = 0.004f;
    [SerializeField] private int radialSegments = 64;
    [SerializeField] private int topRings = 12;
    [SerializeField] private bool addSurfaceGrains = true;
    [SerializeField] private int surfaceGrainCount = 64;
    [SerializeField] private Vector2 surfaceGrainRadiusRange = new Vector2(0.0022f, 0.006f);
    [SerializeField] private float surfaceGrainHeight = 0.0035f;
    [SerializeField] private int surfaceGrainSeed = 731;
    [SerializeField] private Material powderMaterial;
    [SerializeField] private bool regenerateOnValidate = true;

    [Header("Manual Fallback")]
    [SerializeField] private bool useManualFit;
    [SerializeField] private float manualRadius = 0.093f;
    [SerializeField] private float manualHeight = 0.18f;
    [SerializeField] private float manualBaseY = -0.052f;

    private const float MinimumRadius = 0.002f;
    private const float MinimumHeight = 0.004f;

    private void Awake()
    {
        Rebuild();
    }

    private void OnEnable()
    {
        Rebuild();
    }

    public void GenerateMesh()
    {
        Rebuild();
    }

    private void Rebuild()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

        meshFilter.sharedMesh = BuildPowderMesh();

        if (powderMaterial != null)
            meshRenderer.sharedMaterial = powderMaterial;
    }

    private Mesh BuildPowderMesh()
    {
        PowderFit fit = ResolveFit();
        int segments = Mathf.Clamp(radialSegments, 24, 128);
        int rings = Mathf.Clamp(topRings, 3, 16);

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        AddBottomCap(fit, segments, vertices, normals, uvs, triangles);
        AddSideWall(fit, segments, vertices, normals, uvs, triangles);
        AddNoisyTop(fit, segments, rings, vertices, normals, uvs, triangles);
        AddMergedSurfaceGrains(fit, segments, vertices, normals, uvs, triangles);

        Mesh mesh = new Mesh
        {
            name = "InternalPowderMesh_FittedToCylinder"
        };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private PowderFit ResolveFit()
    {
        if (useManualFit)
        {
            return new PowderFit(
                Mathf.Max(MinimumRadius, manualRadius),
                Mathf.Max(MinimumHeight, manualHeight),
                manualBaseY,
                Vector3.zero);
        }

        Transform reference = ResolveCylinderReference();
        if (!TryGetBounds(reference, out Bounds bounds))
        {
            return new PowderFit(
                Mathf.Max(MinimumRadius, manualRadius),
                Mathf.Max(MinimumHeight, manualHeight),
                manualBaseY,
                Vector3.zero);
        }

        Bounds localBounds = WorldBoundsToLocal(bounds);
        float radius = Mathf.Max(MinimumRadius, Mathf.Min(localBounds.extents.x, localBounds.extents.z) * radiusMultiplier);
        float height = Mathf.Max(MinimumHeight, localBounds.size.y * heightMultiplier);
        Vector3 localBaseCenter = new Vector3(localBounds.center.x, localBounds.min.y + bottomOffset, localBounds.center.z);

        return new PowderFit(radius, height, localBaseCenter.y, localBaseCenter);
    }

    private Bounds WorldBoundsToLocal(Bounds worldBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;

        var localBounds = new Bounds(transform.InverseTransformPoint(new Vector3(min.x, min.y, min.z)), Vector3.zero);
        localBounds.Encapsulate(transform.InverseTransformPoint(new Vector3(min.x, min.y, max.z)));
        localBounds.Encapsulate(transform.InverseTransformPoint(new Vector3(min.x, max.y, min.z)));
        localBounds.Encapsulate(transform.InverseTransformPoint(new Vector3(min.x, max.y, max.z)));
        localBounds.Encapsulate(transform.InverseTransformPoint(new Vector3(max.x, min.y, min.z)));
        localBounds.Encapsulate(transform.InverseTransformPoint(new Vector3(max.x, min.y, max.z)));
        localBounds.Encapsulate(transform.InverseTransformPoint(new Vector3(max.x, max.y, min.z)));
        localBounds.Encapsulate(transform.InverseTransformPoint(new Vector3(max.x, max.y, max.z)));

        return localBounds;
    }

    private Transform ResolveCylinderReference()
    {
        if (cylinderReference != null)
            return cylinderReference;

        Transform powderRoot = transform.parent;
        Transform bottleRoot = powderRoot != null ? powderRoot.parent : null;
        if (bottleRoot == null)
            return transform;

        Transform cylinder = bottleRoot.Find("Cylinder");
        return cylinder != null ? cylinder : bottleRoot;
    }

    private static bool TryGetBounds(Transform reference, out Bounds bounds)
    {
        bounds = default;
        if (reference == null)
            return false;

        Renderer renderer = reference.GetComponent<Renderer>();
        if (renderer != null)
        {
            bounds = renderer.bounds;
            return true;
        }

        Collider collider = reference.GetComponent<Collider>();
        if (collider != null)
        {
            bounds = collider.bounds;
            return true;
        }

        Renderer childRenderer = reference.GetComponentInChildren<Renderer>();
        if (childRenderer != null)
        {
            bounds = childRenderer.bounds;
            return true;
        }

        Collider childCollider = reference.GetComponentInChildren<Collider>();
        if (childCollider != null)
        {
            bounds = childCollider.bounds;
            return true;
        }

        return false;
    }

    private void AddBottomCap(
        PowderFit fit,
        int segments,
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<int> triangles)
    {
        int center = vertices.Count;
        vertices.Add(fit.LocalBaseCenter);
        normals.Add(Vector3.down);
        uvs.Add(new Vector2(0.5f, 0.5f));

        int ringStart = vertices.Count;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 local = fit.LocalBaseCenter + new Vector3(Mathf.Cos(angle) * fit.Radius, 0f, Mathf.Sin(angle) * fit.Radius);
            vertices.Add(local);
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

    private void AddSideWall(
        PowderFit fit,
        int segments,
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<int> triangles)
    {
        int start = vertices.Count;
        float topY = fit.BaseY + fit.Height;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float u = (float)i / segments;

            vertices.Add(new Vector3(fit.LocalBaseCenter.x + radial.x * fit.Radius, fit.BaseY, fit.LocalBaseCenter.z + radial.z * fit.Radius));
            normals.Add(radial);
            uvs.Add(new Vector2(u, 0f));

            vertices.Add(new Vector3(fit.LocalBaseCenter.x + radial.x * fit.Radius, topY, fit.LocalBaseCenter.z + radial.z * fit.Radius));
            normals.Add(radial);
            uvs.Add(new Vector2(u, 1f));
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

    private void AddNoisyTop(
        PowderFit fit,
        int segments,
        int rings,
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<int> triangles)
    {
        int[] ringStarts = new int[rings + 1];
        float topY = fit.BaseY + fit.Height;
        float domeHeight = Mathf.Clamp(moundHeight, 0.001f, fit.Height * 0.55f);

        for (int ring = 0; ring <= rings; ring++)
        {
            ringStarts[ring] = vertices.Count;
            float inward = (float)ring / rings;
            float ringRadius = fit.Radius * (1f - inward);
            float dome = domeHeight * inward * inward;
            float noise = topNoiseAmount * (1f - inward) * 0.6f;

            if (ring == rings)
            {
                vertices.Add(new Vector3(fit.LocalBaseCenter.x, topY + domeHeight, fit.LocalBaseCenter.z));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(0.5f, 0.5f));
                continue;
            }

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float wave = Mathf.Sin(angle * 3.7f + ring * 1.23f) + Mathf.Sin(angle * 6.1f + ring * 0.77f) * 0.35f;
                float y = topY + dome + wave * noise;

                vertices.Add(new Vector3(
                    fit.LocalBaseCenter.x + Mathf.Cos(angle) * ringRadius,
                    y,
                    fit.LocalBaseCenter.z + Mathf.Sin(angle) * ringRadius));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(
                    0.5f + Mathf.Cos(angle) * (1f - inward) * 0.5f,
                    0.5f + Mathf.Sin(angle) * (1f - inward) * 0.5f));
            }
        }

        for (int ring = 0; ring < rings; ring++)
        {
            int outer = ringStarts[ring];
            int inner = ringStarts[ring + 1];
            bool lastRing = ring == rings - 1;

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                if (lastRing)
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

    private void AddMergedSurfaceGrains(
        PowderFit fit,
        int segments,
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<int> triangles)
    {
        if (!addSurfaceGrains || surfaceGrainCount <= 0)
            return;

        var random = new System.Random(surfaceGrainSeed);
        int count = Mathf.Clamp(surfaceGrainCount, 0, 120);
        float minRadius = Mathf.Max(0.0008f, Mathf.Min(surfaceGrainRadiusRange.x, surfaceGrainRadiusRange.y));
        float maxRadius = Mathf.Max(minRadius, Mathf.Max(surfaceGrainRadiusRange.x, surfaceGrainRadiusRange.y));
        float domeHeight = Mathf.Clamp(moundHeight, 0.001f, fit.Height * 0.55f);
        int grainSegments = Mathf.Clamp(segments / 8, 6, 10);

        for (int grain = 0; grain < count; grain++)
        {
            float angle = RandomRange(random, 0f, Mathf.PI * 2f);
            float distance = Mathf.Sqrt(RandomRange(random, 0f, 1f)) * fit.Radius * 0.82f;
            float grainRadius = RandomRange(random, minRadius, maxRadius);
            float grainHeight = RandomRange(random, surfaceGrainHeight * 0.45f, surfaceGrainHeight);
            float squash = RandomRange(random, 0.55f, 1.15f);
            float rotation = RandomRange(random, 0f, Mathf.PI * 2f);

            Vector3 center = new Vector3(
                fit.LocalBaseCenter.x + Mathf.Cos(angle) * distance,
                GetSurfaceY(fit, distance, angle, domeHeight) + 0.0003f,
                fit.LocalBaseCenter.z + Mathf.Sin(angle) * distance);

            Vector3 right = new Vector3(Mathf.Cos(rotation), 0f, Mathf.Sin(rotation));
            Vector3 forward = new Vector3(-Mathf.Sin(rotation), 0f, Mathf.Cos(rotation));

            int capCenter = vertices.Count;
            vertices.Add(center + Vector3.up * grainHeight);
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int ringStart = vertices.Count;
            for (int i = 0; i < grainSegments; i++)
            {
                float localAngle = i * Mathf.PI * 2f / grainSegments;
                Vector3 offset = right * (Mathf.Cos(localAngle) * grainRadius)
                    + forward * (Mathf.Sin(localAngle) * grainRadius * squash);

                vertices.Add(center + offset);
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(0.5f + Mathf.Cos(localAngle) * 0.5f, 0.5f + Mathf.Sin(localAngle) * 0.5f));
            }

            for (int i = 0; i < grainSegments; i++)
            {
                triangles.Add(capCenter);
                triangles.Add(ringStart + (i + 1) % grainSegments);
                triangles.Add(ringStart + i);
            }
        }
    }

    private float GetSurfaceY(PowderFit fit, float distance, float angle, float domeHeight)
    {
        float topY = fit.BaseY + fit.Height;
        float outward = Mathf.Clamp01(distance / Mathf.Max(MinimumRadius, fit.Radius));
        float inward = 1f - outward;
        float dome = domeHeight * inward * inward;
        float noise = topNoiseAmount * outward * 0.6f;
        float wave = Mathf.Sin(angle * 3.7f + inward * 1.23f) + Mathf.Sin(angle * 6.1f + inward * 0.77f) * 0.35f;
        return topY + dome + wave * noise;
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private readonly struct PowderFit
    {
        public PowderFit(float radius, float height, float baseY, Vector3 localBaseCenter)
        {
            Radius = radius;
            Height = height;
            BaseY = baseY;
            LocalBaseCenter = localBaseCenter;
        }

        public float Radius { get; }
        public float Height { get; }
        public float BaseY { get; }
        public Vector3 LocalBaseCenter { get; }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!regenerateOnValidate)
            return;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || gameObject == null)
                return;

            Rebuild();
        };
    }

    private void OnDrawGizmosSelected()
    {
        PowderFit fit = ResolveFit();
        Vector3 baseWorld = transform.TransformPoint(fit.LocalBaseCenter);
        Vector3 topWorld = transform.TransformPoint(new Vector3(fit.LocalBaseCenter.x, fit.BaseY + fit.Height, fit.LocalBaseCenter.z));

        Gizmos.color = new Color(0.95f, 0.86f, 0.45f, 0.45f);
        Gizmos.DrawWireSphere(baseWorld, fit.Radius);
        Gizmos.DrawWireSphere(topWorld, fit.Radius);
        Gizmos.DrawLine(baseWorld, topWorld);
    }
#endif
}
