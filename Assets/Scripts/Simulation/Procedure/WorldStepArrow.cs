using TMPro;
using UnityEngine;

public class WorldStepArrow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Text")]
    [SerializeField] private string labelText = "↓\nISI AIR DI SINI";
    [SerializeField] private Color labelColor = Color.yellow;
    [SerializeField] private float textScale = 0.08f;

    [Header("Position")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.45f, 0f);
    [SerializeField] private float bobHeight = 0.06f;
    [SerializeField] private float bobSpeed = 3f;

    [Header("State")]
    [SerializeField] private bool startHidden = true;

    private TextMeshPro textMesh;
    private Camera mainCamera;
    private bool isVisible;

    private void Awake()
    {
        EnsureTextMesh();
        mainCamera = Camera.main;

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
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;

        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);

        EnsureTextMesh();

        if (textMesh != null)
        {
            textMesh.gameObject.SetActive(visible);
            textMesh.text = labelText;
            textMesh.color = labelColor;
        }
    }

    private void EnsureTextMesh()
    {
        if (textMesh != null)
            return;

        textMesh = GetComponentInChildren<TextMeshPro>(true);

        if (textMesh == null)
        {
            GameObject textObject = new GameObject("TMP_StepArrowText");
            textObject.transform.SetParent(transform, false);
            textMesh = textObject.AddComponent<TextMeshPro>();
        }

        textMesh.text = labelText;
        textMesh.color = labelColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontSize = 4f;
        textMesh.enableWordWrapping = false;

        RectTransform rect = textMesh.rectTransform;
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * textScale;
        rect.sizeDelta = new Vector2(8f, 4f);
    }
}