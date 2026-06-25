using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

[DisallowMultipleComponent]
public class EtiketWorkflow : MonoBehaviour
{
    [Header("Scene UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private GameObject formPanel;
    [SerializeField] private GameObject successPanel;
    [SerializeField] private GameObject keyboardRoot;
    [SerializeField] private Button whiteEtiketButton;
    [SerializeField] private Button blueEtiketButton;
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
    [SerializeField] private TMP_Text successTitle;
    [SerializeField] private TMP_Text successDetail;
    [SerializeField] private Button backButton;
    [SerializeField] private KeyboardManager keyboardManager;

    [Header("World Etiket")]
    [SerializeField] private Transform labelSpawnAnchor;
    [SerializeField] private Transform labelSnapAnchor;
    [SerializeField] private BottleLabelSnapTarget bottleSnapTarget;
    [SerializeField] private BottleLid requiredBottleLid;
    [SerializeField] private float attachDistance = 0.14f;
    [SerializeField] private Vector2 labelSizeMeters = new Vector2(0.105f, 0.063f);
    [SerializeField] private Vector3 labelSpawnOffset = new Vector3(-0.08f, 0.08f, -0.12f);
    [SerializeField] private float spawnDistanceInFrontOfBottle = 0.18f;
    [SerializeField] private float spawnHeightAboveBottleCenter = 0.035f;
    [SerializeField] private Vector3 grabColliderSize = new Vector3(0.15f, 0.1f, 0.035f);

    [Header("Editable Etiket Content")]
    [SerializeField] private string productLine = "DIFENHIDRAMIN 250 mg / 100 ml";
    [SerializeField] private string completionTitle = "SIMULASI SELESAI";
    [TextArea(2, 4)]
    [SerializeField] private string completionDetail =
        "Obat sudah selesai dibuat, dikemas, diberi etiket, dan ditutup.";

    public event Action<GameObject> LabelCreated;
    public event Action LabelAttached;
    public event Action BottleNotClosed;
    public event Action BackRequested;

    private Transform bottle;
    private Renderer bottleRenderer;
    private GameObject labelObject;
    private XRGrabInteractable labelGrab;
    private Rigidbody labelRigidbody;
    private Collider[] labelColliders;
    private UnityEngine.UI.Outline labelOutline;
    private bool whiteEtiket = true;
    private bool labelWasGrabbed;
    private bool labelIsAttached;
    private bool eventsBound;

    private static readonly Color WhiteEtiket = new Color(0.97f, 0.98f, 0.96f, 1f);
    private static readonly Color BlueEtiket = new Color(0.16f, 0.66f, 0.86f, 1f);
    private static readonly Color DarkInk = new Color(0.025f, 0.035f, 0.05f, 1f);
    private static readonly Color HighlightYellow = new Color(1f, 0.92f, 0.02f, 1f);

    private bool IsUiConfigured =>
        panelRoot != null &&
        choicePanel != null &&
        formPanel != null &&
        successPanel != null &&
        whiteEtiketButton != null &&
        blueEtiketButton != null &&
        createLabelButton != null;

    public void Initialize(RectTransform unusedCanvasRoot, Transform bottleTarget)
    {
        bottle = bottleTarget;
        bottleRenderer = bottle != null ? bottle.GetComponentInChildren<Renderer>(true) : null;
        ResolveWorldAnchors();
        BindPanelEvents();
    }

    public void ConfigureContent(string newProductLine, string newCompletionDetail)
    {
        if (!string.IsNullOrWhiteSpace(newProductLine))
            productLine = newProductLine.Trim();

        if (!string.IsNullOrWhiteSpace(newCompletionDetail))
            completionDetail = newCompletionDetail.Trim();
    }

    public void BeginLabelSelection(RectTransform unusedCanvasRoot, Transform bottleTarget)
    {
        Initialize(unusedCanvasRoot, bottleTarget);
        DestroyCurrentLabel();

        whiteEtiket = true;
        labelWasGrabbed = false;
        labelIsAttached = false;

        if (!IsUiConfigured)
        {
            Debug.LogError("[Etiket] Referensi UI Etiket di VRLabSimulation belum lengkap.", this);
            return;
        }

        if (numberInput != null)
            numberInput.text = "001";

        if (nameInput != null)
            nameInput.text = string.Empty;

        if (usageInput != null)
            usageInput.text = string.Empty;

        if (dateInput != null)
            dateInput.text = DateTime.Now.ToString("dd-MM-yyyy");

        SetStatus("Arahkan ray controller ke salah satu etiket.", new Color(0.78f, 0.86f, 0.94f, 1f));
        RefreshFormPreview();
        ShowChoice();
    }

    public void ShowSuccess()
    {
        if (successTitle != null)
            successTitle.text = completionTitle;

        if (successDetail != null)
            successDetail.text = completionDetail;

        SetPanelState(false, false, true);
    }

    private void Update()
    {
        if (labelObject == null || labelIsAttached || !labelWasGrabbed || bottle == null)
            return;

        if (labelGrab != null && labelGrab.isSelected)
            return;

        TryAttachLabel();
    }

    private void BindPanelEvents()
    {
        if (eventsBound || !IsUiConfigured)
            return;

        whiteEtiketButton.onClick.AddListener(() => SelectEtiketColor(true));
        blueEtiketButton.onClick.AddListener(() => SelectEtiketColor(false));

        if (chooseAgainButton != null)
            chooseAgainButton.onClick.AddListener(ShowChoice);

        createLabelButton.onClick.AddListener(CreateWorldLabel);

        if (backButton != null)
            backButton.onClick.AddListener(() => BackRequested?.Invoke());

        BindInput(numberInput);
        BindInput(nameInput);
        BindInput(usageInput);
        BindInput(dateInput);
        eventsBound = true;
    }

    private void BindInput(TMP_InputField input)
    {
        if (input == null)
            return;

        input.onSelect.AddListener(_ => OpenKeyboard(input));
        input.onValueChanged.AddListener(_ => RefreshFormPreview());
    }

    private void SelectEtiketColor(bool useWhite)
    {
        whiteEtiket = useWhite;

        if (formTitle != null)
        {
            formTitle.text = useWhite
                ? "Isi Etiket Putih - Obat Dalam"
                : "Isi Etiket Biru - Obat Luar";
            formTitle.color = useWhite ? Color.white : BlueEtiket;
        }

        SetStatus("Pilih kolom dengan ray controller untuk membuka keyboard VR.", new Color(0.78f, 0.86f, 0.94f, 1f));
        RefreshFormPreview();
        SetPanelState(false, true, false);
    }

    private void RefreshFormPreview()
    {
        if (previewCard != null)
            previewCard.color = whiteEtiket ? WhiteEtiket : BlueEtiket;

        if (previewHeader != null)
            previewHeader.text = whiteEtiket
                ? "ETIKET OBAT - OBAT DALAM"
                : "ETIKET OBAT - OBAT LUAR";

        if (previewBody != null)
        {
            previewBody.text =
                $"No: {SafeText(numberInput, "001")}      Tgl: {SafeText(dateInput, "-")}\n" +
                $"Nama: {SafeText(nameInput, "-")}\n" +
                $"Untuk: {SafeText(usageInput, "-")}";
        }
    }

    private void CreateWorldLabel()
    {
        if (string.IsNullOrWhiteSpace(nameInput != null ? nameInput.text : null) ||
            string.IsNullOrWhiteSpace(usageInput != null ? usageInput.text : null))
        {
            SetStatus("Nama dan kegunaan/aturan pakai harus diisi terlebih dahulu.", new Color(1f, 0.42f, 0.32f, 1f));
            return;
        }

        DestroyCurrentLabel();

        labelObject = new GameObject("Etiket_Grabbable");
        Vector3 spawnPosition = GetLabelSpawnPosition();
        labelObject.transform.SetPositionAndRotation(
            spawnPosition,
            GetFacingRotation(spawnPosition));
        labelObject.transform.localScale = Vector3.one;

        BoxCollider physicalCollider = labelObject.AddComponent<BoxCollider>();
        physicalCollider.size = new Vector3(labelSizeMeters.x, labelSizeMeters.y, 0.004f);

        GameObject helperObject = new GameObject("GrabCollider");
        helperObject.transform.SetParent(labelObject.transform, false);
        BoxCollider helperCollider = helperObject.AddComponent<BoxCollider>();
        helperCollider.isTrigger = true;
        helperCollider.size = grabColliderSize;
        labelColliders = new Collider[] { physicalCollider, helperCollider };

        labelRigidbody = labelObject.AddComponent<Rigidbody>();
        labelRigidbody.mass = 0.025f;
        labelRigidbody.useGravity = false;
        labelRigidbody.isKinematic = true;
        labelRigidbody.linearDamping = 8f;
        labelRigidbody.angularDamping = 8f;
        labelRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        labelRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        labelGrab = labelObject.AddComponent<XRGrabInteractable>();
        labelGrab.selectMode = InteractableSelectMode.Single;
        labelGrab.movementType = XRBaseInteractable.MovementType.Kinematic;
        labelGrab.useDynamicAttach = false;
        labelGrab.matchAttachPosition = true;
        labelGrab.matchAttachRotation = true;
        labelGrab.snapToColliderVolume = true;
        labelGrab.trackPosition = true;
        labelGrab.trackRotation = true;
        labelGrab.trackScale = false;
        labelGrab.throwOnDetach = false;
        labelGrab.forceGravityOnDetach = false;
        labelGrab.retainTransformParent = false;
        labelGrab.addDefaultGrabTransformers = false;
        labelGrab.colliders.Clear();
        labelGrab.colliders.Add(physicalCollider);
        labelGrab.colliders.Add(helperCollider);

        Transform grabPoint = CreateGrabPointHandle(labelObject.transform);
        labelGrab.attachTransform = grabPoint;

        XRGeneralGrabTransformer transformer = labelObject.AddComponent<XRGeneralGrabTransformer>();
        transformer.allowOneHandedScaling = false;
        transformer.allowTwoHandedScaling = false;
        labelGrab.AddSingleGrabTransformer(transformer);

        labelGrab.selectEntered.AddListener(_ =>
        {
            labelWasGrabbed = true;
            if (labelOutline != null)
                labelOutline.enabled = false;
        });
        labelGrab.selectExited.AddListener(_ => TryAttachLabel());

        CreateLabelCardVisual();

        labelWasGrabbed = false;
        labelIsAttached = false;
        HideAllPanels();
        LabelCreated?.Invoke(labelObject);
    }

    private void CreateLabelCardVisual()
    {
        GameObject canvasObject = new GameObject("EtiketCardCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(labelObject.transform, false);
        canvasObject.transform.localPosition = Vector3.zero;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * (labelSizeMeters.x / 1000f);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1000f, 600f);

        Image border = CreateSizedImage(
            "IMG_EtiketBorder_Highlight",
            canvasObject.transform,
            Vector2.zero,
            new Vector2(1000f, 600f),
            Color.black);

        labelOutline = border.gameObject.AddComponent<UnityEngine.UI.Outline>();
        labelOutline.effectColor = HighlightYellow;
        labelOutline.effectDistance = new Vector2(12f, -12f);
        labelOutline.useGraphicAlpha = false;

        Image card = CreateSizedImage(
            whiteEtiket ? "IMG_EtiketPutih" : "IMG_EtiketBiru",
            border.transform,
            Vector2.zero,
            new Vector2(966f, 566f),
            whiteEtiket ? WhiteEtiket : BlueEtiket);

        CreateSizedImage("LINE_EtiketHeader", card.transform, new Vector2(0f, 126f), new Vector2(910f, 8f), Color.black);
        CreateSizedImage("LINE_EtiketFooter", card.transform, new Vector2(0f, -190f), new Vector2(910f, 6f), Color.black);

        string category = whiteEtiket ? "OBAT DALAM" : "OBAT LUAR";
        TMP_Text header = CreateText(
            "TXT_EtiketHeader",
            card.transform,
            $"ETIKET OBAT - {category}",
            46f,
            FontStyles.Bold,
            new Vector2(0f, 205f),
            new Vector2(900f, 72f),
            DarkInk);
        header.alignment = TextAlignmentOptions.Center;

        TMP_Text details = CreateText(
            "TXT_EtiketDetail",
            card.transform,
            $"No: {SafeText(numberInput, "001")}                 Tgl: {SafeText(dateInput, DateTime.Now.ToString("dd-MM-yyyy"))}\n" +
            $"Nama: {SafeText(nameInput, "-")}\n" +
            $"Untuk: {SafeText(usageInput, "-")}",
            38f,
            FontStyles.Normal,
            new Vector2(0f, -20f),
            new Vector2(880f, 260f),
            DarkInk);
        details.alignment = TextAlignmentOptions.MidlineLeft;
        details.margin = new Vector4(24f, 8f, 24f, 8f);

        CreateText(
            "TXT_EtiketFooter",
            card.transform,
            $"<b>{productLine}</b>",
            32f,
            FontStyles.Bold,
            new Vector2(0f, -240f),
            new Vector2(900f, 54f),
            DarkInk);
    }

    private void AttachLabelToBottle(Vector3 position, Quaternion rotation)
    {
        if (bottleSnapTarget != null)
        {
            if (!bottleSnapTarget.TrySnapLabel(
                    labelObject.transform,
                    labelGrab,
                    labelRigidbody,
                    labelColliders))
            {
                return;
            }
        }
        else
        {
            if (requiredBottleLid != null && requiredBottleLid.IsOpen)
            {
                BottleNotClosed?.Invoke();
                return;
            }

            // Parent ke bottle (atau labelSnapAnchor yang merupakan child bottle)
            // dengan worldPositionStays=true, lalu snap ke local pose yang benar.
            Transform parent = labelSnapAnchor != null ? labelSnapAnchor : bottle;
            labelObject.transform.SetParent(parent, true);
            labelObject.transform.SetPositionAndRotation(position, rotation);

            if (labelRigidbody != null)
            {
                labelRigidbody.linearVelocity = Vector3.zero;
                labelRigidbody.angularVelocity = Vector3.zero;
                labelRigidbody.isKinematic = true;
                labelRigidbody.useGravity = false;
                labelRigidbody.detectCollisions = false;
            }

            if (labelGrab != null)
                labelGrab.enabled = false;

            if (labelColliders != null)
            {
                foreach (Collider labelCollider in labelColliders)
                {
                    if (labelCollider != null)
                        labelCollider.enabled = false;
                }
            }
        }

        labelIsAttached = true;

        if (labelOutline != null)
            labelOutline.enabled = false;

        LabelAttached?.Invoke();
    }

    private void TryAttachLabel()
    {
        if (labelObject == null || labelIsAttached || !labelWasGrabbed || bottle == null)
            return;

        if (labelGrab != null && labelGrab.isSelected)
            return;

        if (bottleSnapTarget != null)
        {
            if (!bottleSnapTarget.IsLabelInsideSnapArea(labelObject.transform))
                return;

            if (!bottleSnapTarget.IsBottleClosed)
            {
                bottleSnapTarget.NotifyBottleNotClosed();
                BottleNotClosed?.Invoke();
                return;
            }
        }

        Vector3 target = GetBottleSnapPosition(out Quaternion rotation);
        if (bottleSnapTarget == null &&
            Vector3.Distance(labelObject.transform.position, target) > attachDistance)
        {
            return;
        }

        AttachLabelToBottle(target, rotation);
    }

    private Vector3 GetLabelSpawnPosition()
    {
        Vector3 center = bottleRenderer != null
            ? bottleRenderer.bounds.center
            : bottle != null ? bottle.position : transform.position;

        if (Camera.main == null)
        {
            if (labelSpawnAnchor != null)
                return labelSpawnAnchor.position;

            return center + labelSpawnOffset;
        }

        Vector3 towardViewer =
            Vector3.ProjectOnPlane(Camera.main.transform.position - center, Vector3.up);
        if (towardViewer.sqrMagnitude < 0.001f)
            towardViewer = -Camera.main.transform.forward;

        towardViewer.Normalize();
        return center +
               towardViewer * spawnDistanceInFrontOfBottle +
               Vector3.up * spawnHeightAboveBottleCenter;
    }

    private Vector3 GetBottleSnapPosition(out Quaternion rotation)
    {
        if (labelSnapAnchor != null)
        {
            rotation = labelSnapAnchor.rotation;
            return labelSnapAnchor.position;
        }

        Vector3 center = bottleRenderer != null ? bottleRenderer.bounds.center : bottle.position;
        Vector3 towardViewer = Camera.main != null
            ? Vector3.ProjectOnPlane(Camera.main.transform.position - center, Vector3.up)
            : -bottle.forward;

        if (towardViewer.sqrMagnitude < 0.001f)
            towardViewer = -bottle.forward;

        towardViewer.Normalize();

        float radius = 0.046f;
        if (bottleRenderer != null)
            radius = Mathf.Max(0.03f, Mathf.Max(bottleRenderer.bounds.extents.x, bottleRenderer.bounds.extents.z));

        // +margin agar kartu duduk PERSIS DI LUAR permukaan (rata di depan), tidak menekuk
        // masuk ke dalam pot/salep.
        Vector3 position = center + towardViewer * (radius + 0.004f);
        // Kartu etiket menghadap KELUAR ke arah pengguna (rata di depan pot), bukan
        // menekuk masuk ke dalam salep. Forward (+Z, muka kartu) = arah ke viewer.
        rotation = Quaternion.LookRotation(towardViewer, Vector3.up);
        return position;
    }

    private void ResolveWorldAnchors()
    {
        if (bottle == null)
            return;

        if (labelSpawnAnchor == null)
            labelSpawnAnchor = FindDeepChild(bottle, "EtiketSpawnAnchor");

        if (labelSnapAnchor == null)
            labelSnapAnchor = FindDeepChild(bottle, "BottleLabelAnchor");

        if (labelSnapAnchor == null)
            labelSnapAnchor = FindDeepChild(bottle, "EtiketSnapAnchor");

        if (bottleSnapTarget == null)
            bottleSnapTarget = bottle.GetComponentInChildren<BottleLabelSnapTarget>(true);

        if (requiredBottleLid == null)
            requiredBottleLid = bottle.GetComponentInChildren<BottleLid>(true);
    }

    private Quaternion GetFacingRotation(Vector3 worldPosition)
    {
        if (Camera.main == null)
            return Quaternion.identity;

        Vector3 direction = Camera.main.transform.position - worldPosition;
        // Muka kartu (+Z) menghadap kamera supaya teks terbaca lurus di depan.
        return direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : Quaternion.identity;
    }

    private void ShowChoice()
    {
        SetPanelState(true, false, false);
    }

    private void SetPanelState(bool showChoice, bool showForm, bool showSuccess)
    {
        if (panelRoot != null)
            panelRoot.SetActive(showChoice || showForm || showSuccess);

        if (choicePanel != null)
            choicePanel.SetActive(showChoice);

        if (formPanel != null)
            formPanel.SetActive(showForm);

        if (successPanel != null)
            successPanel.SetActive(showSuccess);

        CloseKeyboard();
    }

    private void HideAllPanels()
    {
        SetPanelState(false, false, false);
    }

    private void OpenKeyboard(TMP_InputField input)
    {
        if (input == null)
            return;

        if (keyboardManager != null)
            keyboardManager.OpenKeybord(input);
        else if (keyboardRoot != null)
            keyboardRoot.SetActive(true);
    }

    private void CloseKeyboard()
    {
        if (keyboardManager != null)
            keyboardManager.Done();
        else if (keyboardRoot != null)
            keyboardRoot.SetActive(false);
    }

    private void SetStatus(string message, Color color)
    {
        if (formStatus == null)
            return;

        formStatus.text = message ?? string.Empty;
        formStatus.color = color;
    }

    private void DestroyCurrentLabel()
    {
        if (labelObject != null)
        {
            labelObject.SetActive(false);
            Destroy(labelObject);
        }

        labelObject = null;
        labelGrab = null;
        labelRigidbody = null;
        labelColliders = null;
        labelOutline = null;
    }

    public void ResetWorkflow()
    {
        DestroyCurrentLabel();
        whiteEtiket = true;
        labelWasGrabbed = false;
        labelIsAttached = false;
        HideAllPanels();
    }

    private static Transform CreateGrabPointHandle(Transform parent)
    {
        GameObject handle = new GameObject("GrabPoint_Handle");
        handle.transform.SetParent(parent, false);
        handle.transform.localPosition = Vector3.zero;
        handle.transform.localRotation = Quaternion.identity;
        handle.transform.localScale = Vector3.one;
        return handle.transform;
    }

    private static Transform FindDeepChild(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate != null &&
                string.Equals(candidate.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Image CreateSizedImage(string objectName, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        string value,
        float fontSize,
        FontStyles style,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static string SafeText(TMP_InputField input, string fallback)
    {
        return input != null && !string.IsNullOrWhiteSpace(input.text)
            ? input.text.Trim()
            : fallback;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        attachDistance = Mathf.Clamp(attachDistance, 0.05f, 0.25f);
        labelSizeMeters.x = Mathf.Max(0.03f, labelSizeMeters.x);
        labelSizeMeters.y = Mathf.Max(0.02f, labelSizeMeters.y);
        spawnDistanceInFrontOfBottle = Mathf.Max(0.08f, spawnDistanceInFrontOfBottle);
        spawnHeightAboveBottleCenter = Mathf.Clamp(spawnHeightAboveBottleCenter, -0.1f, 0.25f);
        grabColliderSize = Vector3.Max(grabColliderSize, new Vector3(0.05f, 0.04f, 0.015f));
    }
#endif
}
