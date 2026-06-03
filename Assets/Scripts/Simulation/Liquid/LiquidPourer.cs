using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(LiquidContainer))]
public class LiquidPourer : MonoBehaviour
{
    [SerializeField] private LiquidContainer sourceContainer;
    [SerializeField] private Transform pourPoint;

    [Header("Pouring")]
    [SerializeField] private float pourAngleThreshold = 75f;
    [SerializeField] private float pourRateMlPerSecond = 25f;
    [SerializeField] private float receiverSearchRadius = 0.05f;
    [SerializeField] private LayerMask receiverLayers = ~0;

    [Header("Visual")]
    [SerializeField] private Transform pourVisualRoot;
    [SerializeField] private LineRenderer pourLine;
    [SerializeField] private Color pourLineColor = new Color(0.35f, 0.75f, 1f, 0.7f);
    [SerializeField] private float pourLineStartWidth = 0.006f;
    [SerializeField] private float pourLineEndWidth = 0.003f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    public Transform PourPoint => pourPoint != null ? pourPoint : transform;
    public bool IsPouring { get; private set; }
    public LiquidReceiverZone CurrentReceiver { get; private set; }

    private readonly Collider[] receiverHits = new Collider[12];
    private Material runtimeLineMaterial;

    private void Reset()
    {
        sourceContainer = GetComponent<LiquidContainer>();
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
        pourAngleThreshold = Mathf.Clamp(pourAngleThreshold, 0f, 180f);
        pourRateMlPerSecond = Mathf.Max(0f, pourRateMlPerSecond);
        receiverSearchRadius = Mathf.Max(0.001f, receiverSearchRadius);
        pourLineStartWidth = Mathf.Max(0.0001f, pourLineStartWidth);
        pourLineEndWidth = Mathf.Max(0.0001f, pourLineEndWidth);
    }

    private void Update()
    {
        if (!CanPour())
        {
            StopPourVisual();
            return;
        }

        LiquidReceiverZone receiver = FindReceiver();
        if (receiver == null)
        {
            StopPourVisual();
            return;
        }

        float amountMl = pourRateMlPerSecond * Time.deltaTime;
        float transferredMl = receiver.ReceiveFrom(sourceContainer, amountMl);
        if (transferredMl <= 0f)
        {
            StopPourVisual();
            return;
        }

        CurrentReceiver = receiver;
        UpdatePourVisual(receiver);

        if (debugLogs && transferredMl > 0f)
            Debug.Log($"{name} transferred {transferredMl:0.###} ml", this);
    }

    private bool CanPour()
    {
        return sourceContainer != null
            && !sourceContainer.IsEmpty
            && sourceContainer.CurrentLiquid != null
            && GetTiltAngle() >= pourAngleThreshold;
    }

    private LiquidReceiverZone FindReceiver()
    {
        Transform point = PourPoint;
        int hitCount = Physics.OverlapSphereNonAlloc(
            point.position,
            receiverSearchRadius,
            receiverHits,
            receiverLayers,
            QueryTriggerInteraction.Collide
        );

        LiquidReceiverZone bestReceiver = null;
        float bestDistanceSq = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = receiverHits[i];
            if (hit == null)
                continue;

            LiquidReceiverZone receiver = hit.GetComponentInParent<LiquidReceiverZone>();
            if (receiver == null || !receiver.CanReceiveFrom(sourceContainer))
                continue;

            float distanceSq = (receiver.transform.position - point.position).sqrMagnitude;
            if (distanceSq < bestDistanceSq)
            {
                bestDistanceSq = distanceSq;
                bestReceiver = receiver;
            }
        }

        return bestReceiver;
    }

    private float GetTiltAngle()
    {
        if (sourceContainer == null)
            return 0f;

        Vector3 containerUp = sourceContainer.transform.TransformDirection(sourceContainer.FillAxisLocal);
        return Vector3.Angle(containerUp, Vector3.up);
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
            visualObject.transform.SetParent(PourPoint, false);
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
        pourLine.positionCount = 2;
        pourLine.startWidth = pourLineStartWidth;
        pourLine.endWidth = pourLineEndWidth;
        pourLine.startColor = pourLineColor;
        pourLine.endColor = new Color(pourLineColor.r, pourLineColor.g, pourLineColor.b, pourLineColor.a * 0.45f);
        pourLine.textureMode = LineTextureMode.Stretch;
        pourLine.alignment = LineAlignment.View;
        pourLine.numCapVertices = 2;
        pourLine.numCornerVertices = 1;

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

        if (runtimeLineMaterial.HasProperty("_BaseColor"))
            runtimeLineMaterial.SetColor("_BaseColor", pourLineColor);
        if (runtimeLineMaterial.HasProperty("_Color"))
            runtimeLineMaterial.SetColor("_Color", pourLineColor);
        if (runtimeLineMaterial.HasProperty("_Surface"))
            runtimeLineMaterial.SetFloat("_Surface", 1f);
        if (runtimeLineMaterial.HasProperty("_Blend"))
            runtimeLineMaterial.SetFloat("_Blend", 0f);
        if (runtimeLineMaterial.HasProperty("_SrcBlend"))
            runtimeLineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (runtimeLineMaterial.HasProperty("_DstBlend"))
            runtimeLineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (runtimeLineMaterial.HasProperty("_ZWrite"))
            runtimeLineMaterial.SetInt("_ZWrite", 0);

        runtimeLineMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        runtimeLineMaterial.renderQueue = (int)RenderQueue.Transparent;

        return runtimeLineMaterial;
    }

    private void UpdatePourVisual(LiquidReceiverZone receiver)
    {
        IsPouring = true;
        EnsurePourVisual();

        if (pourLine == null)
            return;

        if (!pourLine.enabled)
            pourLine.enabled = true;

        Transform point = PourPoint;
        pourLine.SetPosition(0, point.position);
        pourLine.SetPosition(1, receiver.transform.position);
    }

    private void StopPourVisual()
    {
        IsPouring = false;
        CurrentReceiver = null;

        if (pourLine != null)
            pourLine.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Transform point = PourPoint;
        Gizmos.color = new Color(0.25f, 0.65f, 1f, 0.35f);
        Gizmos.DrawWireSphere(point.position, receiverSearchRadius);
    }
}
