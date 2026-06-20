using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class BottleMixtureSuspension : MonoBehaviour
{
    [SerializeField] private LiquidContainer container;
    [SerializeField, Range(4, 32)] private int particleCount = 14;
    [SerializeField] private float particleSize = 0.00012f;
    [SerializeField, Range(0.2f, 0.95f)] private float radialCoverage = 0.72f;
    [SerializeField] private float idleDriftSpeed = 0.45f;
    [SerializeField] private Color particleColor = new Color(1f, 1f, 0.96f, 0.82f);

    private Transform particleRoot;
    private Transform[] particles;
    private float[] angles;
    private float[] radii;
    private float[] heights;
    private float[] phases;
    private Material particleMaterial;

    private void Awake()
    {
        ResolveReferences();
        EnsureParticles();
        RefreshParticles();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureParticles();
    }

    private void LateUpdate()
    {
        RefreshParticles();
    }

    private void ResolveReferences()
    {
        if (container == null)
            container = GetComponent<LiquidContainer>();
    }

    private void EnsureParticles()
    {
        if (container == null || container.LiquidSpace == null)
            return;

        if (particleRoot == null)
        {
            Transform found = container.LiquidSpace.Find("SuspendedDifenhidramin");
            if (found != null)
                particleRoot = found;
        }

        if (particleRoot == null)
        {
            GameObject root = new GameObject("SuspendedDifenhidramin");
            root.transform.SetParent(container.LiquidSpace, false);
            particleRoot = root.transform;
        }

        if (particles != null && particles.Length == particleCount)
            return;

        for (int i = particleRoot.childCount - 1; i >= 0; i--)
            Destroy(particleRoot.GetChild(i).gameObject);

        if (particleMaterial == null)
            particleMaterial = CreateParticleMaterial();

        particles = new Transform[particleCount];
        angles = new float[particleCount];
        radii = new float[particleCount];
        heights = new float[particleCount];
        phases = new float[particleCount];

        Random.State oldState = Random.state;
        Random.InitState(250100);

        for (int i = 0; i < particleCount; i++)
        {
            GameObject fleck = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fleck.name = $"BottleFleck_{i + 1:00}";
            fleck.transform.SetParent(particleRoot, false);

            Collider collider = fleck.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = fleck.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = particleMaterial;

            particles[i] = fleck.transform;
            angles[i] = Random.Range(0f, 360f);
            radii[i] = Mathf.Sqrt(Random.Range(0.08f, 1f));
            heights[i] = Random.Range(0.08f, 0.92f);
            phases[i] = Random.Range(0f, Mathf.PI * 2f);
        }

        Random.state = oldState;
    }

    private void RefreshParticles()
    {
        if (container == null || particleRoot == null || particles == null)
            return;

        bool show = !container.IsEmpty &&
                    container.CurrentLiquid != null &&
                    container.CurrentLiquid.liquidName.ToLowerInvariant().Contains("difenhidramin");

        particleRoot.gameObject.SetActive(show);
        if (!show)
            return;

        float visibleHeight = Mathf.Max(0.0002f, container.VisualMaxHeightLocal * container.FillRatio);
        float radiusX = container.VisualDiameterXLocal * 0.5f * radialCoverage;
        float radiusZ = container.VisualDiameterZLocal * 0.5f * radialCoverage;

        for (int i = 0; i < particles.Length; i++)
        {
            Transform particle = particles[i];
            if (particle == null)
                continue;

            angles[i] += idleDriftSpeed * (0.7f + i * 0.035f) * Time.deltaTime;
            float angle = angles[i] * Mathf.Deg2Rad;
            float bob = Mathf.Sin(Time.time * 0.65f + phases[i]) * visibleHeight * 0.018f;
            float normalizedHeight = Mathf.Clamp01(heights[i] * container.FillRatio);
            float profileScale = container.GetDiameterScaleAtHeight(normalizedHeight);

            particle.localPosition = new Vector3(
                Mathf.Cos(angle) * radiusX * profileScale * radii[i],
                Mathf.Clamp(visibleHeight * heights[i] + bob, particleSize, visibleHeight - particleSize),
                Mathf.Sin(angle) * radiusZ * profileScale * radii[i]);
            particle.localScale = Vector3.one * particleSize;
        }
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

        Material material = new Material(shader)
        {
            name = "Runtime_Bottle_Difenhidramin_Flecks",
            hideFlags = HideFlags.DontSave
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", particleColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", particleColor);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent + 5;
        return material;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        particleCount = Mathf.Clamp(particleCount, 4, 32);
        particleSize = Mathf.Max(0.00002f, particleSize);
        idleDriftSpeed = Mathf.Max(0f, idleDriftSpeed);
    }
#endif
}
