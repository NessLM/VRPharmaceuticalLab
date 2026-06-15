using TMPro;
using UnityEngine;

public class WorldStepArrow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshPro textMesh;

    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Text")]
    [SerializeField] private string labelText = "\u2193\nISI DI SINI";
    [SerializeField] private Color labelColor = Color.yellow;
    [SerializeField] private float textScale = 0.08f;
    [SerializeField] private float fontSize = 4.8f;

    [Header("Position")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.6f, 0f);
    [SerializeField] private float bobHeight = 0.07f;
    [SerializeField] private float bobSpeed = 3f;

    [Header("State")]
    [SerializeField] private bool startHidden = true;

    private Camera mainCamera;
    private bool isVisible;
    private bool hasExplicitVisibility;
    private bool warnedMissingTextMesh;

    public Transform Target => target;

    private void Awake()
    {
        EnsureTextMesh();
        mainCamera = Camera.main;

        if (!hasExplicitVisibility)
            SetVisible(!startHidden);
    }

    private void OnEnable()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Update()
    {
        if (!isVisible)
            return;

        if (target != null)
        {
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = target.position + worldOffset + Vector3.up * bob;
        }

        Camera cam = mainCamera != null ? mainCamera : Camera.main;

        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        SnapToTarget();
    }

    public void SetLabel(string newLabelText)
    {
        if (!string.IsNullOrWhiteSpace(newLabelText))
            labelText = newLabelText;

        ApplyTextSettings();
    }

    public void SetWorldOffset(Vector3 newWorldOffset)
    {
        worldOffset = newWorldOffset;
    }

    public void Configure(Transform newTarget, string newLabelText, Vector3 newWorldOffset)
    {
        target = newTarget;
        worldOffset = newWorldOffset;
        SetLabel(newLabelText);

        if (target != null)
            SnapToTarget();
    }

    public void SetVisible(bool visible)
    {
        hasExplicitVisibility = true;
        isVisible = visible;

        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);

        if (!visible)
        {
            if (textMesh != null)
                textMesh.gameObject.SetActive(false);

            return;
        }

        EnsureTextMesh();

        if (textMesh != null)
            textMesh.gameObject.SetActive(visible);

        SnapToTarget();
        ApplyTextSettings();
    }

    private void SnapToTarget()
    {
        if (target != null)
            transform.position = target.position + worldOffset;
    }

    private void EnsureTextMesh()
    {
        if (textMesh != null)
            return;

        textMesh = GetComponentInChildren<TextMeshPro>(true);

        if (textMesh == null)
        {
            if (!warnedMissingTextMesh)
            {
                Debug.LogWarning($"[WorldStepArrow] {name} belum punya child TextMeshPro. Fallback runtime dibuat; tambahkan TMP_StepArrowText di scene kalau ingin edit visual text langsung dari hierarchy.", this);
                warnedMissingTextMesh = true;
            }

            GameObject textObject = new GameObject("TMP_StepArrowText");
            textObject.transform.SetParent(transform, false);
            textMesh = textObject.AddComponent<TextMeshPro>();
        }

        ApplyTextSettings();
    }

    private void ApplyTextSettings()
    {
        if (textMesh == null)
            return;

        textMesh.text = labelText;
        textMesh.color = labelColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontSize = fontSize;
        textMesh.fontStyle = FontStyles.Bold;
        textMesh.enableWordWrapping = true;
        textMesh.textWrappingMode = TextWrappingModes.Normal;

        RectTransform rect = textMesh.rectTransform;
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * textScale;
        rect.sizeDelta = new Vector2(9f, 4.5f);
    }
}
