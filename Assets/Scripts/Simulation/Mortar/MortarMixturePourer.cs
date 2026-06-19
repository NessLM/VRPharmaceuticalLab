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

    private void EnsureStreamVisual()
    {
        if (streamLine == null)
        {
            GameObject visual = new GameObject("MortarPourStream");
            visual.transform.SetParent(transform, false);
            streamLine = visual.AddComponent<LineRenderer>();
        }

        streamLine.useWorldSpace = true;
        streamLine.positionCount = Mathf.Max(3, streamSegments);
        streamLine.startWidth = streamStartWidth;
        streamLine.endWidth = streamEndWidth;
        streamLine.startColor = streamColor;
        streamLine.endColor = new Color(streamColor.r, streamColor.g, streamColor.b, streamColor.a * 0.4f);
        streamLine.numCapVertices = 2;

        if (streamMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                streamMaterial = new Material(shader);
                streamMaterial.name = "Runtime_MortarMixtureStream";
                streamMaterial.hideFlags = HideFlags.DontSave;

                if (streamMaterial.HasProperty("_BaseColor"))
                    streamMaterial.SetColor("_BaseColor", streamColor);

                if (streamMaterial.HasProperty("_Color"))
                    streamMaterial.SetColor("_Color", streamColor);

                if (streamMaterial.HasProperty("_Surface"))
                    streamMaterial.SetFloat("_Surface", 1f);

                streamMaterial.renderQueue = 3000;
            }
        }

        if (streamMaterial != null)
            streamLine.sharedMaterial = streamMaterial;
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
