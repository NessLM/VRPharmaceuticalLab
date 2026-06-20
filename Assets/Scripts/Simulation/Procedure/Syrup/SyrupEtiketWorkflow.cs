using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class SyrupEtiketWorkflow : MonoBehaviour
{
    [Header("Scene UI")]
    [SerializeField] private SyrupEtiketPanelRig panelRig;

    [Header("World Label")]
    [SerializeField] private float attachDistance = 0.12f;
    [SerializeField] private Vector2 labelSizeMeters = new Vector2(0.07f, 0.042f);
    [SerializeField] private Vector3 labelSpawnOffset = new Vector3(0.26f, 0.16f, 0f);

    public event Action<GameObject> LabelCreated;
    public event Action LabelAttached;
    public event Action BackRequested;

    private Transform bottle;
    private Renderer bottleRenderer;
    private GameObject labelObject;
    private XRGrabInteractable labelGrab;
    private Rigidbody labelRigidbody;
    private bool whiteEtiket = true;
    private bool labelWasGrabbed;
    private bool labelIsAttached;
    private bool eventsBound;

    private static readonly Color WhiteEtiket = new Color(0.97f, 0.98f, 0.96f, 1f);
    private static readonly Color BlueEtiket = new Color(0.16f, 0.66f, 0.86f, 1f);
    private static readonly Color DarkInk = new Color(0.025f, 0.035f, 0.05f, 1f);

    public void Initialize(RectTransform unusedCanvasRoot, Transform bottleTarget)
    {
        bottle = bottleTarget;
        bottleRenderer = bottle != null ? bottle.GetComponentInChildren<Renderer>(true) : null;
        ResolvePanelRig();
        BindPanelEvents();
    }

    public void BeginLabelSelection(RectTransform unusedCanvasRoot, Transform bottleTarget)
    {
        Initialize(unusedCanvasRoot, bottleTarget);
        DestroyCurrentLabel();

        whiteEtiket = true;
        labelWasGrabbed = false;
        labelIsAttached = false;

        if (panelRig == null || !panelRig.IsConfigured)
        {
            Debug.LogError("[SyrupEtiket] World-space Etiket UI belum tersedia di VRLabSimulation.", this);
            return;
        }

        panelRig.ConfigureFollowTarget(Camera.main != null ? Camera.main.transform : null);

        if (panelRig.NumberInput != null)
            panelRig.NumberInput.text = "001";

        if (panelRig.NameInput != null)
            panelRig.NameInput.text = string.Empty;

        if (panelRig.UsageInput != null)
            panelRig.UsageInput.text = string.Empty;

        if (panelRig.DateInput != null)
            panelRig.DateInput.text = DateTime.Now.ToString("dd-MM-yyyy");

        panelRig.SetStatus("Arahkan ray controller ke salah satu etiket.", new Color(0.78f, 0.86f, 0.94f, 1f));
        RefreshFormPreview();
        panelRig.ShowChoice();
    }

    public void ShowSuccess()
    {
        ResolvePanelRig();

        if (panelRig != null)
        {
            panelRig.ConfigureFollowTarget(Camera.main != null ? Camera.main.transform : null);
            panelRig.ShowSuccess();
        }
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

    private void ResolvePanelRig()
    {
        if (panelRig == null)
            panelRig = FindFirstObjectByType<SyrupEtiketPanelRig>(FindObjectsInactive.Include);
    }

    private void BindPanelEvents()
    {
        if (eventsBound || panelRig == null || !panelRig.IsConfigured)
            return;

        panelRig.WhiteEtiketButton.onClick.AddListener(() => SelectEtiketColor(true));
        panelRig.BlueEtiketButton.onClick.AddListener(() => SelectEtiketColor(false));

        if (panelRig.ChooseAgainButton != null)
            panelRig.ChooseAgainButton.onClick.AddListener(panelRig.ShowChoice);

        if (panelRig.CreateLabelButton != null)
            panelRig.CreateLabelButton.onClick.AddListener(CreateWorldLabel);

        if (panelRig.BackButton != null)
            panelRig.BackButton.onClick.AddListener(() => BackRequested?.Invoke());

        BindInput(panelRig.NumberInput);
        BindInput(panelRig.NameInput);
        BindInput(panelRig.UsageInput);
        BindInput(panelRig.DateInput);
        eventsBound = true;
    }

    private void BindInput(TMP_InputField input)
    {
        if (input == null)
            return;

        input.onSelect.AddListener(_ => panelRig.OpenKeyboard(input));
        input.onValueChanged.AddListener(_ => RefreshFormPreview());
    }

    private void SelectEtiketColor(bool useWhite)
    {
        whiteEtiket = useWhite;

        if (panelRig.FormTitle != null)
        {
            panelRig.FormTitle.text = useWhite
                ? "Isi Etiket Putih - Obat Dalam"
                : "Isi Etiket Biru - Obat Luar";
            panelRig.FormTitle.color = useWhite ? Color.white : BlueEtiket;
        }

        panelRig.SetStatus("Pilih kolom dengan ray controller untuk membuka keyboard VR.", new Color(0.78f, 0.86f, 0.94f, 1f));
        RefreshFormPreview();
        panelRig.ShowForm();
    }

    private void RefreshFormPreview()
    {
        if (panelRig == null)
            return;

        if (panelRig.PreviewCard != null)
            panelRig.PreviewCard.color = whiteEtiket ? WhiteEtiket : BlueEtiket;

        if (panelRig.PreviewHeader != null)
            panelRig.PreviewHeader.text = whiteEtiket
                ? "ETIKET OBAT - OBAT DALAM"
                : "ETIKET OBAT - OBAT LUAR";

        if (panelRig.PreviewBody != null)
        {
            panelRig.PreviewBody.text =
                $"No: {SafeText(panelRig.NumberInput, "001")}      Tgl: {SafeText(panelRig.DateInput, "-")}\n" +
                $"Nama: {SafeText(panelRig.NameInput, "-")}\n" +
                $"Untuk: {SafeText(panelRig.UsageInput, "-")}";
        }
    }

    private void CreateWorldLabel()
    {
        if (panelRig == null)
            return;

        if (string.IsNullOrWhiteSpace(panelRig.NameInput != null ? panelRig.NameInput.text : null) ||
            string.IsNullOrWhiteSpace(panelRig.UsageInput != null ? panelRig.UsageInput.text : null))
        {
            panelRig.SetStatus("Nama dan kegunaan/aturan pakai harus diisi terlebih dahulu.", new Color(1f, 0.42f, 0.32f, 1f));
            return;
        }

        DestroyCurrentLabel();

        labelObject = new GameObject("EtiketObat_Grabbable");
        labelObject.transform.position = GetLabelSpawnPosition();
        labelObject.transform.rotation = GetFacingRotation(labelObject.transform.position);

        BoxCollider collider = labelObject.AddComponent<BoxCollider>();
        collider.size = new Vector3(labelSizeMeters.x * 1.06f, labelSizeMeters.y * 1.08f, 0.004f);

        labelRigidbody = labelObject.AddComponent<Rigidbody>();
        labelRigidbody.mass = 0.025f;
        labelRigidbody.useGravity = false;
        labelRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        labelRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        labelGrab = labelObject.AddComponent<XRGrabInteractable>();
        labelGrab.selectEntered.AddListener(_ => labelWasGrabbed = true);

        CreateLabelCardVisual();

        labelWasGrabbed = false;
        labelIsAttached = false;
        panelRig.HideAll();
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

        Image border = CreateSizedImage("IMG_EtiketBorder", canvasObject.transform, Vector2.zero, new Vector2(1000f, 600f), Color.black);
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
            $"No: {SafeText(panelRig.NumberInput, "001")}                 Tgl: {SafeText(panelRig.DateInput, DateTime.Now.ToString("dd-MM-yyyy"))}\n" +
            $"Nama: {SafeText(panelRig.NameInput, "-")}\n" +
            $"Untuk: {SafeText(panelRig.UsageInput, "-")}",
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
            "<b>DIFENHIDRAMIN 250 mg / 100 ml</b>",
            32f,
            FontStyles.Bold,
            new Vector2(0f, -240f),
            new Vector2(900f, 54f),
            DarkInk);
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
        Vector3 center = bottleRenderer != null
            ? bottleRenderer.bounds.center
            : bottle != null ? bottle.position : transform.position;

        if (Camera.main == null)
            return center + labelSpawnOffset;

        return center +
               Camera.main.transform.right * labelSpawnOffset.x +
               Vector3.up * labelSpawnOffset.y +
               Camera.main.transform.forward * labelSpawnOffset.z;
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

        float radius = 0.046f;
        if (bottleRenderer != null)
            radius = Mathf.Max(0.03f, Mathf.Min(bottleRenderer.bounds.extents.x, bottleRenderer.bounds.extents.z) * 0.93f);

        Vector3 position = center + towardViewer * (radius + 0.0015f);
        rotation = Quaternion.LookRotation(towardViewer, Vector3.up);
        return position;
    }

    private Quaternion GetFacingRotation(Vector3 worldPosition)
    {
        if (Camera.main == null)
            return Quaternion.identity;

        Vector3 direction = Camera.main.transform.position - worldPosition;
        return direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : Quaternion.identity;
    }

    private void DestroyCurrentLabel()
    {
        if (labelObject != null)
            Destroy(labelObject);

        labelObject = null;
        labelGrab = null;
        labelRigidbody = null;
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
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static string SafeText(TMP_InputField input, string fallback)
    {
        return input != null && !string.IsNullOrWhiteSpace(input.text)
            ? input.text.Trim()
            : fallback;
    }
}
