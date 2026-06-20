using UnityEngine;

[DisallowMultipleComponent]
public class MortarWaterVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MortarController mortarController;
    [SerializeField] private Transform waterVisual;
    [SerializeField] private Renderer waterRenderer;
    [SerializeField] private Material waterMaterial;

    [Header("Liquid Surface")]
    [SerializeField] private bool createVisualIfMissing = true;
    [SerializeField] private bool hideWhenEmpty = true;
    [SerializeField] private float visualMaxWaterMl = 100f;
    [SerializeField] private Vector3 emptyLocalPosition = new Vector3(0f, 0f, 0.000098f);
    [SerializeField] private Vector3 fullLocalPosition = new Vector3(0f, 0f, 0.00017f);
    [SerializeField] private Vector2 emptyLocalRadius = new Vector2(0.0017f, 0.0017f);
    [SerializeField] private Vector2 fullLocalRadius = new Vector2(0.00245f, 0.00245f);
    [SerializeField] private float emptyLocalDepth = 0.000045f;
    [SerializeField] private float fullLocalDepth = 0.00012f;

    [Header("Liquid Color")]
    [SerializeField] private Color firstWaterColor = new Color(0.86f, 0.95f, 1f, 0.48f);
    [SerializeField] private Color mixedColor = new Color(0.96f, 0.97f, 0.92f, 0.58f);
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 0.34f);

    [Header("Suspended White Powder")]
    [SerializeField] private bool showSuspendedPowder = true;
    [SerializeField, Range(4, 40)] private int particleCount = 22;
    [SerializeField] private float particleMinSize = 0.000035f;
    [SerializeField] private float particleMaxSize = 0.000075f;
    [SerializeField] private float idleOrbitSpeed = 18f;
    [SerializeField] private float stirringOrbitSpeed = 190f;

    [Header("Stir Effect")]
    [SerializeField] private bool keepSurfaceWorldLevel = true;
    [SerializeField] private float surfacePulseAmount = 0.035f;
    [SerializeField] private float surfacePulseSpeed = 9f;
    [SerializeField] private float rippleRotationSpeed = 150f;
    [SerializeField] private float stirringWobbleAmount = 0.000018f;
    [SerializeField] private float stirringWobbleSpeed = 11f;

    private Transform particleRoot;
    private Transform rippleVisual;
    private Renderer rippleRenderer;
    private Material runtimeWaterMaterial;
    private Material runtimeRippleMaterial;
    private Material runtimeParticleMaterial;
    private Transform[] particles;
    private float[] particleAngles;
    private float[] particleRadii;
    private float[] particleSpeeds;
    private float[] particlePhases;
    private float[] particleSizes;
    private float[] particleDepths;
    private float swirlAngle;
    private float rippleAngle;

    private void Awake()
    {
        ResolveReferences();
        EnsureVisual();
        EnsureSuspendedPowder();
        UpdateVisual(true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureVisual();
        EnsureSuspendedPowder();
        UpdateVisual(true);
    }

    private void LateUpdate()
    {
        UpdateVisual(false);
    }

    public void ForceRefreshVisual()
    {
        ResolveReferences();
        EnsureVisual();
        EnsureSuspendedPowder();
        UpdateVisual(true);
    }

    private void ResolveReferences()
    {
        if (mortarController == null)
            mortarController = GetComponent<MortarController>();

        if (mortarController == null)
            mortarController = GetComponentInParent<MortarController>();

        if (waterVisual == null)
        {
            Transform found = transform.Find("MortarWaterVisual");

            if (found == null)
                found = transform.Find("MortarLiquidVisual");

            if (found != null)
                waterVisual = found;
        }

        if (waterRenderer == null && waterVisual != null)
            waterRenderer = waterVisual.GetComponentInChildren<Renderer>(true);
    }

    private void EnsureVisual()
    {
        if (waterVisual == null && createVisualIfMissing)
        {
            GameObject obj = new GameObject("MortarWaterVisual");
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = emptyLocalPosition;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = new Vector3(emptyLocalRadius.x, emptyLocalRadius.y, emptyLocalDepth);

            MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();

            meshFilter.sharedMesh = BuildSurfaceMesh();
            waterVisual = obj.transform;
            waterRenderer = meshRenderer;
        }

        if (waterVisual == null)
            return;

        // Mortar root sudah X=-90. Mesh dibuat pada bidang XY, jadi tidak perlu rotasi 90 derajat lagi.
        waterVisual.localRotation = Quaternion.identity;

        MeshFilter waterFilter = waterVisual.GetComponent<MeshFilter>();
        if (waterFilter == null)
            waterFilter = waterVisual.gameObject.AddComponent<MeshFilter>();

        if (waterFilter.sharedMesh == null)
            waterFilter.sharedMesh = BuildSurfaceMesh();

        if (waterRenderer == null)
            waterRenderer = waterVisual.GetComponentInChildren<Renderer>(true);

        if (waterRenderer == null)
            waterRenderer = waterVisual.gameObject.AddComponent<MeshRenderer>();

        if (waterRenderer != null)
        {
            if (waterMaterial != null)
            {
                waterRenderer.sharedMaterial = waterMaterial;
            }
            else
            {
                if (runtimeWaterMaterial == null)
                    runtimeWaterMaterial = CreateTransparentMaterial("Runtime_MortarLiquid", firstWaterColor);

                waterRenderer.sharedMaterial = runtimeWaterMaterial;
            }
        }

        EnsureRippleVisual();
    }

    private void EnsureRippleVisual()
    {
        if (waterVisual == null)
            return;

        if (rippleVisual == null)
        {
            Transform found = waterVisual.Find("MixRipple");

            if (found != null)
                rippleVisual = found;
        }

        if (rippleVisual == null)
        {
            GameObject ripple = new GameObject("MixRipple");
            ripple.transform.SetParent(waterVisual, false);
            ripple.transform.localPosition = new Vector3(0f, 0f, 0.025f);
            ripple.transform.localRotation = Quaternion.identity;
            ripple.transform.localScale = Vector3.one;

            MeshFilter filter = ripple.AddComponent<MeshFilter>();
            rippleRenderer = ripple.AddComponent<MeshRenderer>();
            filter.sharedMesh = BuildRippleMesh();
            rippleVisual = ripple.transform;
        }

        if (rippleRenderer == null)
            rippleRenderer = rippleVisual.GetComponent<Renderer>();

        if (runtimeRippleMaterial == null)
            runtimeRippleMaterial = CreateTransparentMaterial("Runtime_MortarRipple", highlightColor);

        if (rippleRenderer != null && runtimeRippleMaterial != null)
            rippleRenderer.sharedMaterial = runtimeRippleMaterial;
    }

    private void EnsureSuspendedPowder()
    {
        if (!showSuspendedPowder || mortarController == null)
            return;

        if (particleRoot == null)
        {
            Transform found = transform.Find("MortarSuspendedPowder");

            if (found != null)
                particleRoot = found;
        }

        if (particleRoot == null)
        {
            GameObject root = new GameObject("MortarSuspendedPowder");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = emptyLocalPosition;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            particleRoot = root.transform;
        }

        int count = Mathf.Clamp(particleCount, 4, 40);

        if (particles != null && particles.Length == count)
            return;

        for (int i = particleRoot.childCount - 1; i >= 0; i--)
            Destroy(particleRoot.GetChild(i).gameObject);

        if (runtimeParticleMaterial == null)
            runtimeParticleMaterial = CreateParticleMaterial();

        particles = new Transform[count];
        particleAngles = new float[count];
        particleRadii = new float[count];
        particleSpeeds = new float[count];
        particlePhases = new float[count];
        particleSizes = new float[count];
        particleDepths = new float[count];

        Random.State oldState = Random.state;
        Random.InitState(250100);

        for (int i = 0; i < count; i++)
        {
            GameObject fleck = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fleck.name = $"DifenhidraminFleck_{i + 1:00}";
            fleck.transform.SetParent(particleRoot, false);

            Collider col = fleck.GetComponent<Collider>();
            if (col != null)
                Destroy(col);

            Renderer renderer = fleck.GetComponent<Renderer>();
            if (renderer != null && runtimeParticleMaterial != null)
                renderer.sharedMaterial = runtimeParticleMaterial;

            particles[i] = fleck.transform;
            particleAngles[i] = Random.Range(0f, 360f);
            particleRadii[i] = Mathf.Sqrt(Random.Range(0.05f, 1f));
            particleSpeeds[i] = Random.Range(0.65f, 1.35f);
            particlePhases[i] = Random.Range(0f, Mathf.PI * 2f);
            particleSizes[i] = Random.Range(particleMinSize, particleMaxSize);
            particleDepths[i] = Random.Range(0.12f, 0.82f);
        }

        Random.state = oldState;
    }

    private void UpdateVisual(bool force)
    {
        if (mortarController == null || waterVisual == null)
            return;

        float waterMl = mortarController.CurrentWaterMl;
        float water01 = visualMaxWaterMl > 0f ? Mathf.Clamp01(waterMl / visualMaxWaterMl) : 0f;
        float mix01 = mortarController.OverallMix01;
        bool show = !hideWhenEmpty || waterMl > 0.1f;

        waterVisual.gameObject.SetActive(show);

        if (particleRoot != null)
            particleRoot.gameObject.SetActive(show && showSuspendedPowder && mortarController.CurrentPowderMg > 0.1f);

        if (!show)
            return;

        // Mortar melebar ke atas. Kurva terpisah membuat volume terlihat tumbuh
        // dari dasar, lalu radius mengikuti dinding mangkuk secara bertahap.
        float surfaceFill01 = Mathf.SmoothStep(0f, 1f, water01);
        float radiusFill01 = Mathf.Pow(water01, 0.82f);
        float depthFill01 = Mathf.Pow(water01, 0.92f);
        Vector3 surfacePosition = Vector3.Lerp(emptyLocalPosition, fullLocalPosition, surfaceFill01);
        Vector2 radius = Vector2.Lerp(emptyLocalRadius, fullLocalRadius, radiusFill01);
        float depth = Mathf.Lerp(emptyLocalDepth, fullLocalDepth, depthFill01);
        Vector3 baseScale = new Vector3(radius.x, radius.y, depth);
        bool stirring = mortarController.IsActivelyStirring;

        float pulse = stirring
            ? 1f + Mathf.Sin(Time.time * surfacePulseSpeed) * surfacePulseAmount
            : 1f;

        Vector3 animatedScale = baseScale;
        animatedScale.x *= pulse;
        animatedScale.y *= 2f - pulse;

        if (stirring)
        {
            swirlAngle += Time.deltaTime * rippleRotationSpeed * 0.35f;
            rippleAngle += Time.deltaTime * rippleRotationSpeed;
        }

        Vector3 wobble = stirring
            ? new Vector3(
                Mathf.Sin(Time.time * stirringWobbleSpeed) * stirringWobbleAmount,
                Mathf.Cos(Time.time * stirringWobbleSpeed * 0.83f) * stirringWobbleAmount,
                0f
            )
            : Vector3.zero;

        waterVisual.localPosition = surfacePosition + wobble;

        if (keepSurfaceWorldLevel)
        {
            Quaternion levelRotation = Quaternion.FromToRotation(Vector3.forward, Vector3.up);
            waterVisual.rotation = Quaternion.AngleAxis(swirlAngle, Vector3.up) * levelRotation;
        }
        else
        {
            waterVisual.localRotation = stirring
                ? Quaternion.Euler(0f, 0f, swirlAngle)
                : Quaternion.identity;
        }

        waterVisual.localScale = animatedScale;

        if (rippleVisual != null)
        {
            rippleVisual.gameObject.SetActive(stirring);
            rippleVisual.localRotation = Quaternion.Euler(0f, 0f, rippleAngle);

            float ringPulse = 0.88f + Mathf.Sin(Time.time * 7f) * 0.06f;
            rippleVisual.localScale = new Vector3(ringPulse, ringPulse, 1f);
        }

        ApplyLiquidColor(water01, mix01, stirring);
        UpdateSuspendedPowder(surfacePosition, depth, water01, mix01, stirring, force);
    }

    private void ApplyLiquidColor(float water01, float mix01, bool stirring)
    {
        Material mat = waterMaterial != null ? waterRenderer != null ? waterRenderer.sharedMaterial : null : runtimeWaterMaterial;

        if (mat != null)
        {
            Color color = Color.Lerp(firstWaterColor, mixedColor, Mathf.Clamp01(mix01));
            color.a = Mathf.Lerp(firstWaterColor.a, mixedColor.a, Mathf.Max(water01, mix01));

            if (stirring)
                color = Color.Lerp(color, Color.white, 0.02f + Mathf.Sin(Time.time * 8f) * 0.008f);

            SetMaterialColor(mat, color);
        }

        if (runtimeRippleMaterial != null)
        {
            Color ringColor = highlightColor;
            ringColor.a *= stirring ? 1f : 0f;
            SetMaterialColor(runtimeRippleMaterial, ringColor);
        }
    }

    private void UpdateSuspendedPowder(Vector3 surfacePosition, float depth, float water01, float mix01, bool stirring, bool force)
    {
        if (particleRoot == null || particles == null)
            return;

        particleRoot.localPosition = surfacePosition + new Vector3(0f, 0f, 0.000035f);

        if (keepSurfaceWorldLevel)
            particleRoot.rotation = Quaternion.FromToRotation(Vector3.forward, Vector3.up);
        else
            particleRoot.localRotation = Quaternion.identity;

        float liquidRadius = Mathf.Lerp(emptyLocalRadius.x * 0.72f, fullLocalRadius.x * 0.78f, water01);
        float orbitSpeed = stirring ? stirringOrbitSpeed : idleOrbitSpeed;
        int visibleCount = mix01 >= 0.995f
            ? 0
            : Mathf.Clamp(
                Mathf.RoundToInt(particles.Length * Mathf.Lerp(1f, 0.08f, Mathf.SmoothStep(0f, 1f, mix01))),
                1,
                particles.Length
            );

        for (int i = 0; i < particles.Length; i++)
        {
            Transform particle = particles[i];

            if (particle == null)
                continue;

            bool visible = i < visibleCount;

            if (particle.gameObject.activeSelf != visible)
                particle.gameObject.SetActive(visible);

            if (!visible)
                continue;

            if (stirring || idleOrbitSpeed > 0.001f)
                particleAngles[i] += orbitSpeed * particleSpeeds[i] * Time.deltaTime;

            float radians = particleAngles[i] * Mathf.Deg2Rad;
            float radialWave = stirring
                ? 1f + Mathf.Sin(Time.time * 6f + particlePhases[i]) * 0.16f
                : 1f;

            float radius = liquidRadius * particleRadii[i] * radialWave;
            float z = -depth * particleDepths[i];

            if (stirring)
                z += Mathf.Sin(Time.time * 7f + particlePhases[i]) * 0.000012f;

            particle.localPosition = new Vector3(
                Mathf.Cos(radians) * radius,
                Mathf.Sin(radians) * radius,
                z
            );

            float size = particleSizes[i] * (stirring ? 1.12f : 1f);
            particle.localScale = Vector3.one * size;
        }
    }

    private Mesh BuildSurfaceMesh()
    {
        const int segments = 72;
        const int topRings = 5;
        const int sideRings = 8;

        int topVertexCount = 1 + segments * topRings;
        int sideStart = topVertexCount;
        int bottomCenter = sideStart + segments * sideRings;
        Vector3[] vertices = new Vector3[bottomCenter + 1];
        Vector2[] uvs = new Vector2[vertices.Length];
        int topTriangles = segments * 3 + (topRings - 1) * segments * 6;
        int sideTriangles = sideRings * segments * 6;
        int bottomTriangles = segments * 3;
        int[] triangles = new int[topTriangles + sideTriangles + bottomTriangles];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        int vertex = 1;

        for (int ring = 1; ring <= topRings; ring++)
        {
            float radius = ring / (float)topRings;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                float edgeDip = ring == topRings ? -0.018f : 0f;

                vertices[vertex] = new Vector3(x, y, edgeDip);
                uvs[vertex] = new Vector2(0.5f + x, 0.5f + y);
                vertex++;
            }
        }

        for (int ring = 0; ring < sideRings; ring++)
        {
            float vertical01 = ring / (float)(sideRings - 1);
            float curved = Mathf.Pow(vertical01, 0.62f);
            float radius = Mathf.Lerp(0.24f, 1f, curved);
            float z = Mathf.Lerp(-1f, -0.018f, vertical01);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                int index = sideStart + ring * segments + i;
                vertices[index] = new Vector3(x, y, z);
                uvs[index] = new Vector2(0.5f + x, 0.5f + y);
            }
        }

        vertices[bottomCenter] = new Vector3(0f, 0f, -1f);
        uvs[bottomCenter] = new Vector2(0.5f, 0.5f);

        int t = 0;

        for (int i = 0; i < segments; i++)
        {
            triangles[t++] = 0;
            triangles[t++] = 1 + i;
            triangles[t++] = 1 + ((i + 1) % segments);
        }

        for (int ring = 1; ring < topRings; ring++)
        {
            int innerStart = 1 + (ring - 1) * segments;
            int outerStart = 1 + ring * segments;

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int a = innerStart + i;
                int b = innerStart + next;
                int c = outerStart + i;
                int d = outerStart + next;

                triangles[t++] = a;
                triangles[t++] = c;
                triangles[t++] = d;
                triangles[t++] = a;
                triangles[t++] = d;
                triangles[t++] = b;
            }
        }

        int topOuterStart = 1 + (topRings - 1) * segments;

        for (int ring = 0; ring < sideRings - 1; ring++)
        {
            int lowerStart = sideStart + ring * segments;
            int upperStart = lowerStart + segments;

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int a = lowerStart + i;
                int b = lowerStart + next;
                int c = upperStart + i;
                int d = upperStart + next;

                triangles[t++] = a;
                triangles[t++] = c;
                triangles[t++] = d;
                triangles[t++] = a;
                triangles[t++] = d;
                triangles[t++] = b;
            }
        }

        int upperSideStart = sideStart + (sideRings - 1) * segments;
        int lowerSideStart = sideStart;

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            triangles[t++] = topOuterStart + i;
            triangles[t++] = upperSideStart + i;
            triangles[t++] = upperSideStart + next;
            triangles[t++] = topOuterStart + i;
            triangles[t++] = upperSideStart + next;
            triangles[t++] = topOuterStart + next;

            triangles[t++] = bottomCenter;
            triangles[t++] = lowerSideStart + next;
            triangles[t++] = lowerSideStart + i;
        }

        Mesh mesh = new Mesh();
        mesh.name = "MortarLiquidSurface_Generated";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private Mesh BuildRippleMesh()
    {
        const int segments = 72;
        const float innerRadius = 0.58f;
        const float outerRadius = 0.68f;

        Vector3[] vertices = new Vector3[segments * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float cs = Mathf.Cos(angle);
            float sn = Mathf.Sin(angle);

            vertices[i * 2] = new Vector3(cs * innerRadius, sn * innerRadius, 0f);
            vertices[i * 2 + 1] = new Vector3(cs * outerRadius, sn * outerRadius, 0f);
            uvs[i * 2] = new Vector2(0f, i / (float)segments);
            uvs[i * 2 + 1] = new Vector2(1f, i / (float)segments);

            int next = (i + 1) % segments;
            int tri = i * 6;
            int a = i * 2;
            int b = i * 2 + 1;
            int c = next * 2;
            int d = next * 2 + 1;

            triangles[tri] = a;
            triangles[tri + 1] = b;
            triangles[tri + 2] = d;
            triangles[tri + 3] = a;
            triangles[tri + 4] = d;
            triangles[tri + 5] = c;
        }

        Mesh mesh = new Mesh();
        mesh.name = "MortarMixRipple_Generated";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private Material CreateTransparentMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        Material mat = new Material(shader);
        mat.name = materialName;
        mat.hideFlags = HideFlags.DontSave;
        SetMaterialColor(mat, color);

        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f);

        if (mat.HasProperty("_Blend"))
            mat.SetFloat("_Blend", 0f);

        if (mat.HasProperty("_SrcBlend"))
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (mat.HasProperty("_DstBlend"))
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        if (mat.HasProperty("_ZWrite"))
            mat.SetFloat("_ZWrite", 0f);

        if (mat.HasProperty("_Cull"))
            mat.SetFloat("_Cull", 0f);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        return mat;
    }

    private Material CreateParticleMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        Material mat = new Material(shader);
        mat.name = "Runtime_DifenhidraminSuspendedParticles";
        mat.hideFlags = HideFlags.DontSave;
        SetMaterialColor(mat, new Color(1f, 1f, 0.96f, 0.92f));

        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.15f);

        return mat;
    }

    private void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null)
            return;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        visualMaxWaterMl = Mathf.Max(1f, visualMaxWaterMl);
        particleCount = Mathf.Clamp(particleCount, 4, 40);
        particleMinSize = Mathf.Max(0.000005f, particleMinSize);
        particleMaxSize = Mathf.Max(particleMinSize, particleMaxSize);
        emptyLocalDepth = Mathf.Max(0.000005f, emptyLocalDepth);
        fullLocalDepth = Mathf.Max(emptyLocalDepth, fullLocalDepth);
        idleOrbitSpeed = Mathf.Max(0f, idleOrbitSpeed);
        stirringOrbitSpeed = Mathf.Max(idleOrbitSpeed, stirringOrbitSpeed);
        stirringWobbleAmount = Mathf.Max(0f, stirringWobbleAmount);
        stirringWobbleSpeed = Mathf.Max(0f, stirringWobbleSpeed);
    }
#endif
}
