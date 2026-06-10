using UnityEngine;

public class ProcedureHighlightRing : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private bool followTarget = true;

    [Header("Ring Shape")]
    [SerializeField] private bool autoRadiusFromTarget = true;
    [SerializeField] private float manualRadius = 0.25f;
    [SerializeField] private float radiusMultiplier = 1.35f;
    [SerializeField] private float yOffset = 0.05f;
    [SerializeField] private int segments = 96;

    [Header("Visual")]
    [SerializeField] private float lineWidth = 0.015f;
    [SerializeField] private Color color = Color.yellow;
    [SerializeField] private float pulseAmplitude = 0.08f;
    [SerializeField] private float pulseSpeed = 2.5f;

    private LineRenderer lineRenderer;
    private float baseRadius;

    public void Configure(Transform newTarget, Color newColor, float newRadiusMultiplier, float newYOffset, float newLineWidth)
    {
        target = newTarget;
        color = newColor;
        radiusMultiplier = newRadiusMultiplier;
        yOffset = newYOffset;
        lineWidth = newLineWidth;

        SetupLineRenderer();
        DrawRing();
    }

    private void Awake()
    {
        SetupLineRenderer();
    }

    private void OnEnable()
    {
        SetupLineRenderer();
        DrawRing();
    }

    private void Update()
    {
        if (followTarget)
            DrawRing();
    }

    private void SetupLineRenderer()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        segments = Mathf.Clamp(segments, 16, 256);

        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = true;
        lineRenderer.positionCount = segments;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        if (lineRenderer.sharedMaterial == null)
            lineRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }

    private void DrawRing()
    {
        if (target == null || lineRenderer == null)
            return;

        Vector3 center = GetTargetCenter();
        baseRadius = GetTargetRadius();
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
        float radius = baseRadius * pulse;

        center.y += yOffset;

        for (int i = 0; i < segments; i++)
        {
            float angle = ((float)i / segments) * Mathf.PI * 2f;

            Vector3 point = new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                center.y,
                center.z + Mathf.Sin(angle) * radius
            );

            lineRenderer.SetPosition(i, point);
        }
    }

    private Vector3 GetTargetCenter()
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return target.position;

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds.center;
    }

    private float GetTargetRadius()
    {
        if (!autoRadiusFromTarget)
            return manualRadius;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return manualRadius;

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * radiusMultiplier;

        return Mathf.Max(radius, manualRadius);
    }
}
