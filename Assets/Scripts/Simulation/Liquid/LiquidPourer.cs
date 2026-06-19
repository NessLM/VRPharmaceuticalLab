using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(LiquidContainer))]
public class LiquidPourer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LiquidContainer sourceContainer;
    [SerializeField] private Transform pourPoint;

    [Header("Mouth / Pour Start")]
    [Tooltip("OFF = stream starts exactly from PourPoint. Safer for imported/rotated lab glass. ON = stream starts from the lowest rim edge around PourPoint.")]
    [SerializeField] private bool useDynamicMouthEdge = false;

    [Tooltip("Used only when Use Dynamic Mouth Edge is ON.")]
    [SerializeField] private float mouthRadius = 0.06f;

    [Header("Pouring")]
    [Header("Fill Based Pour Angle")]
    [SerializeField] private bool useFillAmountBasedPourAngle = true;
    [SerializeField] private float emptyPourAngle = 95f;
    [SerializeField] private float fullPourAngle = 55f;
    [SerializeField] private float pourAngleThreshold = 60f;
    [SerializeField] private float pourRateMlPerSecond = 65f;

    [Tooltip("Radius used around the stream path to find FillZone/LiquidReceiverZone. Raise this if transfer does not enter the other glass.")]
    [SerializeField] private float receiverSearchRadius = 0.24f;

    [SerializeField] private LayerMask receiverLayers = ~0;

    [Tooltip("How many points along the predicted stream are checked for a receiver. Higher = easier to catch FillZone but slightly more physics checks.")]
    [SerializeField, Range(1, 12)] private int receiverPathSamples = 6;

    [Header("Stream Visual")]
    [SerializeField] private Transform pourVisualRoot;
    [SerializeField] private LineRenderer pourLine;
    [SerializeField] private Color pourLineColor = new Color(0.35f, 0.75f, 1f, 0.72f);
    [SerializeField] private float pourLineStartWidth = 0.007f;
    [SerializeField] private float pourLineEndWidth = 0.0035f;
    [SerializeField, Range(3, 40)] private int streamSegments = 16;
    [SerializeField] private float streamWobble = 0.003f;
    [SerializeField] private float streamWobbleSpeed = 18f;

    [Header("No Receiver Spill Animation - No Puddle")]
    [Tooltip("If ON, source volume decreases even when the stream does not hit a receiver.")]
    [SerializeField] private bool drainWhenNoReceiver = true;

    [Tooltip("If ON, show a short falling stream when water misses the target. It does not create puddle/residue.")]
    [SerializeField] private bool drawNoReceiverSpillStream = true;

    [Tooltip("Extra drain speed when the water misses a receiver. This prevents the liquid from visually returning after a real spill gesture.")]
    [SerializeField, Range(1f, 4f)] private float missedReceiverDrainMultiplier = 1.6f;

    [Tooltip("Horizontal/sideways velocity of a missed stream.")]
    [SerializeField] private float streamInitialSpeed = 0.12f;

    [Tooltip("Downward velocity of a missed stream.")]
    [SerializeField] private float streamDownSpeed = 0.18f;

    [Tooltip("Gravity curve for the missed stream visual only.")]
    [SerializeField] private float streamGravity = 3.5f;

    [Tooltip("Lifetime/path length of the missed stream visual only.")]
    [SerializeField] private float streamMaxTime = 0.25f;

    [Header("Optional Particles")]
    [SerializeField] private ParticleSystem streamParticles;
    [SerializeField] private ParticleSystem splashParticles;
    [SerializeField] private float splashMlPerBurst = 0.35f;
    [SerializeField] private int splashParticlesPerBurst = 8;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private float debugLogCooldown;
    private const float DebugLogInterval = 0.75f;

    public Transform PourPoint => pourPoint != null ? pourPoint : transform;
    public bool IsPouring { get; private set; }
    public LiquidReceiverZone CurrentReceiver { get; private set; }

    private readonly Collider[] receiverHits = new Collider[32];
    private Material runtimeLineMaterial;
    private float splashAccumulatorMl;

    private void Reset()
    {
        sourceContainer = GetComponent<LiquidContainer>();
        pourPoint = transform.Find("PourPoint");
        ApplyFocusPourNoPuddlePreset();
    }

    private void Awake()
    {
        if (sourceContainer == null)
            sourceContainer = GetComponent<LiquidContainer>();

        if (pourPoint == null)
            pourPoint = transform.Find("PourPoint");

        if (pourLine == null && pourVisualRoot != null)
            pourLine = pourVisualRoot.GetComponent<LineRenderer>();

        if (pourLine != null)
            ConfigurePourLine();

        StopPourVisual();
    }

    private void Update()
    {
        if (!CanPour(out string blockReason))
        {
            LogPourBlocked(blockReason);
            StopPourVisual();
            return;
        }

        float amountMl = Mathf.Min(pourRateMlPerSecond * Time.deltaTime, sourceContainer.CurrentMl);
        if (amountMl <= 0f)
        {
            StopPourVisual();
            return;
        }

        Vector3 start = GetPourStartPosition();
        Vector3 missVelocity = GetNoReceiverStreamVelocity();

        MortarWaterIntakeZone mortarReceiver = FindMortarReceiverAlongStream(start, missVelocity);
        if (mortarReceiver != null)
        {
            float transferredMl = mortarReceiver.ReceiveFrom(sourceContainer, amountMl);

            if (transferredMl > 0f)
            {
                CurrentReceiver = null;
                Vector3 end = mortarReceiver.ReceiverPoint.position;
                DrawStreamToTarget(start, end, true);
                PlayStreamParticles(start, (end - start).normalized);

                if (debugLogs)
                    Debug.Log($"{name} transferred {transferredMl:0.###} ml directly to mortar", this);

                return;
            }

            // The stream is aimed at the mortar but the current 50 ml phase is already full.
            // Do not silently drain the measuring glass as a missed spill.
            StopPourVisual();
            return;
        }

        LiquidReceiverZone receiver = FindReceiverAlongStream(start, missVelocity);

        if (receiver != null)
        {
            float transferredMl = receiver.ReceiveFrom(sourceContainer, amountMl);

            if (transferredMl > 0f)
            {
                CurrentReceiver = receiver;
                Vector3 end = GetReceiverTargetPosition(receiver, start);
                DrawStreamToTarget(start, end, true);
                PlayStreamParticles(start, (end - start).normalized);

                if (debugLogs)
                    Debug.Log($"{name} transferred {transferredMl:0.###} ml to {receiver.name}", this);

                return;
            }
        }

        CurrentReceiver = null;

        if (!drainWhenNoReceiver)
        {
            LogPourBlocked("no valid receiver");
            StopPourVisual();
            return;
        }

        // Missed pour: drain faster than receiver transfer so the liquid does not visually return after a spill gesture.
        float missedAmountMl = Mathf.Min(sourceContainer.CurrentMl, amountMl * missedReceiverDrainMultiplier);
        sourceContainer.RemoveLiquid(missedAmountMl);

        if (drawNoReceiverSpillStream)
        {
            DrawNoReceiverStream(start, missVelocity);
            PlayStreamParticles(start, missVelocity.normalized);
            EmitSplashAt(GetNoReceiverStreamEnd(start, missVelocity), missedAmountMl);
        }
        else
        {
            StopPourVisual();
        }

        if (debugLogs)
            Debug.Log($"{name} poured outside receiver: {missedAmountMl:0.###} ml. No puddle object created.", this);
    }

    private void OnDisable()
    {
        StopPourVisual();
    }

    private void OnDestroy()
    {
        if (runtimeLineMaterial != null)
            Destroy(runtimeLineMaterial);
    }

    private void OnValidate()
    {
        mouthRadius = Mathf.Max(0f, mouthRadius);
        emptyPourAngle = Mathf.Clamp(emptyPourAngle, 0f, 180f);
        fullPourAngle = Mathf.Clamp(fullPourAngle, 0f, 180f);
        pourAngleThreshold = Mathf.Clamp(pourAngleThreshold, 0f, 180f);
        pourRateMlPerSecond = Mathf.Max(0f, pourRateMlPerSecond);
        receiverSearchRadius = Mathf.Max(0.01f, receiverSearchRadius);
        receiverPathSamples = Mathf.Clamp(receiverPathSamples, 1, 12);

        pourLineStartWidth = Mathf.Max(0.0001f, pourLineStartWidth);
        pourLineEndWidth = Mathf.Max(0.0001f, pourLineEndWidth);

        streamSegments = Mathf.Clamp(streamSegments, 3, 40);
        streamWobble = Mathf.Max(0f, streamWobble);
        streamWobbleSpeed = Mathf.Max(0f, streamWobbleSpeed);

        missedReceiverDrainMultiplier = Mathf.Clamp(missedReceiverDrainMultiplier, 1f, 4f);
        streamInitialSpeed = Mathf.Max(0f, streamInitialSpeed);
        streamDownSpeed = Mathf.Max(0f, streamDownSpeed);
        streamGravity = Mathf.Max(0f, streamGravity);
        streamMaxTime = Mathf.Max(0.05f, streamMaxTime);

        splashMlPerBurst = Mathf.Max(0.001f, splashMlPerBurst);
        splashParticlesPerBurst = Mathf.Max(1, splashParticlesPerBurst);
    }

    [ContextMenu("Apply Focus Pour No Puddle Preset")]
    public void ApplyFocusPourNoPuddlePreset()
    {
        useDynamicMouthEdge = false;
        mouthRadius = 0.045f;

        useFillAmountBasedPourAngle = true;
        emptyPourAngle = 95f;
        fullPourAngle = 55f;
        pourAngleThreshold = 60f;
        pourRateMlPerSecond = 65f;

        receiverSearchRadius = 0.30f;
        receiverPathSamples = 8;

        pourLineStartWidth = 0.006f;
        pourLineEndWidth = 0.003f;
        streamSegments = 14;
        streamWobble = 0.0015f;
        streamWobbleSpeed = 16f;

        drainWhenNoReceiver = true;
        drawNoReceiverSpillStream = true;
        missedReceiverDrainMultiplier = 1.6f;
        streamInitialSpeed = 0.10f;
        streamDownSpeed = 0.24f;
        streamGravity = 4.2f;
        streamMaxTime = 0.18f;

        ConfigurePourLine();
    }


    [ContextMenu("Apply Fast Lab Pour Preset")]
    public void ApplyFastLabPourPreset()
    {
        ApplyFocusPourNoPuddlePreset();
        pourRateMlPerSecond = 80f;
        emptyPourAngle = 88f;
        fullPourAngle = 50f;
        pourAngleThreshold = 55f;
        missedReceiverDrainMultiplier = 1.9f;
        streamMaxTime = 0.16f;
        streamGravity = 4.5f;
        ConfigurePourLine();
    }

    [ContextMenu("Delete Existing Runtime Puddles In Scene")]
    public void DeleteExistingRuntimePuddlesInScene()
    {
        Transform[] allTransforms = FindObjectsOfType<Transform>();
        int deleted = 0;

        for (int i = allTransforms.Length - 1; i >= 0; i--)
        {
            Transform t = allTransforms[i];
            if (t == null)
                continue;

            if (t.name != "Runtime_LiquidSpill")
                continue;

            if (Application.isPlaying)
                Destroy(t.gameObject);
            else
                DestroyImmediate(t.gameObject);

            deleted++;
        }

        if (debugLogs)
            Debug.Log($"Deleted {deleted} Runtime_LiquidSpill object(s).", this);
    }

    private bool CanPour(out string blockReason)
    {
        blockReason = null;

        if (sourceContainer == null)
        {
            blockReason = "missing LiquidContainer";
            return false;
        }

        if (sourceContainer.IsEmpty)
        {
            blockReason = "empty";
            return false;
        }

        sourceContainer.EnsureLiquidTypeForPouring();

        if (sourceContainer.CurrentLiquid == null && sourceContainer.GetEffectiveLiquid() == null)
        {
            blockReason = "liquid data is null";
            return false;
        }

        float tilt = GetTiltAngle();
        float required = GetRequiredPourAngle();
        if (tilt < required)
        {
            blockReason = $"tilt {tilt:0.#}° below required {required:0.#}°";
            return false;
        }

        return true;
    }

    private void LogPourBlocked(string reason)
    {
        if (!debugLogs || string.IsNullOrEmpty(reason))
            return;

        if (Time.time < debugLogCooldown)
            return;

        debugLogCooldown = Time.time + DebugLogInterval;
        Debug.Log($"{name} cannot pour: {reason}", this);
    }

    private float GetRequiredPourAngle()
    {
        if (!useFillAmountBasedPourAngle || sourceContainer == null || sourceContainer.CapacityMl <= 0f)
            return pourAngleThreshold;

        float fill01 = Mathf.Clamp01(sourceContainer.CurrentMl / sourceContainer.CapacityMl);

        // Kalau isi sedikit, perlu miring ekstrem. Kalau isi penuh, lebih gampang tumpah.
        return Mathf.Lerp(emptyPourAngle, fullPourAngle, fill01);
    }

    private float GetTiltAngle()
    {
        Vector3 containerUp = GetContainerUp();
        return Vector3.Angle(containerUp, Vector3.up);
    }

    private Vector3 GetContainerUp()
    {
        if (sourceContainer == null)
            return transform.up;

        Vector3 axis = sourceContainer.FillAxisLocal;
        if (axis.sqrMagnitude < 0.0001f)
            axis = Vector3.up;

        return sourceContainer.transform.TransformDirection(axis.normalized).normalized;
    }

    private Vector3 GetPourStartPosition()
    {
        Transform mouth = PourPoint;

        if (!useDynamicMouthEdge)
            return mouth.position;

        Vector3 containerUp = GetContainerUp();
        Vector3 rimDirection = Vector3.ProjectOnPlane(Vector3.down, containerUp);

        if (rimDirection.sqrMagnitude < 0.0001f)
            return mouth.position;

        return mouth.position + rimDirection.normalized * mouthRadius;
    }

    private Vector3 GetNoReceiverStreamVelocity()
    {
        Vector3 containerUp = GetContainerUp();
        Vector3 sideways = Vector3.ProjectOnPlane(Vector3.down, containerUp);

        if (sideways.sqrMagnitude < 0.0001f)
            sideways = transform.forward;

        sideways.Normalize();
        return sideways * streamInitialSpeed + Vector3.down * streamDownSpeed;
    }

    private Vector3 GetBallisticPoint(Vector3 start, Vector3 velocity, float normalizedT)
    {
        float t = streamMaxTime * Mathf.Clamp01(normalizedT);
        return start + velocity * t + 0.5f * Vector3.down * streamGravity * t * t;
    }

    private Vector3 GetNoReceiverStreamEnd(Vector3 start, Vector3 velocity)
    {
        return GetBallisticPoint(start, velocity, 1f);
    }

    private LiquidReceiverZone FindReceiverAlongStream(Vector3 start, Vector3 velocity)
    {
        LiquidReceiverZone bestReceiver = null;
        float bestScore = float.MaxValue;

        int samples = Mathf.Max(1, receiverPathSamples);
        for (int s = 0; s < samples; s++)
        {
            float t = samples == 1 ? 0f : s / (float)(samples - 1);
            Vector3 samplePoint = GetBallisticPoint(start, velocity, t);
            float sampleRadius = receiverSearchRadius;

            int hitCount = Physics.OverlapSphereNonAlloc(
                samplePoint,
                sampleRadius,
                receiverHits,
                receiverLayers,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = receiverHits[i];
                if (hit == null)
                    continue;

                LiquidReceiverZone receiver = hit.GetComponentInParent<LiquidReceiverZone>();
                if (receiver == null)
                    continue;

                LiquidContainer receiverContainer = receiver.GetComponentInParent<LiquidContainer>();
                if (receiverContainer == null || receiverContainer == sourceContainer)
                    continue;

                if (!receiver.CanReceiveFrom(sourceContainer))
                    continue;

                Vector3 target = GetReceiverTargetPosition(receiver, samplePoint);
                float distanceScore = (target - samplePoint).sqrMagnitude;

                // Prefer receivers closer to the stream start. This avoids catching random FillZones behind the target.
                float sequencePenalty = t * 0.02f;
                float score = distanceScore + sequencePenalty;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestReceiver = receiver;
                }
            }
        }

        return bestReceiver;
    }

    private MortarWaterIntakeZone FindMortarReceiverAlongStream(Vector3 start, Vector3 velocity)
    {
        MortarWaterIntakeZone bestReceiver = null;
        float bestScore = float.MaxValue;

        int samples = Mathf.Max(1, receiverPathSamples);
        for (int s = 0; s < samples; s++)
        {
            float t = samples == 1 ? 0f : s / (float)(samples - 1);
            Vector3 samplePoint = GetBallisticPoint(start, velocity, t);

            int hitCount = Physics.OverlapSphereNonAlloc(
                samplePoint,
                receiverSearchRadius,
                receiverHits,
                receiverLayers,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = receiverHits[i];
                if (hit == null)
                    continue;

                MortarWaterIntakeZone receiver = hit.GetComponentInParent<MortarWaterIntakeZone>();
                if (receiver == null || !receiver.CanReceiveFrom(sourceContainer))
                    continue;

                Vector3 target = receiver.ReceiverPoint.position;
                float score = (target - samplePoint).sqrMagnitude + t * 0.02f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestReceiver = receiver;
                }
            }
        }

        return bestReceiver;
    }

    private Vector3 GetReceiverTargetPosition(LiquidReceiverZone receiver, Vector3 fallback)
    {
        if (receiver == null)
            return fallback;

        Collider receiverCollider = receiver.GetComponent<Collider>();
        if (receiverCollider == null)
            receiverCollider = receiver.GetComponentInChildren<Collider>();

        if (receiverCollider != null)
            return receiverCollider.bounds.center;

        return receiver.transform.position;
    }

    private void EnsurePourVisual()
    {
        if (pourLine == null && pourVisualRoot != null)
        {
            pourLine = pourVisualRoot.GetComponent<LineRenderer>();

            if (pourLine == null)
                pourLine = pourVisualRoot.gameObject.AddComponent<LineRenderer>();
        }

        if (pourLine == null)
        {
            GameObject visualObject = new GameObject("PourVisual");
            visualObject.transform.SetParent(transform, false);
            visualObject.transform.localPosition = Vector3.zero;
            visualObject.transform.localRotation = Quaternion.identity;
            visualObject.transform.localScale = Vector3.one;

            pourVisualRoot = visualObject.transform;
            pourLine = visualObject.AddComponent<LineRenderer>();
        }

        ConfigurePourLine();
    }

    private void ConfigurePourLine()
    {
        if (pourLine == null)
            return;

        pourLine.useWorldSpace = true;
        pourLine.positionCount = Mathf.Max(3, streamSegments);
        pourLine.startWidth = pourLineStartWidth;
        pourLine.endWidth = pourLineEndWidth;
        pourLine.startColor = pourLineColor;
        pourLine.endColor = new Color(pourLineColor.r, pourLineColor.g, pourLineColor.b, pourLineColor.a * 0.45f);
        pourLine.textureMode = LineTextureMode.Stretch;
        pourLine.alignment = LineAlignment.View;
        pourLine.numCapVertices = 2;
        pourLine.numCornerVertices = 2;

        if (pourLine.sharedMaterial == null)
            pourLine.sharedMaterial = GetLineMaterial();
    }

    private Material GetLineMaterial()
    {
        if (runtimeLineMaterial != null)
            return runtimeLineMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            return null;

        runtimeLineMaterial = new Material(shader)
        {
            name = "Runtime Pour Line Material",
            hideFlags = HideFlags.DontSave
        };

        SetMaterialColor(runtimeLineMaterial, pourLineColor);
        SetupTransparentMaterial(runtimeLineMaterial);

        return runtimeLineMaterial;
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private void SetupTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);

        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private void DrawStreamToTarget(Vector3 start, Vector3 end, bool toReceiver)
    {
        IsPouring = true;
        EnsurePourVisual();

        if (pourLine == null)
            return;

        if (!pourLine.enabled)
            pourLine.enabled = true;

        int segments = Mathf.Max(3, streamSegments);
        pourLine.positionCount = segments;

        float distance = Vector3.Distance(start, end);
        float sag = toReceiver
            ? Mathf.Clamp(distance * 0.22f, 0.025f, 0.15f)
            : Mathf.Clamp(distance * 0.14f, 0.015f, 0.08f);

        Vector3 mid = (start + end) * 0.5f + Vector3.down * sag;

        Vector3 streamDirection = end - start;
        Vector3 side = streamDirection.sqrMagnitude > 0.0001f
            ? Vector3.Cross(streamDirection.normalized, Vector3.up)
            : transform.right;

        if (side.sqrMagnitude < 0.001f)
            side = transform.right;

        side.Normalize();

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);

            Vector3 a = Vector3.Lerp(start, mid, t);
            Vector3 b = Vector3.Lerp(mid, end, t);
            Vector3 pos = Vector3.Lerp(a, b, t);

            float wobble = Mathf.Sin((Time.time * streamWobbleSpeed) + (t * 13f)) * streamWobble;
            pos += side * wobble * Mathf.Sin(t * Mathf.PI);

            pourLine.SetPosition(i, pos);
        }
    }

    private void DrawNoReceiverStream(Vector3 start, Vector3 velocity)
    {
        IsPouring = true;
        EnsurePourVisual();

        if (pourLine == null)
            return;

        if (!pourLine.enabled)
            pourLine.enabled = true;

        int segments = Mathf.Max(3, streamSegments);
        pourLine.positionCount = segments;

        Vector3 side = Vector3.Cross(velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector3.down, Vector3.up);
        if (side.sqrMagnitude < 0.001f)
            side = transform.right;
        side.Normalize();

        for (int i = 0; i < segments; i++)
        {
            float nt = i / (float)(segments - 1);
            Vector3 pos = GetBallisticPoint(start, velocity, nt);

            float wobble = Mathf.Sin((Time.time * streamWobbleSpeed) + (nt * 14f)) * streamWobble;
            pos += side * wobble * Mathf.Sin(nt * Mathf.PI);

            pourLine.SetPosition(i, pos);
        }
    }

    private void PlayStreamParticles(Vector3 position, Vector3 direction)
    {
        if (streamParticles == null)
            return;

        streamParticles.transform.position = position;

        if (direction.sqrMagnitude > 0.0001f)
            streamParticles.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        if (!streamParticles.isPlaying)
            streamParticles.Play();
    }

    private void EmitSplashAt(Vector3 position, float amountMl)
    {
        if (splashParticles == null || amountMl <= 0f)
            return;

        splashAccumulatorMl += amountMl;

        if (splashAccumulatorMl < splashMlPerBurst)
            return;

        splashAccumulatorMl = 0f;
        splashParticles.transform.position = position;
        splashParticles.transform.rotation = Quaternion.identity;
        splashParticles.Emit(splashParticlesPerBurst);
    }

    private void StopPourVisual()
    {
        IsPouring = false;
        CurrentReceiver = null;

        if (pourLine != null)
            pourLine.enabled = false;

        if (streamParticles != null && streamParticles.isPlaying)
            streamParticles.Stop();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 start = PourPoint != null ? PourPoint.position : transform.position;

        Gizmos.color = new Color(0.25f, 0.65f, 1f, 0.35f);
        Gizmos.DrawWireSphere(start, receiverSearchRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(start, 0.01f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(start, mouthRadius);

        Vector3 velocity = Application.isPlaying
            ? GetNoReceiverStreamVelocity()
            : Vector3.down * streamDownSpeed + transform.forward * streamInitialSpeed;

        Gizmos.color = new Color(0.25f, 0.65f, 1f, 0.85f);
        Vector3 previous = start;
        int samples = Mathf.Max(2, receiverPathSamples);
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 next = GetBallisticPoint(start, velocity, t);
            Gizmos.DrawLine(previous, next);
            Gizmos.DrawWireSphere(next, receiverSearchRadius * 0.5f);
            previous = next;
        }
    }
}
