using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Scrollable checklist weight selector for the analytical balance right pan.
/// Builds its own UI at runtime — no prefabs needed.
/// Weight objects under timbanganNeraca are auto-discovered; Inspector fields
/// can override the auto-discovery when needed.
///
/// Flow: check items → press Terima → weights teleport to pan (kinematic + solid).
/// Reset clears everything. X button hides the canvas panel.
///
/// Attach to: WeightSelectorPanel (child of WeightSelectorCanvas, child of timbanganNeraca).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class VirtualWeightSelector : MonoBehaviour
{
    // ── Denomination table ──────────────────────────────────────────────────

    private static readonly (float grams, string label)[] Denominations =
    {
        (0.005f,  "5 mg"),
        (0.010f,  "10 mg"),
        (0.020f,  "20 mg"),
        (0.050f,  "50 mg"),
        (0.100f,  "100 mg"),
        (0.200f,  "200 mg"),
        (0.500f,  "500 mg"),
        (1f,      "1 g"),
        (2f,      "2 g"),
        (5f,      "5 g"),
        (10f,     "10 g"),
        (20f,     "20 g"),
        (50f,     "50 g"),
        (100f,    "100 g"),
        (200f,    "200 g"),
        (500f,    "500 g"),
    };

    /// <summary>Gram value → expected child name under timbanganNeraca.</summary>
    private static readonly (float grams, string goName)[] WeightObjectMap =
    {
        (0.200f, "Weight_200mg"),
        (0.500f, "Weight_500mg"),
        (1f,     "Weight_1g"),
        (2f,     "Weight_2g"),
        (5f,     "Weight_5g"),
        (10f,    "Weight_10g"),
        (20f,    "Weight_20g"),
        (50f,    "Weight_50g"),
        (100f,   "Weight_100g"),
        (200f,   "Weight_200g"),
        (500f,   "Weight_500g"),
    };

    private const float RowHeight        = 46f;
    private const float HeaderHeight     = 52f;
    private const float FooterHeight     = 56f;
    private const float TotalRowHeight   = 44f;
    private const float ScrollAreaHeight = 280f;

    /// <summary>
    /// Y offset above Balance_WeightRight's localPosition (in timbanganNeraca space)
    /// where the first weight is placed.
    /// </summary>
    private const float PlaceYOffset = 0.015f;

    /// <summary>Stacking step per weight (timbanganNeraca local units).</summary>
    private const float StackStep = 0.004f;

    // ── Inspector ───────────────────────────────────────────────────────────

    [Header("Panel Root (auto-resolved to parent Canvas if not set)")]
    [SerializeField] private GameObject panelRoot;

    [Header("Right Pan (auto-found as 'Balance_WeightRight' if not set)")]
    [SerializeField] private Transform rightPanParent;

    [Header("Override: weight GameObjects and their gram values (auto-discovered if empty)")]
    [SerializeField] private GameObject[] weightObjects;
    [SerializeField] private float[]      weightGramValues;

    [Header("Colors")]
    [SerializeField] private Color checkedColor   = new Color(0.15f, 0.70f, 0.20f, 1f);
    [SerializeField] private Color uncheckedColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color panelBgColor   = new Color(0.12f, 0.12f, 0.15f, 0.96f);
    [SerializeField] private Color headerBgColor  = new Color(0.08f, 0.08f, 0.10f, 1f);
    [SerializeField] private Color acceptBtnColor = new Color(0.15f, 0.60f, 0.20f, 1f);
    [SerializeField] private Color resetBtnColor  = new Color(0.60f, 0.15f, 0.15f, 1f);

    [Header("Events")]
    public UnityEvent<float> onTargetAccepted;
    public UnityEvent        onTargetCleared;

    // ── Runtime state ───────────────────────────────────────────────────────

    private readonly bool[]       _selected   = new bool[Denominations.Length];
    private readonly Image[]      _rowBg      = new Image[Denominations.Length];
    private readonly GameObject[] _checkmarks = new GameObject[Denominations.Length];

    private TMP_Text _totalText;
    private Button   _acceptBtn;

    private float _currentTotal;
    private float _lockedTotal;
    private bool  _isLocked;

    [Header("Weight Visual Placement")]
    [Tooltip("If true, weight GameObjects are re-parented to Balance_WeightRight on Accept " +
             "so they tilt with the pan. On Reset, they are restored to their original parent.")]
    [SerializeField] private bool reparentToRightPan = true;
    [SerializeField] private bool hideWeightsOnReset = false;

    private struct WeightState
    {
        public Transform  originalParent;  // saved for reparenting restore
        public Vector3    localPosition;
        public Quaternion localRotation;
        public bool       wasKinematic;
        public bool       wasTrigger;
    }

    private WeightState[] _originalStates;

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>Returns locked right-pan mass in grams once accepted.</summary>
    public float LockedRightMassGrams => _isLocked ? _lockedTotal : 0f;

    /// <summary>True when the user has accepted a weight selection.</summary>
    public bool IsLocked => _isLocked;

    /// <summary>Live running total (pre-accept).</summary>
    public float SelectedTotalGrams => _currentTotal;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-discover panelRoot from immediate parent (the Canvas)
        if (panelRoot == null)
            panelRoot = transform.parent?.gameObject;

        // Walk up the hierarchy until we find the timbanganNeraca by looking for
        // its distinctive "Balance_WeightRight" child. This is robust to any number
        // of intermediate anchor GameObjects between the canvas and the timbangan.
        Transform timbanganNeraca = FindTimbanganRoot();
        if (timbanganNeraca != null)
        {
            if (rightPanParent == null)
                rightPanParent = timbanganNeraca.Find("Balance_WeightRight");

            if (weightObjects == null || weightObjects.Length == 0)
                AutoDiscoverWeights(timbanganNeraca);
        }

        SaveOriginalStates();
    }

    /// <summary>Walks up the hierarchy to find the balance root (contains Balance_WeightRight).</summary>
    private Transform FindTimbanganRoot()
    {
        Transform current = transform.parent;
        while (current != null)
        {
            if (current.Find("Balance_WeightRight") != null)
                return current;
            current = current.parent;
        }
        return null;
    }

    private void Start()
    {
        ClearOldChildren();
        SetupPanelLayout();
        BuildHeader();
        BuildScrollList();
        BuildTotalRow();
        BuildFooter();
        // Weight objects stay visible; Reset restores them to their box/home pose.
    }

    // ── Auto-discovery ──────────────────────────────────────────────────────

    private void AutoDiscoverWeights(Transform timbanganNeraca)
    {
        var objs  = new List<GameObject>();
        var grams = new List<float>();

        foreach (var (g, name) in WeightObjectMap)
        {
            Transform t = timbanganNeraca.Find(name);
            if (t != null)
            {
                objs.Add(t.gameObject);
                grams.Add(g);
            }
        }

        weightObjects    = objs.ToArray();
        weightGramValues = grams.ToArray();
    }

    private void SaveOriginalStates()
    {
        if (weightObjects == null) return;

        _originalStates = new WeightState[weightObjects.Length];
        for (int i = 0; i < weightObjects.Length; i++)
        {
            var w = weightObjects[i];
            if (w == null) continue;

            _originalStates[i] = new WeightState
            {
                originalParent = w.transform.parent,
                localPosition  = w.transform.localPosition,
                localRotation  = w.transform.localRotation,
                wasKinematic   = w.TryGetComponent<Rigidbody>(out var rb) && rb.isKinematic,
                wasTrigger     = w.TryGetComponent<BoxCollider>(out var col) && col.isTrigger,
            };
        }
    }

    // ── UI Construction ─────────────────────────────────────────────────────

    private void ClearOldChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    private void SetupPanelLayout()
    {
        Image bg = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        bg.color = panelBgColor;

        VerticalLayoutGroup vlg = gameObject.GetComponent<VerticalLayoutGroup>()
                                ?? gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing                = 0f;
        vlg.padding                = new RectOffset(0, 0, 0, 0);
        vlg.childAlignment         = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;
    }

    // ── Header (title + close button) ───────────────────────────────────────

    private void BuildHeader()
    {
        GameObject header = MakeContainer("Header", transform, HeaderHeight);
        Image hbg = header.AddComponent<Image>();
        hbg.color = headerBgColor;

        HorizontalLayoutGroup hlg = header.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset(12, 8, 8, 8);
        hlg.spacing                = 8f;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = true;

        GameObject titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(header.transform, false);
        TMP_Text titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        titleTxt.text      = "Pilih Anak Timbangan";
        titleTxt.fontSize  = 17f;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color     = Color.white;
        titleTxt.alignment = TextAlignmentOptions.Left;
        LayoutElement titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.flexibleWidth = 1f;

        Button closeBtn = MakeButton("CloseBtn", header.transform, "✕", 36f, 36f,
                                     new Color(0.7f, 0.18f, 0.18f, 1f), Color.white, 18f);
        closeBtn.onClick.AddListener(OnCloseClicked);
        LayoutElement closLE = closeBtn.gameObject.AddComponent<LayoutElement>();
        closLE.preferredWidth  = 36f;
        closLE.preferredHeight = 36f;
    }

    // ── Scroll list ─────────────────────────────────────────────────────────

    private void BuildScrollList()
    {
        GameObject scrollContainer = MakeContainer("ScrollContainer", transform, ScrollAreaHeight);
        LayoutElement scrollLE = scrollContainer.AddComponent<LayoutElement>();
        scrollLE.preferredHeight = ScrollAreaHeight;

        Image scrollBg = scrollContainer.AddComponent<Image>();
        scrollBg.color = new Color(0.09f, 0.09f, 0.12f, 1f);

        ScrollRect scrollRect = scrollContainer.AddComponent<ScrollRect>();
        scrollRect.horizontal        = false;
        scrollRect.vertical          = true;
        scrollRect.scrollSensitivity = 20f;
        scrollRect.movementType      = ScrollRect.MovementType.Clamped;

        // Viewport (Mask)
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollContainer.transform, false);
        RectTransform vpRT = viewport.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = Color.clear;
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        scrollRect.viewport = vpRT;

        // Content
        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        VerticalLayoutGroup contentVLG = content.AddComponent<VerticalLayoutGroup>();
        contentVLG.spacing                = 2f;
        contentVLG.padding                = new RectOffset(4, 4, 4, 4);
        contentVLG.childAlignment         = TextAnchor.UpperCenter;
        contentVLG.childForceExpandWidth  = true;
        contentVLG.childForceExpandHeight = false;
        contentVLG.childControlWidth      = true;
        contentVLG.childControlHeight     = false;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRT;

        for (int i = 0; i < Denominations.Length; i++)
            BuildToggleRow(content.transform, i);
    }

    private void BuildToggleRow(Transform parent, int idx)
    {
        string lbl = Denominations[idx].label;

        GameObject row = new GameObject($"Row_{lbl.Replace(" ", "")}", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rowRT = row.GetComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(0, RowHeight);
        LayoutElement rowLE = row.AddComponent<LayoutElement>();
        rowLE.preferredHeight = RowHeight;

        Image rowBgImg = row.AddComponent<Image>();
        rowBgImg.color = idx % 2 == 0
            ? new Color(0.14f, 0.14f, 0.17f, 1f)
            : new Color(0.11f, 0.11f, 0.14f, 1f);

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset(10, 10, 5, 5);
        hlg.spacing                = 12f;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = true;

        // Checkbox
        GameObject checkGO = new GameObject("Check", typeof(RectTransform));
        checkGO.transform.SetParent(row.transform, false);
        Image checkImg = checkGO.AddComponent<Image>();
        checkImg.color = uncheckedColor;
        _rowBg[idx]    = checkImg;
        RectTransform checkRT = checkGO.GetComponent<RectTransform>();
        checkRT.sizeDelta = new Vector2(34f, 34f);
        LayoutElement checkLE = checkGO.AddComponent<LayoutElement>();
        checkLE.preferredWidth  = 34f;
        checkLE.preferredHeight = 34f;

        Button rowBtn = checkGO.AddComponent<Button>();
        rowBtn.targetGraphic = checkImg;
        ColorBlock cb = ColorBlock.defaultColorBlock;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cb.pressedColor     = new Color(0.65f, 0.65f, 0.65f, 1f);
        rowBtn.colors = cb;

        // Checkmark ✓
        GameObject markGO = new GameObject("Mark", typeof(RectTransform));
        markGO.transform.SetParent(checkGO.transform, false);
        TMP_Text markTxt = markGO.AddComponent<TextMeshProUGUI>();
        markTxt.text      = "✓";
        markTxt.fontSize  = 20f;
        markTxt.fontStyle = FontStyles.Bold;
        markTxt.alignment = TextAlignmentOptions.Center;
        markTxt.color     = Color.white;
        RectTransform markRT = markGO.GetComponent<RectTransform>();
        markRT.anchorMin = Vector2.zero;
        markRT.anchorMax = Vector2.one;
        markRT.offsetMin = markRT.offsetMax = Vector2.zero;
        markGO.SetActive(false);
        _checkmarks[idx] = markGO;

        // Label
        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(row.transform, false);
        TMP_Text labelTxt = labelGO.AddComponent<TextMeshProUGUI>();
        labelTxt.text      = lbl;
        labelTxt.fontSize  = 17f;
        labelTxt.alignment = TextAlignmentOptions.Left;
        labelTxt.color     = Color.white;
        LayoutElement lblLE = labelGO.AddComponent<LayoutElement>();
        lblLE.flexibleWidth = 1f;

        // Whether this denomination has a physical weight object
        bool hasPhysical = HasPhysicalObject(Denominations[idx].grams);
        if (!hasPhysical)
        {
            // Dim the row slightly to indicate no physical object
            rowBgImg.color = idx % 2 == 0
                ? new Color(0.12f, 0.12f, 0.14f, 0.75f)
                : new Color(0.10f, 0.10f, 0.12f, 0.75f);
            labelTxt.color = new Color(0.75f, 0.75f, 0.75f, 1f);
        }

        int capturedIdx   = idx;
        float capturedGrams = Denominations[idx].grams;
        rowBtn.onClick.AddListener(() => OnRowToggled(capturedIdx, capturedGrams));
    }

    // ── Total row ───────────────────────────────────────────────────────────

    private void BuildTotalRow()
    {
        GameObject row = MakeContainer("TotalRow", transform, TotalRowHeight);
        Image bg = row.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.12f, 0.08f, 1f);

        GameObject textGO = new GameObject("TotalText", typeof(RectTransform));
        textGO.transform.SetParent(row.transform, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(12f, 0f);
        textRT.offsetMax = new Vector2(-12f, 0f);

        _totalText = textGO.AddComponent<TextMeshProUGUI>();
        _totalText.text      = "Total: 0 g";
        _totalText.fontSize  = 16f;
        _totalText.fontStyle = FontStyles.Bold;
        _totalText.alignment = TextAlignmentOptions.Center;
        _totalText.color     = Color.white;

        RefreshTotalDisplay();
    }

    // ── Footer (Reset + Accept) ─────────────────────────────────────────────

    private void BuildFooter()
    {
        GameObject footer = MakeContainer("Footer", transform, FooterHeight);
        Image fbg = footer.AddComponent<Image>();
        fbg.color = headerBgColor;

        HorizontalLayoutGroup hlg = footer.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset(12, 12, 8, 8);
        hlg.spacing                = 12f;
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = true;

        Button resetBtn = MakeButton("ResetBtn", footer.transform, "Reset", 110f, 38f,
                                     resetBtnColor, Color.white, 15f);
        resetBtn.onClick.AddListener(OnResetClicked);
        LayoutElement rLE = resetBtn.gameObject.AddComponent<LayoutElement>();
        rLE.preferredWidth  = 110f;
        rLE.preferredHeight = 38f;

        _acceptBtn = MakeButton("AcceptBtn", footer.transform, "Terima ✓", 140f, 38f,
                                acceptBtnColor, Color.white, 15f);
        _acceptBtn.onClick.AddListener(OnAcceptClicked);
        LayoutElement aLE = _acceptBtn.gameObject.AddComponent<LayoutElement>();
        aLE.preferredWidth  = 140f;
        aLE.preferredHeight = 38f;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static GameObject MakeContainer(string name, Transform parent, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        return go;
    }

    private static Button MakeButton(string name, Transform parent, string text,
                                     float w, float h, Color bg, Color fg, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);

        Image img = go.AddComponent<Image>();
        img.color = bg;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = ColorBlock.defaultColorBlock;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        cb.pressedColor     = new Color(0.70f, 0.70f, 0.70f, 1f);
        btn.colors = cb;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        TMP_Text lbl = labelGO.AddComponent<TextMeshProUGUI>();
        lbl.text      = text;
        lbl.fontSize  = fontSize;
        lbl.fontStyle = FontStyles.Bold;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color     = fg;
        RectTransform lblRT = labelGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;

        return btn;
    }

    // ── Toggle logic ────────────────────────────────────────────────────────

    private void OnRowToggled(int idx, float grams)
    {
        if (_isLocked) return;

        _selected[idx] = !_selected[idx];
        bool isOn = _selected[idx];

        if (_rowBg[idx]      != null) _rowBg[idx].color = isOn ? checkedColor : uncheckedColor;
        if (_checkmarks[idx] != null) _checkmarks[idx].SetActive(isOn);

        RecalculateTotal();
        RefreshTotalDisplay();
    }

    private void RecalculateTotal()
    {
        _currentTotal = 0f;
        for (int i = 0; i < Denominations.Length; i++)
            if (_selected[i]) _currentTotal += Denominations[i].grams;
    }

    // ── Button handlers ──────────────────────────────────────────────────────

    private void OnAcceptClicked()
    {
        if (_isLocked || _currentTotal < 0.001f) return;

        _isLocked    = true;
        _lockedTotal = _currentTotal;
        PlaceWeightObjectsOnPan();
        RefreshTotalDisplay();
        onTargetAccepted?.Invoke(_lockedTotal);
    }

    private void OnResetClicked()
    {
        _isLocked     = false;
        _lockedTotal  = 0f;
        _currentTotal = 0f;

        for (int i = 0; i < _selected.Length; i++)
        {
            _selected[i] = false;
            if (_rowBg[i]      != null) _rowBg[i].color = uncheckedColor;
            if (_checkmarks[i] != null) _checkmarks[i].SetActive(false);
        }

        ResetWeightObjectsToHome();
        RefreshTotalDisplay();
        onTargetCleared?.Invoke();
    }

    /// <summary>Called by the X button in the header or externally to hide the panel.</summary>
    public void OnCloseClicked()
    {
        GameObject root = panelRoot != null ? panelRoot : transform.parent?.gameObject;
        if (root != null) root.SetActive(false);
    }

    // ── UI refresh ───────────────────────────────────────────────────────────

    private void RefreshTotalDisplay()
    {
        if (_totalText == null) return;

        _totalText.text = _isLocked
            ? $"Target: {FormatGrams(_lockedTotal)}  [TERKUNCI]"
            : $"Total: {FormatGrams(_currentTotal)}";

        _totalText.color = _isLocked
            ? new Color(0.3f, 1f, 0.4f, 1f)
            : Color.white;

        if (_acceptBtn != null)
            _acceptBtn.interactable = !_isLocked && _currentTotal > 0.001f;
    }

    private static string FormatGrams(float g)
        => g < 1f ? $"{g * 1000f:F0} mg" : $"{g:F3} g";

    // ── Weight object placement ───────────────────────────────────────────────

    /// <summary>
    /// For each selected denomination that has a physical Weight GameObject,
    /// teleport it above Balance_WeightRight (kinematic + solid collider).
    /// Denominations without a corresponding GameObject still add to the mass total
    /// but produce no visual object.
    /// </summary>
    private void PlaceWeightObjectsOnPan()
    {
        if (weightObjects == null || weightGramValues == null) return;

        // Base localPosition in timbanganNeraca space (above right pan pivot)
        Vector3 basePos = rightPanParent != null
            ? rightPanParent.localPosition + new Vector3(0f, PlaceYOffset, 0f)
            : new Vector3(0.039f, 0.483f, 0f);

        float stackY = 0f;

        for (int si = 0; si < Denominations.Length; si++)
        {
            if (!_selected[si]) continue;
            float denom = Denominations[si].grams;

            int len = Mathf.Min(weightObjects.Length, weightGramValues.Length);
            for (int wi = 0; wi < len; wi++)
            {
                if (Mathf.Abs(weightGramValues[wi] - denom) >= 0.0001f) continue;
                if (weightObjects[wi] == null) continue;

                GameObject w = weightObjects[wi];
                w.SetActive(true);

                if (reparentToRightPan && rightPanParent != null)
                {
                    // Parent to the right pan so the weight tilts with the beam.
                    w.transform.SetParent(rightPanParent, false);
                    w.transform.localPosition = new Vector3(0f, PlaceYOffset + stackY, 0f);
                }
                else
                {
                    w.transform.localPosition = basePos + new Vector3(0f, stackY, 0f);
                }

                w.transform.localRotation = Quaternion.identity;
                stackY += StackStep;

                // Kinematic so it stays exactly where placed;
                // isTrigger=false so it's a solid object players can interact with.
                if (w.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
                if (w.TryGetComponent<BoxCollider>(out var col))
                {
                    col.isTrigger = false;
                    col.enabled   = true;
                }
                break; // One physical object per denomination
            }
        }
    }

    /// <summary>
    /// Restores all weight objects to their original local transform + physics state.
    /// </summary>
    private void ResetWeightObjectsToHome()
    {
        if (weightObjects == null) return;

        for (int i = 0; i < weightObjects.Length; i++)
        {
            var w = weightObjects[i];
            if (w == null) continue;

            if (_originalStates != null && i < _originalStates.Length)
            {
                // Restore original parent before restoring local transform.
                Transform origParent = _originalStates[i].originalParent;
                if (reparentToRightPan && origParent != null
                    && w.transform.parent != origParent)
                {
                    w.transform.SetParent(origParent, false);
                }

                w.transform.localPosition = _originalStates[i].localPosition;
                w.transform.localRotation = _originalStates[i].localRotation;

                if (w.TryGetComponent<Rigidbody>(out var rb))
                    rb.isKinematic = _originalStates[i].wasKinematic;

                if (w.TryGetComponent<BoxCollider>(out var col))
                {
                    col.isTrigger = _originalStates[i].wasTrigger;
                    col.enabled   = true;
                }
            }

            if (hideWeightsOnReset)
                w.SetActive(false);
            else if (!w.activeSelf)
                w.SetActive(true);
        }
    }

    // ── Utility ─────────────────────────────────────────────────────────────

    private bool HasPhysicalObject(float grams)
    {
        if (weightGramValues == null) return false;
        foreach (float g in weightGramValues)
            if (Mathf.Abs(g - grams) < 0.0001f) return true;
        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (weightObjects != null && weightGramValues != null
            && weightObjects.Length != weightGramValues.Length)
        {
            Debug.LogWarning("[VirtualWeightSelector] weightObjects and weightGramValues length mismatch.", this);
        }
    }
#endif
}
