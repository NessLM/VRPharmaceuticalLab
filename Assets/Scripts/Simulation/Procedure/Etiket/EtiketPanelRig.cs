using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

[DisallowMultipleComponent]
public class EtiketPanelRig : MonoBehaviour
{
    [Header("Scene Version")]
    [SerializeField] private int sceneVersion = 5;

    [Header("World UI")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private LazyFollow lazyFollow;
    [SerializeField] private float followHorizontalOffset = -0.12f;
    [SerializeField] private float followVerticalOffset = -0.1f;
    [SerializeField] private float followDistance = 0.4f;
    [SerializeField] private float panelWorldScale = 0.0009f;

    [Header("Panels")]
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private GameObject formPanel;
    [SerializeField] private GameObject successPanel;
    [SerializeField] private GameObject keyboardRoot;

    [Header("Choice")]
    [SerializeField] private Button whiteEtiketButton;
    [SerializeField] private Button blueEtiketButton;

    [Header("Form")]
    [SerializeField] private TMP_Text formTitle;
    [SerializeField] private Image previewCard;
    [SerializeField] private TMP_Text previewHeader;
    [SerializeField] private TMP_Text previewBody;
    [SerializeField] private TMP_Text formStatus;
    [SerializeField] private TMP_InputField numberInput;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField usageInput;
    [SerializeField] private TMP_InputField dateInput;
    [SerializeField] private Button chooseAgainButton;
    [SerializeField] private Button createLabelButton;

    [Header("Success")]
    [SerializeField] private TMP_Text successTitle;
    [SerializeField] private TMP_Text successDetail;
    [SerializeField] private Button backButton;

    [Header("Keyboard")]
    [SerializeField] private KeyboardManager keyboardManager;
    [SerializeField] private Vector3 keyboardLocalPosition = new Vector3(0f, -430f, -28f);
    [SerializeField] private Vector3 keyboardLocalEuler = new Vector3(24f, 0f, 0f);
    [SerializeField] private float keyboardLocalScale = 920f;

    public int SceneVersion => sceneVersion;
    public Canvas WorldCanvas => worldCanvas;
    public GameObject ChoicePanel => choicePanel;
    public GameObject FormPanel => formPanel;
    public GameObject SuccessPanel => successPanel;
    public GameObject KeyboardRoot => keyboardRoot;
    public Button WhiteEtiketButton => whiteEtiketButton;
    public Button BlueEtiketButton => blueEtiketButton;
    public Button ChooseAgainButton => chooseAgainButton;
    public Button CreateLabelButton => createLabelButton;
    public Button BackButton => backButton;
    public TMP_Text SuccessTitle => successTitle;
    public TMP_Text SuccessDetail => successDetail;
    public TMP_Text FormTitle => formTitle;
    public Image PreviewCard => previewCard;
    public TMP_Text PreviewHeader => previewHeader;
    public TMP_Text PreviewBody => previewBody;
    public TMP_Text FormStatus => formStatus;
    public TMP_InputField NumberInput => numberInput;
    public TMP_InputField NameInput => nameInput;
    public TMP_InputField UsageInput => usageInput;
    public TMP_InputField DateInput => dateInput;
    public KeyboardManager KeyboardManager => keyboardManager;

    public bool IsConfigured =>
        worldCanvas != null &&
        choicePanel != null &&
        formPanel != null &&
        successPanel != null &&
        whiteEtiketButton != null &&
        blueEtiketButton != null &&
        createLabelButton != null;

    private void Awake()
    {
        ApplyReadableLayout();

        if (worldCanvas != null && worldCanvas.worldCamera == null)
            worldCanvas.worldCamera = Camera.main;

        ConfigureFollowTarget(Camera.main != null ? Camera.main.transform : null);
    }

    public void ConfigureFollowTarget(Transform target)
    {
        if (lazyFollow == null)
            lazyFollow = GetComponent<LazyFollow>();

        if (lazyFollow == null)
            return;

        lazyFollow.target = target;
        lazyFollow.targetOffset = new Vector3(followHorizontalOffset, followVerticalOffset, followDistance);
        // LazyFollow calculates a world-space target when followInLocalSpace is false.
        // Applying that result as local space under the UI parent throws the panel far away.
        lazyFollow.applyTargetInLocalSpace = false;
        lazyFollow.followInLocalSpace = false;
        lazyFollow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
        lazyFollow.rotationFollowMode = LazyFollow.RotationFollowMode.LookAtWithWorldUp;
        lazyFollow.movementSpeed = 9f;
        lazyFollow.movementSpeedVariancePercentage = 0.15f;
        lazyFollow.minDistanceAllowed = 0.02f;
        lazyFollow.maxDistanceAllowed = 0.1f;
        lazyFollow.timeUntilThresholdReachesMaxDistance = 0.45f;
        lazyFollow.snapOnEnable = true;

        if (isActiveAndEnabled)
            SnapToViewer();
    }

    public void ShowChoice()
    {
        SetPanelState(true, false, false);
        SnapToViewer();
        CloseKeyboard();
    }

    public void ShowForm()
    {
        SetPanelState(false, true, false);
        SnapToViewer();
        CloseKeyboard();
    }

    public void ShowSuccess()
    {
        SetPanelState(false, false, true);
        SnapToViewer();
        CloseKeyboard();
    }

    public void HideAll()
    {
        SetPanelState(false, false, false);
        CloseKeyboard();
    }

    public void OpenKeyboard(TMP_InputField input)
    {
        if (input == null)
            return;

        SnapToViewer();

        if (keyboardManager != null)
            keyboardManager.OpenKeybord(input);
        else if (keyboardRoot != null)
            keyboardRoot.SetActive(true);
    }

    public void CloseKeyboard()
    {
        if (keyboardManager != null)
            keyboardManager.Done();
        else if (keyboardRoot != null)
            keyboardRoot.SetActive(false);
    }

    public void SetStatus(string message, Color color)
    {
        if (formStatus == null)
            return;

        formStatus.text = message ?? string.Empty;
        formStatus.color = color;
    }

    private void SetPanelState(bool showChoice, bool showForm, bool showSuccess)
    {
        gameObject.SetActive(showChoice || showForm || showSuccess);

        if (choicePanel != null)
            choicePanel.SetActive(showChoice);

        if (formPanel != null)
            formPanel.SetActive(showForm);

        if (successPanel != null)
            successPanel.SetActive(showSuccess);
    }

    private void ApplyReadableLayout()
    {
        transform.localScale = Vector3.one * panelWorldScale;

        if (keyboardRoot != null)
        {
            keyboardRoot.transform.localPosition = keyboardLocalPosition;
            keyboardRoot.transform.localRotation = Quaternion.Euler(keyboardLocalEuler);
            keyboardRoot.transform.localScale = Vector3.one * keyboardLocalScale;
        }

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            SetLayerRecursively(transform, uiLayer);

        IncreaseInputReadability(numberInput);
        IncreaseInputReadability(nameInput);
        IncreaseInputReadability(usageInput);
        IncreaseInputReadability(dateInput);
    }

    private void SnapToViewer()
    {
        if (lazyFollow == null || lazyFollow.target == null)
            return;

        Transform viewer = lazyFollow.target;
        Vector3 offset = new Vector3(followHorizontalOffset, followVerticalOffset, followDistance);
        transform.position = viewer.TransformPoint(offset);

        Vector3 forward = transform.position - viewer.position;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (forward.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private static void IncreaseInputReadability(TMP_InputField input)
    {
        if (input == null)
            return;

        if (input.textComponent != null)
            input.textComponent.fontSize = Mathf.Max(26f, input.textComponent.fontSize);

        if (input.placeholder is TMP_Text placeholder)
            placeholder.fontSize = Mathf.Max(24f, placeholder.fontSize);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        followHorizontalOffset = Mathf.Clamp(followHorizontalOffset, -0.35f, 0.35f);
        followDistance = Mathf.Clamp(followDistance, 0.28f, 0.75f);
        followVerticalOffset = Mathf.Clamp(followVerticalOffset, -0.3f, 0.3f);
        panelWorldScale = Mathf.Clamp(panelWorldScale, 0.0006f, 0.0013f);
        keyboardLocalScale = Mathf.Clamp(keyboardLocalScale, 650f, 1200f);

        if (!Application.isPlaying)
            ApplyReadableLayout();
    }
#endif
}
