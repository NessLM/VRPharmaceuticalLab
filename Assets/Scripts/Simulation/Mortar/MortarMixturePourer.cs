using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class MortarMixturePourer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MortarController mortar;
    [SerializeField] private Transform pourPoint;
    [SerializeField] private LiquidContainer targetContainer;
    [SerializeField] private Transform receiverPoint;
    [SerializeField] private LiquidData mixtureLiquid;
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private MortarWaterVisual waterVisual;

    [Header("Pour Rules")]
    [SerializeField] private bool transferEnabled;
    [SerializeField] private bool requireCompletedMixture = true;
    [SerializeField] private bool requireHeldToPour = true;
    [SerializeField] private float minimumHoldBeforePour = 0.35f;
    [SerializeField] private float minimumTiltAngle = 85f;
    [SerializeField] private float minimumTiltDuration = 0.18f;
    [SerializeField] private float maximumReceiverDistance = 0.42f;
    [SerializeField] private float transferRateMlPerSecond = 38f;
    [SerializeField] private float mortarRimRadiusWorld = 0.105f;
    [SerializeField] private float rimHeightWorld = 0.012f;

    [Header("Stream Visual")]
    [SerializeField] private Color streamColor = new Color(0.96f, 0.97f, 0.92f, 0.72f);
    [SerializeField] private float streamStartWidth = 0.009f;
    [SerializeField] private float streamEndWidth = 0.004f;
    [SerializeField] private int streamSegments = 14;

    private LineRenderer streamLine;
    private Material streamMaterial;
    private float grabbedAt = float.NegativeInfinity;
    private float tiltReadyTimer;

    public bool TransferEnabled => transferEnabled;
    public bool IsPouring { get; private set; }
    public bool IsHeld => grabInteractable != null && grabInteractable.isSelected;
    public float CurrentTiltAngle => Vector3.Angle(GetBowlNormal(), Vector3.up);
    public float RequiredTiltAngle => minimumTiltAngle;

    private void Awake()
    {
        ResolveReferences();
        EnsureStreamVisual();
        StopStream();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }

        tiltReadyTimer = 0f;
        StopStream();
    }

    private void Update()
    {
        if (!CanAttemptPour())
        {
            tiltReadyTimer = 0f;
            StopStream();
            return;
        }

        Vector3 end = receiverPoint != null ? receiverPoint.position : targetContainer.transform.position;
        Vector3 start = GetPourStart(end);

        if (Vector3.Distance(start, end) > maximumReceiverDistance)
        {
            tiltReadyTimer = 0f;
            StopStream();
            return;
        }

        float tilt = Vector3.Angle(GetBowlNormal(), Vector3.up);
        if (tilt < minimumTiltAngle)
        {
            tiltReadyTimer = 0f;
            StopStream();
            return;
        }

        tiltReadyTimer += Time.deltaTime;
        if (tiltReadyTimer < minimumTiltDuration)
        {
            StopStream();
            return;
        }

        float request = Mathf.Min(transferRateMlPerSecond * Time.deltaTime, mortar.CurrentWaterMl);

        if (request <= 0.001f)
        {
            StopStream();
            return;
        }

        float overflow = targetContainer.AddLiquid(request, mixtureLiquid);
        float accepted = Mathf.Max(0f, request - overflow);

        if (accepted <= 0.001f)
        {
            StopStream();
            return;
        }

        mortar.RemoveMixtureMl(accepted);

        // Samakan warna cairan DI DALAM BOTOL dengan warna cairan yang terlihat di Mortar
        // (single source of truth). Tanpa ini, isi botol memakai warna asset LiquidData yang
        // bisa berbeda dari isi mortar. Memakai warna live mortar PENUH (termasuk alpha-nya)
        // agar material isi botol benar-benar sama dengan material isi mortar.
        targetContainer.SetLiquidColorOverride(ResolveBottleLiquidColor());

        DrawStream(start, end);
    }

    public void SetTransferEnabled(bool value)
    {
        transferEnabled = value;

        if (!value)
        {
            tiltReadyTimer = 0f;
            StopStream();
        }
    }

    public void ConfigureTarget(LiquidContainer container, Transform targetPoint)
    {
        targetContainer = container;
        receiverPoint = targetPoint;
    }

    private bool CanAttemptPour()
    {
        if (!transferEnabled || mortar == null || targetContainer == null)
            return false;

        if (mortar.CurrentWaterMl <= 0.001f)
            return false;

        if (requireCompletedMixture && !mortar.IsStep5MixDone)
            return false;

        if (requireHeldToPour)
        {
            if (grabInteractable == null || !grabInteractable.isSelected)
                return false;

            if (Time.time - grabbedAt < minimumHoldBeforePour)
                return false;
        }

        return true;
    }

    private Vector3 GetPourStart(Vector3 receiverPosition)
    {
        Vector3 bowlNormal = GetBowlNormal();
        Vector3 towardReceiver = Vector3.ProjectOnPlane(receiverPosition - transform.position, bowlNormal);

        if (towardReceiver.sqrMagnitude < 0.0001f)
            towardReceiver = transform.right;

        towardReceiver.Normalize();

        Vector3 authoredBase = pourPoint != null ? pourPoint.position : transform.position;
        return authoredBase + towardReceiver * mortarRimRadiusWorld + bowlNormal * rimHeightWorld;
    }

    private Vector3 GetBowlNormal()
    {
        return transform.forward.sqrMagnitude > 0.001f
            ? transform.forward.normalized
            : Vector3.up;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        grabbedAt = Time.time;
        tiltReadyTimer = 0f;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        grabbedAt = float.NegativeInfinity;
        tiltReadyTimer = 0f;
        StopStream();
    }

    private void ResolveReferences()
    {
        if (mortar == null)
            mortar = GetComponent<MortarController>();

        if (waterVisual == null)
            waterVisual = GetComponent<MortarWaterVisual>();

        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (pourPoint == null)
        {
            Transform found = transform.Find("MortarPourPoint");
            pourPoint = found != null ? found : transform;
        }

        if (targetContainer == null)
        {
            GameObject bottle = GameObject.Find("bottle");
            if (bottle != null)
                targetContainer = bottle.GetComponent<LiquidContainer>();
        }

        if (receiverPoint == null && targetContainer != null)
        {
            Transform found = targetContainer.transform.Find("BottleReceiverZone");
            receiverPoint = found != null ? found : targetContainer.transform;
        }
    }

    // Warna stream = warna campuran FINAL di mortar. Diambil dari LiquidData yang SAMA
    // (mixtureLiquid / DifenhidraminMixture) yang juga mengisi botol, jadi warna stream
    // selalu cocok dengan isi mortar & isi botol (bukan putih). Alpha stream tetap dari
    // streamColor agar terlihat jelas saat menuang.
    private Color ResolveStreamColor()
    {
        // Prioritas: warna AIR mortar yang benar-benar terlihat (Runtime_MortarLiquid),
        // supaya aliran tuang ke botol persis sama dengan isi mortar final.
        if (waterVisual != null)
        {
            Color w = waterVisual.CurrentLiquidColor;
            return new Color(w.r, w.g, w.b, streamColor.a);
        }

        if (mixtureLiquid != null)
        {
            Color c = mixtureLiquid.liquidColor;
            return new Color(c.r, c.g, c.b, streamColor.a);
        }

        return streamColor;
    }

    // Warna untuk ISI BOTOL = warna cairan mortar yang BENAR-BENAR terlihat, lengkap dengan
    // alpha-nya, supaya material isi botol sama persis dengan material isi mortar. Berbeda
    // dari ResolveStreamColor() yang memaksa alpha stream agar aliran tampak jelas.
    private Color ResolveBottleLiquidColor()
    {
        if (waterVisual != null)
            return waterVisual.CurrentLiquidColor;

        if (mixtureLiquid != null)
            return mixtureLiquid.liquidColor;

        return streamColor;
    }

    private void EnsureStreamVisual()
    {
        if (streamLine == null)
        {
            GameObject visual = new GameObject("MortarPourStream");
            visual.transform.SetParent(transform, false);
            streamLine = visual.AddComponent<LineRenderer>();
        }

        Color color = ResolveStreamColor();

        streamLine.useWorldSpace = true;
        streamLine.positionCount = Mathf.Max(3, streamSegments);
        streamLine.startWidth = streamStartWidth;
        streamLine.endWidth = streamEndWidth;
        streamLine.startColor = color;
        streamLine.endColor = new Color(color.r, color.g, color.b, color.a * 0.4f);
        streamLine.numCapVertices = 2;

        if (streamMaterial == null)
            streamMaterial = CreateStreamMaterial(color);

        if (streamMaterial != null)
        {
            ApplyMaterialColor(streamMaterial, color);
            streamLine.sharedMaterial = streamMaterial;
        }
    }

    // Material transparan yang benar (meniru MortarWaterVisual.CreateTransparentMaterial).
    // Tanpa setup blend/keyword yang lengkap, LineRenderer bisa jatuh ke material default
    // putih → itulah sebab stream tampak putih sebelumnya.
    private Material CreateStreamMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        Material mat = new Material(shader);
        mat.name = "Runtime_MortarMixtureStream";
        mat.hideFlags = HideFlags.DontSave;

        ApplyMaterialColor(mat, color);

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

    private static void ApplyMaterialColor(Material mat, Color color)
    {
        if (mat == null)
            return;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }

    private void DrawStream(Vector3 start, Vector3 end)
    {
        EnsureStreamVisual();
        IsPouring = true;
        streamLine.enabled = true;

        int count = Mathf.Max(3, streamSegments);
        streamLine.positionCount = count;
        Vector3 middle = (start + end) * 0.5f + Vector3.down * Mathf.Clamp(Vector3.Distance(start, end) * 0.16f, 0.02f, 0.12f);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            Vector3 a = Vector3.Lerp(start, middle, t);
            Vector3 b = Vector3.Lerp(middle, end, t);
            streamLine.SetPosition(i, Vector3.Lerp(a, b, t));
        }
    }

    private void StopStream()
    {
        IsPouring = false;

        if (streamLine != null)
            streamLine.enabled = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        minimumTiltAngle = Mathf.Clamp(minimumTiltAngle, 0f, 180f);
        minimumHoldBeforePour = Mathf.Max(0f, minimumHoldBeforePour);
        minimumTiltDuration = Mathf.Max(0f, minimumTiltDuration);
        maximumReceiverDistance = Mathf.Max(0.05f, maximumReceiverDistance);
        transferRateMlPerSecond = Mathf.Max(1f, transferRateMlPerSecond);
        mortarRimRadiusWorld = Mathf.Max(0.01f, mortarRimRadiusWorld);
        rimHeightWorld = Mathf.Max(0f, rimHeightWorld);
        streamSegments = Mathf.Clamp(streamSegments, 3, 32);
    }
#endif
}
