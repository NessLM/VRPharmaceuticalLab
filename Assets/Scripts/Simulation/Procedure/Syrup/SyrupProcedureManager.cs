using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SyrupProcedureManager : MonoBehaviour
{
    private enum SyrupStep
    {
        Step_01_MeasureWater100ml,
        Step_02_Placeholder,
        Done
    }

    [Header("Current Step")]
    [SerializeField] private SyrupStep currentStep;

    [Header("Step 01 - Measure Water 100 ml")]
    [SerializeField] private LiquidContainer gelasUkurContainer;
    [SerializeField] private float targetWaterMl = 100f;
    [SerializeField] private float toleranceMl = 2f;
    [SerializeField] private float stableRequiredTime = 0.5f;

    [Header("Main UI")]
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private GameObject doneIcon;

    [Header("Left Checklist UI")]
    [SerializeField] private TMP_Text checklistStep1Text;
    [SerializeField] private TMP_Text checklistStep2Text;
    [SerializeField] private RectTransform strikeStep1Line;
    [SerializeField] private float strikeLineTargetWidth = 520f;
    [SerializeField] private float strikeAnimationDuration = 0.45f;

    [Header("Layout")]
    [SerializeField] private RectTransform stepCanvasRoot;
    [SerializeField] private RectTransform checklistPanel;
    [SerializeField] private bool forceUILayout = true;

    [Header("Highlights")]
    [SerializeField] private GameObject highlightGelasUkur100ml;
    [SerializeField] private GameObject highlightWasher;
    [SerializeField] private Transform washerHighlightTarget;
    [SerializeField] private bool showWasherHighlight = true;

    private static readonly Vector2 InstructionPosition = new Vector2(0f, -42f);
    private static readonly Vector2 InstructionSize = new Vector2(1320f, 96f);
    private static readonly Vector2 ProgressPosition = new Vector2(0f, -124f);
    private static readonly Vector2 ProgressSize = new Vector2(980f, 42f);
    private static readonly Vector2 DoneIconPosition = new Vector2(0f, -168f);
    private static readonly Vector2 DoneIconSize = new Vector2(56f, 48f);
    private static readonly Vector2 ChecklistPanelPosition = new Vector2(48f, 116f);
    private static readonly Vector2 ChecklistPanelSize = new Vector2(760f, 132f);
    private static readonly Vector2 ChecklistStep1ActivePosition = new Vector2(0f, 36f);
    private static readonly Vector2 ChecklistStep1DonePosition = new Vector2(0f, 78f);
    private static readonly Vector2 ChecklistStep2HiddenPosition = new Vector2(0f, -24f);
    private static readonly Vector2 ChecklistStep2ActivePosition = new Vector2(0f, 36f);
    private static readonly Vector2 ChecklistStepSize = new Vector2(720f, 36f);

    private float stableTimer;
    private bool stepDone;
    private bool isAnimating;

    private void OnEnable()
    {
        BeginSyrupProcedure();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        SetHighlightActive(highlightGelasUkur100ml, false, null);
        SetHighlightActive(highlightWasher, false, null);
    }

    private void Update()
    {
        if (isAnimating)
            return;

        if (currentStep == SyrupStep.Step_01_MeasureWater100ml)
            CheckStep01MeasureWater100ml();
    }

    public void BeginSyrupProcedure()
    {
        StopAllCoroutines();
        ResolveSceneReferences();

        currentStep = SyrupStep.Step_01_MeasureWater100ml;
        stepDone = false;
        stableTimer = 0f;
        isAnimating = false;

        if (forceUILayout)
            ApplyUILayout();

        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(true);
            instructionText.text = "Step 1: Isi Gelas Ukur";
        }

        if (progressText != null)
        {
            progressText.gameObject.SetActive(true);
            progressText.text = $"Isi dengan aquadest dari Washer sampai {targetWaterMl:0} ml.";
        }

        if (doneIcon != null)
            doneIcon.SetActive(false);

        SetupChecklist();
        SetHighlightActive(highlightGelasUkur100ml, true, gelasUkurContainer != null ? gelasUkurContainer.transform : null);
        SetHighlightActive(highlightWasher, showWasherHighlight, ResolveWasherHighlightTarget());

        Debug.Log("[SyrupProcedure] Step 1 started.");
    }

    private void SetupChecklist()
    {
        if (checklistStep1Text != null)
        {
            checklistStep1Text.gameObject.SetActive(true);
            checklistStep1Text.text = $"Step 1  Isi Gelas Ukur sampai {targetWaterMl:0} ml";
            checklistStep1Text.fontStyle = FontStyles.Normal;
            SetTextAlpha(checklistStep1Text, 1f);
            SetAnchoredPosition(checklistStep1Text.rectTransform, ChecklistStep1ActivePosition);
        }

        if (checklistStep2Text != null)
        {
            checklistStep2Text.text = "Step 2  Lanjutkan tahap berikutnya";
            checklistStep2Text.fontStyle = FontStyles.Normal;
            SetTextAlpha(checklistStep2Text, 0f);
            SetAnchoredPosition(checklistStep2Text.rectTransform, ChecklistStep2HiddenPosition);
            checklistStep2Text.gameObject.SetActive(false);
        }

        if (strikeStep1Line != null)
        {
            strikeStep1Line.gameObject.SetActive(false);
            SetAnchoredPosition(strikeStep1Line, GetStrikePosition(ChecklistStep1ActivePosition));
            SetSize(strikeStep1Line, 0f, 3f);
        }
    }

    private void CheckStep01MeasureWater100ml()
    {
        if (stepDone)
            return;

        if (gelasUkurContainer == null)
        {
            if (progressText != null)
                progressText.text = "Gelas Ukur belum tersambung.";
            return;
        }

        if (!TryReadCurrentMl(gelasUkurContainer, out float currentVolume))
        {
            if (progressText != null)
                progressText.text = "Volume Gelas Ukur belum terbaca.";
            return;
        }

        if (progressText != null)
            progressText.text = $"Air di Gelas Ukur: {currentVolume:0.0} / {targetWaterMl:0} ml";

        bool volumeReached = currentVolume >= targetWaterMl - toleranceMl;

        if (volumeReached)
        {
            stableTimer += Time.deltaTime;

            if (stableTimer >= stableRequiredTime)
                CompleteStep01();
        }
        else
        {
            stableTimer = 0f;
        }
    }

    private void CompleteStep01()
    {
        if (stepDone)
            return;

        stepDone = true;

        if (progressText != null)
            progressText.text = "Step 1 selesai.";

        if (doneIcon != null)
            doneIcon.SetActive(true);

        SetHighlightActive(highlightGelasUkur100ml, false, null);
        SetHighlightActive(highlightWasher, false, null);

        StartCoroutine(AnimateStep1CompleteThenShowStep2());

        Debug.Log("[SyrupProcedure] Step 1 complete.");
    }

    private IEnumerator AnimateStep1CompleteThenShowStep2()
    {
        isAnimating = true;

        if (instructionText != null)
            instructionText.text = "Step 2: Lanjutkan tahap berikutnya";

        if (checklistStep2Text != null)
        {
            checklistStep2Text.gameObject.SetActive(true);
            SetTextAlpha(checklistStep2Text, 0f);
            SetAnchoredPosition(checklistStep2Text.rectTransform, ChecklistStep2HiddenPosition);
        }

        if (strikeStep1Line != null)
        {
            strikeStep1Line.gameObject.SetActive(true);
            SetSize(strikeStep1Line, 0f, 3f);
        }

        float timer = 0f;
        float duration = Mathf.Max(0.01f, strikeAnimationDuration);

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / duration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            Vector2 step1Position = Vector2.Lerp(ChecklistStep1ActivePosition, ChecklistStep1DonePosition, smooth);
            Vector2 step2Position = Vector2.Lerp(ChecklistStep2HiddenPosition, ChecklistStep2ActivePosition, smooth);

            if (checklistStep1Text != null)
                SetAnchoredPosition(checklistStep1Text.rectTransform, step1Position);

            if (checklistStep2Text != null)
            {
                SetAnchoredPosition(checklistStep2Text.rectTransform, step2Position);
                SetTextAlpha(checklistStep2Text, smooth);
            }

            if (strikeStep1Line != null)
            {
                SetAnchoredPosition(strikeStep1Line, GetStrikePosition(step1Position));
                SetSize(strikeStep1Line, Mathf.Lerp(0f, strikeLineTargetWidth, smooth), 3f);
            }

            yield return null;
        }

        if (checklistStep1Text != null)
        {
            checklistStep1Text.fontStyle = FontStyles.Strikethrough;
            SetAnchoredPosition(checklistStep1Text.rectTransform, ChecklistStep1DonePosition);
        }

        if (checklistStep2Text != null)
        {
            SetTextAlpha(checklistStep2Text, 1f);
            SetAnchoredPosition(checklistStep2Text.rectTransform, ChecklistStep2ActivePosition);
        }

        if (strikeStep1Line != null)
        {
            SetAnchoredPosition(strikeStep1Line, GetStrikePosition(ChecklistStep1DonePosition));
            SetSize(strikeStep1Line, strikeLineTargetWidth, 3f);
        }

        yield return new WaitForSeconds(0.15f);

        currentStep = SyrupStep.Step_02_Placeholder;

        if (progressText != null)
            progressText.text = "Menunggu aksi Step 2.";

        if (doneIcon != null)
            doneIcon.SetActive(false);

        isAnimating = false;
    }

    private void ResolveSceneReferences()
    {
        if (instructionText == null)
            instructionText = FindSceneComponentByName<TMP_Text>("TXT_SyrupInstruction");

        if (progressText == null)
            progressText = FindSceneComponentByName<TMP_Text>("TXT_SyrupProgress");

        if (doneIcon == null)
            doneIcon = FindSceneObjectByName("IMG_SyrupDoneIcon");

        if (checklistStep1Text == null)
            checklistStep1Text = FindSceneComponentByName<TMP_Text>("TXT_CheckStep1");

        if (checklistStep2Text == null)
            checklistStep2Text = FindSceneComponentByName<TMP_Text>("TXT_CheckStep2");

        if (strikeStep1Line == null)
        {
            GameObject strikeObject = FindSceneObjectByName("IMG_StrikeStep1");
            if (strikeObject != null)
                strikeStep1Line = strikeObject.transform as RectTransform;
        }

        if (stepCanvasRoot == null && instructionText != null && instructionText.canvas != null)
            stepCanvasRoot = instructionText.canvas.transform as RectTransform;

        if (checklistPanel == null && checklistStep1Text != null)
            checklistPanel = checklistStep1Text.transform.parent as RectTransform;

        if (highlightGelasUkur100ml == null)
            highlightGelasUkur100ml = FindSceneObjectByName("HL_GelasUkur100ml");

        if (highlightWasher == null)
            highlightWasher = FindSceneObjectByName("HL_Washer");
    }

    private void ApplyUILayout()
    {
        Canvas canvas = stepCanvasRoot != null ? stepCanvasRoot.GetComponent<Canvas>() : null;
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
        }

        CanvasScaler scaler = stepCanvasRoot != null ? stepCanvasRoot.GetComponent<CanvasScaler>() : null;
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        ConfigureTopText(instructionText, InstructionPosition, InstructionSize, 42f, FontStyles.Bold);
        ConfigureTopText(progressText, ProgressPosition, ProgressSize, 24f, FontStyles.Normal);
        ConfigureRect(doneIcon != null ? doneIcon.transform as RectTransform : null, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), DoneIconPosition, DoneIconSize);

        if (checklistPanel != null)
        {
            checklistPanel.localScale = Vector3.one;
            ConfigureRect(checklistPanel, Vector2.zero, Vector2.zero, Vector2.zero, ChecklistPanelPosition, ChecklistPanelSize);
        }

        ConfigureChecklistText(checklistStep1Text);
        ConfigureChecklistText(checklistStep2Text);

        if (strikeStep1Line != null)
        {
            strikeStep1Line.localScale = Vector3.one;
            ConfigureRect(strikeStep1Line, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), GetStrikePosition(ChecklistStep1ActivePosition), new Vector2(0f, 3f));

            Image strikeImage = strikeStep1Line.GetComponent<Image>();
            if (strikeImage != null)
            {
                strikeImage.raycastTarget = false;
                strikeImage.color = new Color(1f, 0.9f, 0.16f, 0.95f);
            }
        }
    }

    private void ConfigureTopText(TMP_Text text, Vector2 position, Vector2 size, float fontSize, FontStyles style)
    {
        if (text == null)
            return;

        text.raycastTarget = false;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = Color.white;
        text.rectTransform.localScale = Vector3.one;

        ConfigureRect(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, size);
    }

    private void ConfigureChecklistText(TMP_Text text)
    {
        if (text == null)
            return;

        text.raycastTarget = false;
        text.fontSize = 24f;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = Color.white;
        text.rectTransform.localScale = Vector3.one;

        ConfigureRect(text.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), ChecklistStep1ActivePosition, ChecklistStepSize);
    }

    private void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private Vector2 GetStrikePosition(Vector2 stepPosition)
    {
        return new Vector2(stepPosition.x, stepPosition.y - 1f);
    }

    private void SetHighlightActive(GameObject highlightObject, bool active, Transform target)
    {
        if (highlightObject == null)
            return;

        if (!active || target == null)
        {
            highlightObject.SetActive(false);
            return;
        }

        highlightObject.SetActive(true);

        ProcedureHighlightRing ring = highlightObject.GetComponent<ProcedureHighlightRing>();
        if (ring == null)
            ring = highlightObject.AddComponent<ProcedureHighlightRing>();

        float radiusMultiplier = target == gelasUkurContainer?.transform ? 1.75f : 1.25f;
        float yOffset = target == gelasUkurContainer?.transform ? 0.035f : 0.04f;
        ring.Configure(target, Color.yellow, radiusMultiplier, yOffset, 0.018f);
    }

    private Transform ResolveWasherHighlightTarget()
    {
        if (!showWasherHighlight)
            return null;

        if (washerHighlightTarget != null)
            return washerHighlightTarget;

        WasherWaterController[] washerControllers = FindObjectsByType<WasherWaterController>(FindObjectsSortMode.None);
        if (washerControllers != null && washerControllers.Length > 0)
            return washerControllers[0].transform;

        GameObject washerObject = FindSceneObjectByName("Washer1");
        if (washerObject != null)
            return washerObject.transform;

        washerObject = FindSceneObjectByName("Washer2");
        return washerObject != null ? washerObject.transform : null;
    }

    private void SetAnchoredPosition(RectTransform rect, Vector2 position)
    {
        if (rect != null)
            rect.anchoredPosition = position;
    }

    private void SetSize(RectTransform rect, float width, float height)
    {
        if (rect == null)
            return;

        Vector2 size = rect.sizeDelta;
        size.x = width;
        size.y = height;
        rect.sizeDelta = size;
    }

    private void SetTextAlpha(TMP_Text text, float alpha)
    {
        if (text == null)
            return;

        Color color = text.color;
        color.a = Mathf.Clamp01(alpha);
        text.color = color;
    }

    private T FindSceneComponentByName<T>(string objectName) where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();

        foreach (T component in components)
        {
            if (component == null || component.gameObject == null)
                continue;

            if (!component.gameObject.scene.IsValid())
                continue;

            if (component.name == objectName)
                return component;
        }

        return null;
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject sceneObject in objects)
        {
            if (sceneObject == null)
                continue;

            if (!sceneObject.scene.IsValid())
                continue;

            if (sceneObject.name == objectName)
                return sceneObject;
        }

        return null;
    }

    private bool TryReadCurrentMl(LiquidContainer container, out float value)
    {
        value = 0f;

        if (container == null)
            return false;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        string[] names =
        {
            "CurrentMl",
            "currentMl",
            "currentML",
            "CurrentML",
            "CurrentVolumeMl",
            "currentVolumeMl",
            "VolumeMl",
            "volumeMl"
        };

        System.Type type = container.GetType();

        foreach (string name in names)
        {
            FieldInfo field = type.GetField(name, flags);
            if (field != null)
                return ConvertToFloat(field.GetValue(container), out value);

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.CanRead)
                return ConvertToFloat(property.GetValue(container), out value);
        }

        return false;
    }

    private bool ConvertToFloat(object raw, out float value)
    {
        value = 0f;

        if (raw is float f)
        {
            value = f;
            return true;
        }

        if (raw is int i)
        {
            value = i;
            return true;
        }

        if (raw is double d)
        {
            value = (float)d;
            return true;
        }

        return false;
    }
}
