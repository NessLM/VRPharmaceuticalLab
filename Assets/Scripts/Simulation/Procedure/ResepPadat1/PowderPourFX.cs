using UnityEngine;

/// <summary>
/// Owns a runtime ParticleSystem that emits small white "powder" particles,
/// styled after <c>PowderSpillFXController</c>. Used to show powder falling from
/// a tilted perkamen into an open capsule mouth during the Step 4 pour sequence.
/// </summary>
public class PowderPourFX : MonoBehaviour
{
    [Header("Particle Appearance")]
    [Tooltip("Material for the powder particles. If null, a white runtime material is created.")]
    [SerializeField] private Material particleMaterial;

    [Header("Emission Tuning")]
    [Tooltip("Particles emitted each EmitAt call (per frame while pouring).")]
    [SerializeField] private int particlesPerEmit = 2;
    [Tooltip("Base downward speed of the powder stream (m/s).")]
    [SerializeField] private float emitSpeed = 0.3f;
    [Tooltip("How much the pour direction is biased toward straight down (0=along perkamen, 1=straight down).")]
    [SerializeField, Range(0f, 1f)] private float downwardBias = 0.3f;

    private ParticleSystem _ps;
    private Material _runtimeMaterial;

    private void Awake()
    {
        EnsureParticleSystem();
    }

    private void EnsureParticleSystem()
    {
        if (_ps != null) return;

        _ps = GetComponent<ParticleSystem>();
        if (_ps == null)
            _ps = gameObject.AddComponent<ParticleSystem>();

        ConfigureParticles();
    }

    private void ConfigureParticles()
    {
        ParticleSystem.MainModule main = _ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.018f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f));
        main.maxParticles = 80;
        main.gravityModifier = 0.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = _ps.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = _ps.shape;
        shape.enabled = false;

        ParticleSystem.CollisionModule collision = _ps.collision;
        collision.enabled = false;

        ParticleSystem.TrailModule trails = _ps.trails;
        trails.enabled = false;

        ParticleSystem.LightsModule lights = _ps.lights;
        lights.enabled = false;

        ParticleSystemRenderer renderer = _ps.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            renderer = _ps.gameObject.AddComponent<ParticleSystemRenderer>();

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.minParticleSize = 0.0005f;
        renderer.maxParticleSize = 0.018f;
        renderer.sharedMaterial = GetRuntimeMaterial();
    }

    /// <summary>
    /// Emit a small burst of powder particles travelling from <paramref name="fromWorld"/>
    /// (the perkamen pour origin) toward <paramref name="toWorld"/> (the capsule mouth),
    /// biased toward straight down so it visibly falls.
    /// </summary>
    public void EmitAt(Vector3 fromWorld, Vector3 toWorld)
    {
        EnsureParticleSystem();

        transform.position = fromWorld;

        Vector3 toTarget = toWorld - fromWorld;
        Vector3 baseDir = toTarget.sqrMagnitude > 0.0000001f ? toTarget.normalized : Vector3.down;
        Vector3 dir = Vector3.Slerp(baseDir, Vector3.down, downwardBias).normalized;

        for (int i = 0; i < particlesPerEmit; i++)
        {
            Vector3 jitter = Random.insideUnitSphere * 0.01f;
            var emitParams = new ParticleSystem.EmitParams
            {
                position = fromWorld + jitter,
                velocity = (dir + Random.insideUnitSphere * 0.16f).normalized * emitSpeed,
                startSize = Random.Range(0.006f, 0.018f),
                startLifetime = Random.Range(0.18f, 0.35f),
                startColor = new Color(1f, 1f, 1f, 0.9f)
            };
            _ps.Emit(emitParams, 1);
        }
    }

    /// <summary>Stop emitting. Live particles continue to fall and die naturally.</summary>
    public void StopEmitting()
    {
        if (_ps != null && _ps.isPlaying)
            _ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    private Material GetRuntimeMaterial()
    {
        if (particleMaterial != null)
            return particleMaterial;

        if (_runtimeMaterial != null)
            return _runtimeMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        _runtimeMaterial = new Material(shader)
        {
            name = "Runtime_PowderPourFX"
        };

        Color color = new Color(1f, 1f, 1f, 0.9f);
        if (_runtimeMaterial.HasProperty("_BaseColor"))
            _runtimeMaterial.SetColor("_BaseColor", color);
        if (_runtimeMaterial.HasProperty("_Color"))
            _runtimeMaterial.SetColor("_Color", color);

        return _runtimeMaterial;
    }
}
