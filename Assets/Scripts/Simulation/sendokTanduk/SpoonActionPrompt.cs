using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class SpoonActionPrompt : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HornSpoon hornSpoon;
    [SerializeField] private XRGrabInteractable grabInteractable;

    [Header("Auto Visual")]
    [SerializeField] private bool createPromptAutomatically = true;
    [SerializeField] private Transform promptRoot;
    [SerializeField] private TMP_Text promptText;

    [Header("Prompt Position")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.08f, 0.05f);
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool keepUpright = true;
    [SerializeField] private bool flipText = false;

    [Header("Display")]
    [SerializeField] private bool showOnlyWhenHeld = true;
    [SerializeField] private string scoopText = "Trigger\n<size=65%>Ambil 50 mg</size>";
    [SerializeField] private string fullText = "Penuh\n<size=65%>Tuang ke piring</size>";

    [Header("External Prompt")]
    [SerializeField] private bool externalPromptActive;
    [SerializeField] private string externalPromptText;

    [Header("Text Style")]
    [SerializeField] private float fontSize = 0.16f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color outlineColor = Color.black;
    [Range(0f, 1f)]
    [SerializeField] private float outlineWidth = 0.25f;

    [Header("Debug")]
    [SerializeField] private ScoopBottleTarget currentScoopTarget;

    private Camera targetCamera;

    private void Awake()
    {
        if (hornSpoon == null)
            hornSpoon = GetComponent<HornSpoon>();

        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (createPromptAutomatically)
            EnsurePromptExists();

        ConfigureText();
        RefreshPrompt();
    }

    private void LateUpdate()
    {
        UpdatePromptTransform();
        RefreshPrompt();
    }

    public void SetScoopTarget(ScoopBottleTarget target)
    {
        currentScoopTarget = target;
        RefreshPrompt();
    }

    public void ClearScoopTarget(ScoopBottleTarget target)
    {
        if (currentScoopTarget == target)
            currentScoopTarget = null;

        RefreshPrompt();
    }

    public void RefreshPrompt()
    {
        if (promptRoot == null || promptText == null)
            return;

        bool isHeld = grabInteractable != null && grabInteractable.isSelected;

        if (showOnlyWhenHeld && !isHeld)
        {
            promptRoot.gameObject.SetActive(false);
            return;
        }

        if (externalPromptActive)
        {
            promptText.text = externalPromptText;
            promptRoot.gameObject.SetActive(true);
            return;
        }

        if (hornSpoon != null && hornSpoon.IsFull)
        {
            promptText.text = fullText;
            promptRoot.gameObject.SetActive(true);
            return;
        }

        if (currentScoopTarget != null)
        {
            promptText.text = scoopText;
            promptRoot.gameObject.SetActive(true);
            return;
        }

        promptRoot.gameObject.SetActive(false);
    }

    private void EnsurePromptExists()
    {
        if (promptRoot == null)
        {
            GameObject rootObject = new GameObject("Runtime_SpoonActionPromptRoot");
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.localPosition = localOffset;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;

            promptRoot = rootObject.transform;
        }

        if (promptText == null)
        {
            GameObject textObject = new GameObject("Runtime_TXT_SpoonActionPrompt");
            textObject.transform.SetParent(promptRoot, false);
            textObject.transform.localPosition = Vector3.zero;
            textObject.transform.localRotation = Quaternion.identity;
            textObject.transform.localScale = Vector3.one;

            TextMeshPro textMesh = textObject.AddComponent<TextMeshPro>();
            promptText = textMesh;
        }
    }

    public void SetExternalPrompt(string text)
    {
        externalPromptText = text;
        externalPromptActive = !string.IsNullOrWhiteSpace(text);
        RefreshPrompt();
    }

    public void ClearExternalPrompt()
    {
        externalPromptText = string.Empty;
        externalPromptActive = false;
        RefreshPrompt();
    }

    private void ConfigureText()
    {
        if (promptText == null)
            return;

        promptText.alignment = TextAlignmentOptions.Center;
        promptText.fontStyle = FontStyles.Bold;
        promptText.fontSize = fontSize;
        promptText.color = textColor;
        promptText.enableWordWrapping = false;

        // Biar outline TMP benar-benar aktif di runtime
        Material mat = promptText.fontMaterial;

        if (mat != null)
        {
            mat.EnableKeyword("OUTLINE_ON");

            if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
                mat.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);

            if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);

            promptText.fontMaterial = mat;
            promptText.UpdateMeshPadding();
        }

        promptText.outlineColor = outlineColor;
        promptText.outlineWidth = outlineWidth;
    }

    private void UpdatePromptTransform()
    {
        if (promptRoot == null)
            return;

        promptRoot.localPosition = localOffset;

        if (!faceCamera)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        Vector3 direction = promptRoot.position - targetCamera.transform.position;

        if (keepUpright)
            direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);

        if (flipText)
            targetRotation *= Quaternion.Euler(0f, 180f, 0f);

        promptRoot.rotation = targetRotation;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fontSize = Mathf.Max(0.01f, fontSize);

        if (Application.isPlaying)
        {
            ConfigureText();
            RefreshPrompt();
        }
    }
#endif
}