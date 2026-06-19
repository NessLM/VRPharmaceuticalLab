using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class SyrupEtiketWorkflow : MonoBehaviour
{
    [Header("World Label")]
    [SerializeField] private float attachDistance = 0.22f;
    [SerializeField] private Vector2 labelSizeMeters = new Vector2(0.09f, 0.052f);
    [SerializeField] private Vector3 labelSpawnOffset = new Vector3(0.34f, 0.24f, 0f);

    public event Action<GameObject> LabelCreated;
    public event Action LabelAttached;
    public event Action BackRequested;

    private RectTransform uiRoot;
    private GameObject choicePanel;
    private GameObject formPanel;
    private GameObject keyboardPanel;
    private GameObject successPanel;
    private TMP_InputField numberInput;
    private TMP_InputField nameInput;
    private TMP_InputField usageInput;
    private TMP_InputField dateInput;
    private TMP_InputField activeInput;
    private TMP_Text formTitle;
    private Image formPreviewCard;
    private TMP_Text formPreviewHeader;
    private TMP_Text formPreviewBody;

    private Transform bottle;
    private Renderer bottleRenderer;
    private GameObject labelObject;
    private XRGrabInteractable labelGrab;
    private Rigidbody labelRigidbody;
    private bool whiteEtiket = true;
    private bool labelWasGrabbed;
    private bool labelIsAttached;

    private static readonly Color PanelColor = new Color(0.055f, 0.07f, 0.09f, 0.96f);
    private static readonly Color WhiteEtiket = new Color(0.98f, 0.98f, 0.96f, 1f);
    private static readonly Color BlueEtiket = new Color(0.14f, 0.67f, 0.88f, 1f);
    private static readonly Color Accent = new Color(1f, 0.65f, 0.08f, 1f);

    public void Initialize(RectTransform canvasRoot, Transform bottleTarget)
    {
        bottle = bottleTarget;
        bottleRenderer = bottle != null ? bottle.GetComponentInChildren<Renderer>(true) : null;
        EnsureUI(canvasRoot);
    }

    public void BeginLabelSelection(RectTransform canvasRoot, Transform bottleTarget)
    {
        Initialize(canvasRoot, bottleTarget);
        DestroyCurrentLabel();

        whiteEtiket = true;
        labelWasGrabbed = false;
        labelIsAttached = false;
        activeInput = null;

        if (numberInput != null)
            numberInput.text = "001";

        if (nameInput != null)
            nameInput.text = string.Empty;

        if (usageInput != null)
            usageInput.text = string.Empty;

        if (dateInput != null)
            dateInput.text = DateTime.Now.ToString("dd-MM-yyyy");

        SetPanelState(showChoice: true, showForm: false, showKeyboard: false, showSuccess: false);
    }

    public void ShowSuccess()
    {
        EnsureUI(uiRoot != null ? uiRoot.parent as RectTransform : null);
        SetPanelState(showChoice: false, showForm: false, showKeyboard: false, showSuccess: true);
    }

    private void Update()
    {
        if (labelObject == null || labelIsAttached || !labelWasGrabbed || bottle == null)
            return;

        if (labelGrab != null && labelGrab.isSelected)
            return;

        Vector3 target = GetBottleSnapPosition(out Quaternion rotation);
        if (Vector3.Distance(labelObject.transform.position, target) > attachDistance)
            return;

        AttachLabelToBottle(target, rotation);
    }

    private void EnsureUI(RectTransform canvasRoot)
    {
        if (uiRoot != null)
            return;

        if (canvasRoot == null)
            return;

        EnableCanvasRaycasters(canvasRoot);

        uiRoot = CreateRect("PNL_EtiketWorkflow", canvasRoot);
        uiRoot.anchorMin = new Vector2(1f, 0.5f);
        uiRoot.anchorMax = new Vector2(1f, 0.5f);
        uiRoot.pivot = new Vector2(1f, 0.5f);
        uiRoot.anchoredPosition = new Vector2(-28f, 0f);
        uiRoot.sizeDelta = new Vector2(700f, 660f);

        Image backdrop = uiRoot.gameObject.AddComponent<Image>();
        backdrop.color = new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.93f);
        backdrop.raycastTarget = true;

        BuildChoicePanel();
        BuildFormPanel();
        BuildKeyboardPanel();
        BuildSuccessPanel();

        uiRoot.gameObject.SetActive(false);
    }

    private void BuildChoicePanel()
    {
        choicePanel = CreateRect("PNL_PilihWarnaEtiket", uiRoot).gameObject;

        CreateText(
            "TXT_JudulPilihEtiket",
            choicePanel.transform,
            "Step 7: Pilih Etiket Obat",
            34f,
            FontStyles.Bold,
            new Vector2(0f, 272f),
            new Vector2(640f, 55f),
            Color.white
        );

        CreateText(
            "TXT_KeteranganEtiket",
            choicePanel.transform,
            "Putih = obat dalam / diminum\nBiru = obat luar\nKeduanya tetap dapat dipilih untuk latihan.",
            21f,
            FontStyles.Normal,
            new Vector2(0f, 208f),
            new Vector2(630f, 70f),
            new Color(0.84f, 0.88f, 0.92f, 1f)
        );

        Button whiteButton = CreateEtiketPreviewButton(
            "BTN_EtiketPutih",
            choicePanel.transform,
            "ETIKET PUTIH",
            "OBAT DALAM",
            new Vector2(-165f, 42f),
            new Vector2(290f, 205f),
            WhiteEtiket,
            Color.black
        );
        whiteButton.onClick.AddListener(() => SelectEtiketColor(true));

        Button blueButton = CreateEtiketPreviewButton(
            "BTN_EtiketBiru",
            choicePanel.transform,
            "ETIKET BIRU",
            "OBAT LUAR",
            new Vector2(165f, 42f),
            new Vector2(290f, 205f),
            BlueEtiket,
            new Color(0.02f, 0.08f, 0.12f, 1f)
        );
        blueButton.onClick.AddListener(() => SelectEtiketColor(false));
    }

    private void BuildFormPanel()
    {
        formPanel = CreateRect("PNL_FormEtiket", uiRoot).gameObject;

        formTitle = CreateText(
            "TXT_JudulFormEtiket",
            formPanel.transform,
            "Isi Etiket Putih",
            32f,
            FontStyles.Bold,
            new Vector2(0f, 286f),
            new Vector2(640f, 52f),
            Color.white
        );

        CreateFormPreview();

        numberInput = CreateInput("INP_NoEtiket", formPanel.transform, "No.", new Vector2(0f, 65f));
        nameInput = CreateInput("INP_NamaPasien", formPanel.transform, "Nama", new Vector2(0f, -5f));
        usageInput = CreateInput("INP_AturanPakai", formPanel.transform, "Untuk / aturan pakai", new Vector2(0f, -75f));
        dateInput = CreateInput("INP_TanggalEtiket", formPanel.transform, "Tanggal", new Vector2(0f, -145f));

        numberInput.onValueChanged.AddListener(_ => RefreshFormPreview());
        nameInput.onValueChanged.AddListener(_ => RefreshFormPreview());
        usageInput.onValueChanged.AddListener(_ => RefreshFormPreview());
        dateInput.onValueChanged.AddListener(_ => RefreshFormPreview());

        Button chooseAgain = CreateButton(
            "BTN_PilihUlangEtiket",
            formPanel.transform,
            "PILIH ULANG",
            new Vector2(-165f, -235f),
            new Vector2(240f, 64f),
            new Color(0.25f, 0.29f, 0.33f, 1f),
            Color.white
        );
        chooseAgain.onClick.AddListener(() =>
            SetPanelState(showChoice: true, showForm: false, showKeyboard: false, showSuccess: false));

        Button create = CreateButton(
            "BTN_BuatEtiket",
            formPanel.transform,
            "BUAT ETIKET",
            new Vector2(165f, -235f),
            new Vector2(240f, 64f),
            Accent,
            Color.black
        );
        create.onClick.AddListener(CreateWorldLabel);
    }

    private void BuildKeyboardPanel()
    {
        keyboardPanel = CreateRect("PNL_KeyboardEtiket", uiRoot).gameObject;
        RectTransform keyboardRect = keyboardPanel.transform as RectTransform;
        keyboardRect.anchorMin = new Vector2(0.5f, 0f);
        keyboardRect.anchorMax = new Vector2(0.5f, 0f);
        keyboardRect.pivot = new Vector2(0.5f, 0f);
        keyboardRect.anchoredPosition = new Vector2(0f, 8f);
        keyboardRect.sizeDelta = new Vector2(680f, 315f);

        Image background = keyboardPanel.AddComponent<Image>();
        background.color = new Color(0.02f, 0.025f, 0.035f, 0.98f);

        CreateKeyboardRow("1234567890", 108f, 56f, 62f);
        CreateKeyboardRow("QWERTYUIOP", 43f, 56f, 62f);
        CreateKeyboardRow("ASDFGHJKL", -22f, 56f, 62f);
        CreateKeyboardRow("ZXCVBNM", -87f, 56f, 62f);

        Button space = CreateButton(
            "BTN_Key_Space",
            keyboardPanel.transform,
            "SPASI",
            new Vector2(-95f, -135f),
            new Vector2(250f, 54f),
            new Color(0.22f, 0.25f, 0.29f, 1f),
            Color.white
        );
        space.onClick.AddListener(() => AddCharacter(" "));

        Button backspace = CreateButton(
            "BTN_Key_Backspace",
            keyboardPanel.transform,
            "HAPUS",
            new Vector2(115f, -135f),
            new Vector2(145f, 54f),
            new Color(0.42f, 0.18f, 0.16f, 1f),
            Color.white
        );
        backspace.onClick.AddListener(DeleteCharacter);

        Button done = CreateButton(
            "BTN_Key_Done",
            keyboardPanel.transform,
            "SELESAI",
            new Vector2(275f, -135f),
            new Vector2(145f, 54f),
            Accent,
            Color.black
        );
        done.onClick.AddListener(() => keyboardPanel.SetActive(false));
    }

    private void CreateFormPreview()
    {
        Image border = CreateSizedImage(
            "IMG_FormEtiketBorder",
            formPanel.transform,
            new Vector2(0f, 175f),
            new Vector2(570f, 150f),
            Color.black
        );

        formPreviewCard = CreateSizedImage(
            "IMG_FormEtiketCard",
            border.transform,
            Vector2.zero,
            new Vector2(554f, 134f),
            WhiteEtiket
        );

        CreateSizedImage(
            "LINE_FormEtiket",
            formPreviewCard.transform,
            new Vector2(0f, 18f),
            new Vector2(520f, 4f),
            Color.black
        );

        formPreviewHeader = CreateText(
            "TXT_FormEtiketHeader",
            formPreviewCard.transform,
            "ETIKET OBAT - OBAT DALAM",
            24f,
            FontStyles.Bold,
            new Vector2(0f, 47f),
            new Vector2(520f, 38f),
            Color.black
        );

        formPreviewBody = CreateText(
            "TXT_FormEtiketBody",
            formPreviewCard.transform,
            "No: 001       Tgl: -\nNama: -       Untuk: -",
            18f,
            FontStyles.Normal,
            new Vector2(0f, -30f),
            new Vector2(510f, 76f),
            Color.black
        );
        formPreviewBody.alignment = TextAlignmentOptions.MidlineLeft;
        formPreviewBody.margin = new Vector4(12f, 4f, 12f, 4f);
    }

    private void RefreshFormPreview()
    {
        if (formPreviewCard != null)
            formPreviewCard.color = whiteEtiket ? WhiteEtiket : BlueEtiket;

        if (formPreviewHeader != null)
            formPreviewHeader.text = whiteEtiket
                ? "ETIKET OBAT - OBAT DALAM"
                : "ETIKET OBAT - OBAT LUAR";

        if (formPreviewBody != null)
        {
            string number = SafeText(numberInput, "001");
            string patient = SafeText(nameInput, "-");
            string usage = SafeText(usageInput, "-");
            string date = SafeText(dateInput, "-");
            formPreviewBody.text =
                $"No: {number}       Tgl: {date}\n" +
                $"Nama: {patient}       Untuk: {usage}";
        }
    }

    private void BuildSuccessPanel()
    {
        successPanel = CreateRect("PNL_SirupBerhasil", uiRoot).gameObject;

        CreateText(
            "TXT_SirupBerhasil",
            successPanel.transform,
            "SIMULASI SELESAI",
            54f,
            FontStyles.Bold,
            new Vector2(0f, 145f),
            new Vector2(820f, 80f),
            new Color(0.35f, 1f, 0.58f, 1f)
        );

        CreateText(
            "TXT_SirupBerhasilDetail",
            successPanel.transform,
            "Sirup Difenhidramin 250 mg / 100 ml sudah dibuat,\ndimasukkan ke botol, dan diberi etiket.",
            30f,
            FontStyles.Normal,
            new Vector2(0f, 35f),
            new Vector2(820f, 120f),
            Color.white
        );

        Button back = CreateButton(
            "BTN_BackToSimulationMenu",
            successPanel.transform,
            "BACK",
            new Vector2(0f, -115f),
            new Vector2(310f, 84f),
            Accent,
            Color.black
        );
        back.onClick.AddListener(() => BackRequested?.Invoke());
    }

    private void SelectEtiketColor(bool useWhite)
    {
        whiteEtiket = useWhite;

        if (formTitle != null)
        {
            formTitle.text = useWhite ? "Isi Etiket Putih (Obat Dalam)" : "Isi Etiket Biru (Obat Luar)";
            formTitle.color = useWhite ? Color.white : BlueEtiket;
        }

        RefreshFormPreview();
        SetPanelState(showChoice: false, showForm: true, showKeyboard: false, showSuccess: false);
    }

    private void OpenKeyboard(TMP_InputField input)
    {
        activeInput = input;
        if (keyboardPanel != null)
            keyboardPanel.SetActive(true);
    }

    private void AddCharacter(string character)
    {
        if (activeInput == null)
            return;

        activeInput.text += character;
        activeInput.caretPosition = activeInput.text.Length;
    }

    private void DeleteCharacter()
    {
        if (activeInput == null || string.IsNullOrEmpty(activeInput.text))
            return;

        activeInput.text = activeInput.text.Substring(0, activeInput.text.Length - 1);
        activeInput.caretPosition = activeInput.text.Length;
    }

    private void CreateWorldLabel()
    {
        DestroyCurrentLabel();

        labelObject = new GameObject("EtiketObat_Grabbable");
        labelObject.transform.position = GetLabelSpawnPosition();
        labelObject.transform.rotation = GetFacingRotation(labelObject.transform.position);

        BoxCollider collider = labelObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(labelSizeMeters.x * 1.08f, labelSizeMeters.y * 1.12f, 0.012f);

        labelRigidbody = labelObject.AddComponent<Rigidbody>();
        labelRigidbody.mass = 0.03f;
        labelRigidbody.useGravity = false;
        labelRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        labelRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        labelGrab = labelObject.AddComponent<XRGrabInteractable>();
        labelGrab.selectEntered.AddListener(_ => labelWasGrabbed = true);

        CreateLabelCardVisual();

        labelWasGrabbed = false;
        labelIsAttached = false;
        SetPanelState(showChoice: false, showForm: false, showKeyboard: false, showSuccess: false);
        LabelCreated?.Invoke(labelObject);
    }

    private void CreateLabelCardVisual()
    {
        GameObject canvasObject = new GameObject("EtiketCardCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(labelObject.transform, false);
        canvasObject.transform.localPosition = Vector3.zero;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * 0.0001f;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 40;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(900f, 520f);

        Image border = CreateSizedImage(
            "IMG_EtiketBorder",
            canvasObject.transform,
            Vector2.zero,
            new Vector2(900f, 520f),
            Color.black
        );

        Image card = CreateSizedImage(
            whiteEtiket ? "IMG_EtiketPutih" : "IMG_EtiketBiru",
            border.transform,
            Vector2.zero,
            new Vector2(868f, 488f),
            whiteEtiket ? WhiteEtiket : BlueEtiket
        );

        CreateSizedImage(
            "LINE_EtiketHeader",
            card.transform,
            new Vector2(0f, 112f),
            new Vector2(820f, 8f),
            Color.black
        );

        CreateSizedImage(
            "LINE_EtiketFooter",
            card.transform,
            new Vector2(0f, -155f),
            new Vector2(820f, 6f),
            Color.black
        );

        string category = whiteEtiket ? "OBAT DALAM" : "OBAT LUAR";
        string number = SafeText(numberInput, "001");
        string patient = SafeText(nameInput, "-");
        string usage = SafeText(usageInput, "-");
        string date = SafeText(dateInput, DateTime.Now.ToString("dd-MM-yyyy"));
        TMP_Text header = CreateText(
            "TXT_EtiketHeader",
            card.transform,
            $"ETIKET OBAT - {category}",
            42f,
            FontStyles.Bold,
            new Vector2(0f, 178f),
            new Vector2(800f, 70f),
            new Color(0.025f, 0.035f, 0.05f, 1f)
        );
        header.alignment = TextAlignmentOptions.Center;

        TMP_Text details = CreateText(
            "TXT_EtiketDetail",
            card.transform,
            $"No: {number}                         Tgl: {date}\nNama: {patient}\nUntuk: {usage}",
            34f,
            FontStyles.Normal,
            new Vector2(0f, -5f),
            new Vector2(790f, 220f),
            new Color(0.025f, 0.035f, 0.05f, 1f)
        );
        details.alignment = TextAlignmentOptions.MidlineLeft;
        details.margin = new Vector4(24f, 8f, 24f, 8f);

        CreateText(
            "TXT_EtiketFooter",
            card.transform,
            "<b>DIFENHIDRAMIN 250 mg / 100 ml</b>",
            31f,
            FontStyles.Bold,
            new Vector2(0f, -205f),
            new Vector2(800f, 54f),
            new Color(0.025f, 0.035f, 0.05f, 1f)
        );
    }

    private void AttachLabelToBottle(Vector3 position, Quaternion rotation)
    {
        labelIsAttached = true;

        labelObject.transform.SetPositionAndRotation(position, rotation);
        labelObject.transform.SetParent(bottle, true);

        if (labelRigidbody != null)
        {
            labelRigidbody.linearVelocity = Vector3.zero;
            labelRigidbody.angularVelocity = Vector3.zero;
            labelRigidbody.isKinematic = true;
            labelRigidbody.useGravity = false;
        }

        if (labelGrab != null)
            labelGrab.enabled = false;

        LabelAttached?.Invoke();
    }

    private Vector3 GetLabelSpawnPosition()
    {
        if (bottleRenderer != null)
            return bottleRenderer.bounds.center + labelSpawnOffset;

        return bottle != null ? bottle.position + labelSpawnOffset : transform.position + Vector3.up;
    }

    private Vector3 GetBottleSnapPosition(out Quaternion rotation)
    {
        Vector3 center = bottleRenderer != null ? bottleRenderer.bounds.center : bottle.position;
        Vector3 towardViewer = Camera.main != null
            ? Vector3.ProjectOnPlane(Camera.main.transform.position - center, Vector3.up)
            : -bottle.forward;

        if (towardViewer.sqrMagnitude < 0.001f)
            towardViewer = -bottle.forward;

        towardViewer.Normalize();

        float radius = 0.045f;
        if (bottleRenderer != null)
            radius = Mathf.Max(0.025f, Mathf.Min(bottleRenderer.bounds.extents.x, bottleRenderer.bounds.extents.z) * 0.88f);

        Vector3 position = center + towardViewer * (radius + 0.002f);
        rotation = Quaternion.LookRotation(-towardViewer, Vector3.up);
        return position;
    }

    private Quaternion GetFacingRotation(Vector3 worldPosition)
    {
        if (Camera.main == null)
            return Quaternion.identity;

        Vector3 direction = Camera.main.transform.position - worldPosition;
        return direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(-direction.normalized, Vector3.up)
            : Quaternion.identity;
    }

    private void SetPanelState(bool showChoice, bool showForm, bool showKeyboard, bool showSuccess)
    {
        if (uiRoot != null)
            uiRoot.gameObject.SetActive(showChoice || showForm || showKeyboard || showSuccess);

        if (choicePanel != null)
            choicePanel.SetActive(showChoice);

        if (formPanel != null)
            formPanel.SetActive(showForm);

        if (keyboardPanel != null)
            keyboardPanel.SetActive(showKeyboard);

        if (successPanel != null)
            successPanel.SetActive(showSuccess);
    }

    private void DestroyCurrentLabel()
    {
        if (labelObject != null)
            Destroy(labelObject);

        labelObject = null;
        labelGrab = null;
        labelRigidbody = null;
    }

    private TMP_InputField CreateInput(string objectName, Transform parent, string placeholderText, Vector2 position)
    {
        RectTransform root = CreateRect(objectName, parent);
        root.anchoredPosition = position;
        root.sizeDelta = new Vector2(570f, 56f);

        Image background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0.96f, 0.97f, 0.98f, 1f);

        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;

        RectTransform viewport = CreateRect("Text Area", root);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(22f, 8f);
        viewport.offsetMax = new Vector2(-22f, -8f);

        TMP_Text placeholder = CreateText(
            "Placeholder",
            viewport,
            placeholderText,
            22f,
            FontStyles.Italic,
            Vector2.zero,
            new Vector2(520f, 42f),
            new Color(0.35f, 0.39f, 0.43f, 0.75f)
        );
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_Text text = CreateText(
            "Text",
            viewport,
            string.Empty,
            23f,
            FontStyles.Normal,
            Vector2.zero,
            new Vector2(520f, 42f),
            new Color(0.03f, 0.045f, 0.06f, 1f)
        );
        text.alignment = TextAlignmentOptions.MidlineLeft;

        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.onSelect.AddListener(_ => OpenKeyboard(input));
        return input;
    }

    private void CreateKeyboardRow(string characters, float y, float keyWidth, float spacing)
    {
        float totalWidth = characters.Length * spacing;
        float startX = -totalWidth * 0.5f + spacing * 0.5f;

        for (int i = 0; i < characters.Length; i++)
        {
            string character = characters[i].ToString();
            Button key = CreateButton(
                $"BTN_Key_{character}",
                keyboardPanel.transform,
                character,
                new Vector2(startX + i * spacing, y),
                new Vector2(keyWidth - 6f, 64f),
                new Color(0.18f, 0.21f, 0.25f, 1f),
                Color.white
            );
            key.onClick.AddListener(() => AddCharacter(character));
        }
    }

    private static Button CreateEtiketPreviewButton(
        string objectName,
        Transform parent,
        string header,
        string category,
        Vector2 position,
        Vector2 size,
        Color cardColor,
        Color textColor)
    {
        Image border = CreateSizedImage(objectName, parent, position, size, Color.black);
        Button button = border.gameObject.AddComponent<Button>();
        button.targetGraphic = border;

        Image card = CreateSizedImage(
            "Card",
            border.transform,
            Vector2.zero,
            size - new Vector2(12f, 12f),
            cardColor
        );

        CreateSizedImage(
            "Divider",
            card.transform,
            new Vector2(0f, 18f),
            new Vector2(size.x - 35f, 4f),
            Color.black
        );

        CreateText(
            "Header",
            card.transform,
            header,
            23f,
            FontStyles.Bold,
            new Vector2(0f, 65f),
            new Vector2(size.x - 28f, 42f),
            textColor
        );

        TMP_Text body = CreateText(
            "Body",
            card.transform,
            $"No: ____      Tgl: ____\nNama: __________\nUntuk: __________\n<b>{category}</b>",
            16f,
            FontStyles.Normal,
            new Vector2(0f, -43f),
            new Vector2(size.x - 32f, 110f),
            textColor
        );
        body.alignment = TextAlignmentOptions.MidlineLeft;
        body.margin = new Vector4(8f, 2f, 8f, 2f);
        return button;
    }

    private static Image CreateSizedImage(
        string objectName,
        Transform parent,
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
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 position,
        Vector2 size,
        Color backgroundColor,
        Color textColor)
    {
        RectTransform rect = CreateRect(objectName, parent);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = backgroundColor;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText(
            "Text",
            rect,
            label,
            Mathf.Min(30f, size.y * 0.36f),
            FontStyles.Bold,
            Vector2.zero,
            size - new Vector2(16f, 12f),
            textColor
        );
        text.raycastTarget = false;

        return button;
    }

    private static void SetRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return;

        Material material = new Material(shader)
        {
            name = "Runtime_EtiketMaterial",
            color = color
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        renderer.sharedMaterial = material;
    }

    private static string SafeText(TMP_InputField input, string fallback)
    {
        return input != null && !string.IsNullOrWhiteSpace(input.text)
            ? input.text.Trim()
            : fallback;
    }

    private static void EnableCanvasRaycasters(RectTransform canvasRoot)
    {
        Behaviour[] behaviours = canvasRoot.GetComponents<Behaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.GetType().Name.Contains("GraphicRaycaster"))
                behaviour.enabled = true;
        }
    }
}
