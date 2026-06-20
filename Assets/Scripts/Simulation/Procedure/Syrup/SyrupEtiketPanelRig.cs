using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

[DisallowMultipleComponent]
public class SyrupEtiketPanelRig : MonoBehaviour
{
    [Header("Scene Version")]
    [SerializeField] private int sceneVersion = 3;

    [Header("World UI")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private LazyFollow lazyFollow;

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
    [SerializeField] private Button backButton;

    [Header("Keyboard")]
    [SerializeField] private KeyboardManager keyboardManager;

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
        lazyFollow.targetOffset = new Vector3(0f, -0.04f, 1.18f);
        lazyFollow.applyTargetInLocalSpace = true;
        lazyFollow.followInLocalSpace = false;
        lazyFollow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
        lazyFollow.rotationFollowMode = LazyFollow.RotationFollowMode.LookAtWithWorldUp;
        lazyFollow.movementSpeed = 4.5f;
        lazyFollow.movementSpeedVariancePercentage = 0.2f;
        lazyFollow.minDistanceAllowed = 0.08f;
        lazyFollow.maxDistanceAllowed = 0.38f;
        lazyFollow.timeUntilThresholdReachesMaxDistance = 1.25f;
        lazyFollow.snapOnEnable = true;
    }

    public void ShowChoice()
    {
        SetPanelState(true, false, false);
        CloseKeyboard();
    }

    public void ShowForm()
    {
        SetPanelState(false, true, false);
        CloseKeyboard();
    }

    public void ShowSuccess()
    {
        SetPanelState(false, false, true);
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
}
