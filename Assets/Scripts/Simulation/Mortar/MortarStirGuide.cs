using UnityEngine;

[DisallowMultipleComponent]
public class MortarStirGuide : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.075f, 0f);

    [Header("Circle")]
    [SerializeField] private float radius = 0.07f;
    [SerializeField] private int segments = 48;
    [SerializeField] private float lineWidth = 0.004f;
    [SerializeField] private Color lineColor = new Color(0.55f, 1f, 1f, 0.9f);

    [Header("Moving Indicator")]
    [SerializeField] private Transform movingIndicator;
    [SerializeField] private float spinSpeedDegrees = 150f;
    [SerializeField] private Vector3 indicatorScale = new Vector3(0.018f, 0.004f, 0.011f);

    [Header("Runtime")]
    [SerializeField] private bool visible;
    [SerializeField] private bool detachFromScaledParentOnPlay = true;

    private LineRenderer lineRenderer;
    private Material runtimeMaterial;
    private float angle;

    private void Awake()
    {
        if (Application.isPlaying && detachFromScaledParentOnPlay && transform.parent != null)
        {
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;
        }

        EnsureLineRenderer();
        EnsureIndicator();
        SetVisible(visible);
    }

    private void OnEnable()
    {
        EnsureLineRenderer();
        EnsureIndicator();
        SetVisible(visible);
    }

    private void Update()
    {
        if (!visible)
            return;

        UpdateGuide();
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetVisible(bool value)
    {
        visible = value;

        EnsureLineRenderer();
        EnsureIndicator();

        if (lineRenderer != null)
            lineRenderer.enabled = visible;

        if (movingIndicator != null)
            movingIndicator.gameObject.SetActive(visible);
    }

    private void UpdateGuide()
    {
        Vector3 center = target != null
            ? target.position + worldOffset
            : transform.position + worldOffset;

        transform.position = center;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        BuildWorldCircle(center);

        angle += spinSpeedDegrees * Time.deltaTime;
        float rad = angle * Mathf.Deg2Rad;

        if (movingIndicator != null)
        {
            Vector3 pos = center + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
            Vector3 tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;

            movingIndicator.position = pos;
            movingIndicator.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            movingIndicator.localScale = indicatorScale;
        }
    }

    private void EnsureLineRenderer()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = true;
        lineRenderer.positionCount = Mathf.Max(8, segments);
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.numCornerVertices = 3;
        lineRenderer.numCapVertices = 3;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.enabled = visible;

        if (runtimeMaterial == null)
            runtimeMaterial = CreateGuideMaterial();

        if (runtimeMaterial != null)
            lineRenderer.sharedMaterial = runtimeMaterial;
    }

    private void EnsureIndicator()
    {
        if (movingIndicator != null)
            return;

        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        indicator.name = "Runtime_StirDirectionIndicator";
        indicator.transform.SetParent(transform, false);
        indicator.transform.localScale = indicatorScale;

        Collider col = indicator.GetComponent<Collider>();
        if (col != null)
            Destroy(col);

        Renderer renderer = indicator.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = lineRenderer != null ? lineRenderer.sharedMaterial : CreateGuideMaterial();

        movingIndicator = indicator.transform;
    }

    private void BuildWorldCircle(Vector3 center)
    {
        if (lineRenderer == null)
            return;

        int count = Mathf.Max(8, segments);
        lineRenderer.positionCount = count;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float rad = t * Mathf.PI * 2f;

            Vector3 pos = center + new Vector3(
                Mathf.Cos(rad) * radius,
                0f,
                Mathf.Sin(rad) * radius
            );

            lineRenderer.SetPosition(i, pos);
        }
    }

    private Material CreateGuideMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            return null;

        Material mat = new Material(shader);
        mat.name = "Runtime_MortarStirGuide_Material";

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", lineColor);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", lineColor);

        return mat;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
        segments = Mathf.Max(8, segments);
        lineWidth = Mathf.Max(0.001f, lineWidth);
    }
#endif
}