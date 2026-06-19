using UnityEngine;

[DisallowMultipleComponent]
public class MortarStirGuide : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.12f, 0f);

    [Header("Circle")]
    [SerializeField] private float radius = 0.16f;
    [SerializeField] private int segments = 48;
    [SerializeField] private float lineWidth = 0.012f;
    [SerializeField] private Color lineColor = new Color(0.55f, 1f, 1f, 0.85f);

    [Header("Moving Indicator")]
    [SerializeField] private Transform movingIndicator;
    [SerializeField] private float spinSpeedDegrees = 120f;
    [SerializeField] private Vector3 indicatorScale = new Vector3(0.035f, 0.008f, 0.02f);

    [Header("Runtime")]
    [SerializeField] private bool visible;

    private LineRenderer lineRenderer;
    private float angle;

    private void Awake()
    {
        EnsureLineRenderer();
        EnsureIndicator();
        SetVisible(visible);
    }

    private void Update()
    {
        if (!visible)
            return;

        if (target != null)
            transform.position = target.position + worldOffset;

        BuildCircle();

        angle += spinSpeedDegrees * Time.deltaTime;
        float rad = angle * Mathf.Deg2Rad;

        if (movingIndicator != null)
        {
            Vector3 localPos = new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
            movingIndicator.localPosition = localPos;
            movingIndicator.localRotation = Quaternion.Euler(0f, -angle, 0f);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetVisible(bool value)
    {
        visible = value;

        if (lineRenderer != null)
            lineRenderer.enabled = visible;

        if (movingIndicator != null)
            movingIndicator.gameObject.SetActive(visible);
    }

    private void EnsureLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = Mathf.Max(8, segments);
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.numCornerVertices = 3;
        lineRenderer.numCapVertices = 3;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.name = "Runtime_MortarStirGuide_Material";

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", lineColor);

            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", lineColor);

            lineRenderer.material = mat;
        }

        BuildCircle();
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
        if (renderer != null && lineRenderer != null)
            renderer.sharedMaterial = lineRenderer.material;

        movingIndicator = indicator.transform;
    }

    private void BuildCircle()
    {
        if (lineRenderer == null)
            return;

        int count = Mathf.Max(8, segments);
        lineRenderer.positionCount = count;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float rad = t * Mathf.PI * 2f;
            Vector3 pos = new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
            lineRenderer.SetPosition(i, pos);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        segments = Mathf.Max(8, segments);
        radius = Mathf.Max(0.01f, radius);
        lineWidth = Mathf.Max(0.001f, lineWidth);
    }
#endif
}