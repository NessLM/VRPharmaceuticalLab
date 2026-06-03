using UnityEngine;

public sealed class PowderSpillFXController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BottleLid requiredOpenLid;
    [SerializeField] private Transform bottleRoot;
    [SerializeField] private Transform spillOrigin;
    [SerializeField] private ParticleSystem spillFX;
    [SerializeField] private Rigidbody bottleRigidbody;

    [Header("Particle Appearance")]
    [Tooltip("Material for spill particles. Should use URP Particles/Unlit shader. If null, a white runtime material is created.")]
    [SerializeField] private Material spillParticleMaterial;

    [Header("Mouth Rig Safety")]
    [SerializeField] private bool keepMouthRigAttached = true;
    [SerializeField] private Vector3 mouthRigLocalPosition = new Vector3(0f, 0.43f, 0f);

    [Header("Emission")]
    [SerializeField] private float mouthDownStartDot = 0.15f;
    [SerializeField] private float mouthDownFullDot = 0.75f;
    [SerializeField] private float minAngularVelocity = 0.45f;
    [SerializeField] private float maxEmissionRate = 18f;
    [SerializeField] private float burstCooldown = 0.08f;
    [SerializeField] private int minBurst = 2;
    [SerializeField] private int maxBurst = 5;
    [SerializeField] private bool debugLogs;

    private float nextBurstTime;
    private Material runtimeMaterial;

    private void Awake()
    {
        ResolveReferences();
        AttachMouthRigToBottle();
        ConfigureParticles();
        StopParticles();
    }

    private void OnDisable()
    {
        StopParticles();
    }

    private void Update()
    {
        if (!CanEmit(out float intensity))
        {
            StopParticles();
            return;
        }

        if (spillFX == null || Time.time < nextBurstTime)
            return;

        nextBurstTime = Time.time + burstCooldown;
        int burstCount = Mathf.RoundToInt(Mathf.Lerp(minBurst, maxBurst, intensity));

        for (int i = 0; i < burstCount; i++)
        {
            Vector3 direction = Vector3.Slerp(spillOrigin.up, Vector3.down, 0.35f).normalized;
            Vector3 jitter = Random.insideUnitSphere * 0.025f;
            var emitParams = new ParticleSystem.EmitParams
            {
                position = spillOrigin.position + jitter,
                velocity = (direction + Random.insideUnitSphere * 0.16f).normalized * Random.Range(0.05f, 0.16f),
                startSize = Random.Range(0.006f, 0.018f),
                startLifetime = Random.Range(0.18f, 0.35f),
                startColor = new Color(1f, 1f, 1f, 0.9f)
            };

            spillFX.Emit(emitParams, 1);
        }
    }

    private bool CanEmit(out float intensity)
    {
        intensity = 0f;

        if (!IsLidOpenEnough() || spillOrigin == null)
            return false;

        float mouthDown = Vector3.Dot(spillOrigin.up, Vector3.down);
        if (mouthDown < mouthDownStartDot)
            return false;

        float angularVelocity = bottleRigidbody != null ? bottleRigidbody.angularVelocity.magnitude : 0f;
        float mouthFactor = Mathf.InverseLerp(mouthDownStartDot, mouthDownFullDot, mouthDown);
        float motionFactor = bottleRigidbody != null
            ? Mathf.Clamp01(angularVelocity / Mathf.Max(0.01f, minAngularVelocity * 2f))
            : 1f;

        intensity = Mathf.Clamp01(mouthFactor * Mathf.Lerp(0.45f, 1f, motionFactor));
        return intensity > 0f;
    }

    private bool IsLidOpenEnough()
    {
        return requiredOpenLid == null || requiredOpenLid.IsOpen || requiredOpenLid.transform.parent == null;
    }

    private void AttachMouthRigToBottle()
    {
        if (!keepMouthRigAttached || bottleRoot == null)
            return;

        Transform rig = transform.parent != null && transform.parent.name == "MouthRig"
            ? transform.parent
            : transform;

        if (rig == null)
            return;

        if (rig.parent != bottleRoot)
            rig.SetParent(bottleRoot, false);

        rig.localPosition = mouthRigLocalPosition;
        rig.localRotation = Quaternion.identity;
        rig.localScale = Vector3.one;
    }

    private void ResolveReferences()
    {
        if (spillOrigin == null)
            spillOrigin = transform;

        if (bottleRoot == null)
            bottleRoot = transform.root;

        if (bottleRigidbody == null)
            bottleRigidbody = GetComponentInParent<Rigidbody>();

        if (requiredOpenLid == null)
            requiredOpenLid = GetComponentInParent<BottleLid>();

        if (spillFX == null && spillOrigin != null)
        {
            Transform fxChild = spillOrigin.Find("PowderSpillFX");
            if (fxChild != null)
                spillFX = fxChild.GetComponent<ParticleSystem>() ?? fxChild.gameObject.AddComponent<ParticleSystem>();
        }
    }

    private void ConfigureParticles()
    {
        if (spillFX == null)
            return;

        ParticleSystem.MainModule main = spillFX.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.018f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f));
        main.maxParticles = Mathf.Clamp(Mathf.RoundToInt(maxEmissionRate * 4f), 48, 80);
        main.gravityModifier = 0.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = spillFX.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = spillFX.shape;
        shape.enabled = false;

        ParticleSystem.CollisionModule collision = spillFX.collision;
        collision.enabled = false;

        ParticleSystem.TrailModule trails = spillFX.trails;
        trails.enabled = false;

        ParticleSystem.LightsModule lights = spillFX.lights;
        lights.enabled = false;

        ParticleSystemRenderer renderer = spillFX.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            renderer = spillFX.gameObject.AddComponent<ParticleSystemRenderer>();

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.minParticleSize = 0.0005f;
        renderer.maxParticleSize = 0.018f;
        renderer.sharedMaterial = GetRuntimeMaterial();
    }

    private void StopParticles()
    {
        if (spillFX != null && spillFX.isPlaying)
            spillFX.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    /// <summary>
    /// Manually emit a burst of powder particles for testing.
    /// Works directly from the Inspector via context menu — no need to tilt the bottle.
    /// </summary>
    [ContextMenu("Test Emit Spill")]
    public void TestEmitSpill()
    {
        if (spillFX == null)
            ResolveReferences();

        if (spillFX == null)
        {
            Debug.LogWarning("[PowderSpillFXController] spillFX is null. Make sure PowderSpillFX child exists.", this);
            return;
        }

        if (!Application.isPlaying)
        {
            Debug.Log("[PowderSpillFXController] TestEmitSpill works only in Play Mode.", this);
            return;
        }

        int burstCount = Mathf.RoundToInt(Mathf.Lerp(minBurst, maxBurst, 0.7f));
        Transform origin = spillOrigin != null ? spillOrigin : transform;

        for (int i = 0; i < burstCount; i++)
        {
            Vector3 direction = Vector3.Slerp(origin.up, Vector3.down, 0.4f).normalized;
            var emitParams = new ParticleSystem.EmitParams
            {
                position = origin.position + Random.insideUnitSphere * 0.02f,
                velocity = (direction + Random.insideUnitSphere * 0.16f).normalized * Random.Range(0.05f, 0.16f),
                startSize = Random.Range(0.006f, 0.018f),
                startLifetime = Random.Range(0.18f, 0.35f),
                startColor = new Color(1f, 1f, 1f, 0.9f)
            };
            spillFX.Emit(emitParams, 1);
        }

        Debug.Log($"[PowderSpillFXController] TestEmitSpill: emitted {burstCount} particles.", this);
    }

    private Material GetRuntimeMaterial()
    {
        // Use the assigned material if available.
        if (spillParticleMaterial != null)
            return spillParticleMaterial;

        if (runtimeMaterial != null)
            return runtimeMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        runtimeMaterial = new Material(shader)
        {
            name = "Runtime_Difenhidramin_SpillFX"
        };

        Color color = new Color(1f, 1f, 1f, 0.9f);
        if (runtimeMaterial.HasProperty("_BaseColor"))
            runtimeMaterial.SetColor("_BaseColor", color);
        if (runtimeMaterial.HasProperty("_Color"))
            runtimeMaterial.SetColor("_Color", color);

        return runtimeMaterial;
    }
}
