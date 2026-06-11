using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EPOOutline;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SyrupProcedureManager : MonoBehaviour
{
    private enum SyrupStep
    {
        Step_01_MeasureWater100ml,
        Step_02_PlaceParchmentOnScale,
        Step_03_Default,
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

    [Header("Outline Highlights")]
    [SerializeField] private Outlinable gelasUkurOutline;
    [SerializeField] private Outlinable washerOutline;
    [SerializeField] private Outlinable waterSwitchOutline;
    [SerializeField] private bool showWasherHighlight = true;

    [Header("Step 1 Pointer Arrows")]
    [SerializeField] private Transform waterSwitchTarget;
    [SerializeField] private WorldStepArrow gelasUkurStepArrow;
    [SerializeField] private WorldStepArrow washerStepArrow;
    [SerializeField] private bool useStepArrowPointer = true;

    [Header("Step 2 Guide Prep")]
    [SerializeField] private bool prepareStep2Guidance = true;
    [SerializeField] private Transform perkamenStackTarget;
    [SerializeField] private Transform timbanganTarget;
    [SerializeField] private Outlinable perkamenStackOutline;
    [SerializeField] private Outlinable timbanganOutline;
    [SerializeField] private WorldStepArrow perkamenStepArrow;
    [SerializeField] private WorldStepArrow timbanganStepArrow;

    [Header("Step 2 Perkamen Snap")]
    [SerializeField] private bool setupPerkamenSnapTargets = true;
    [SerializeField] private SyrupPerkamenSnapTarget leftPerkamenSnapTarget;
    [SerializeField] private SyrupPerkamenSnapTarget rightPerkamenSnapTarget;
    [SerializeField] private Vector3 perkamenSnapWorldOffset = new Vector3(0f, 0.025f, 0f);
    [SerializeField] private Vector3 perkamenSnapTriggerSize = new Vector3(0.24f, 0.14f, 0.24f);

    private const string Step01Instruction = "Step 1: Isi gelas ukur sampai 100 ml";
    private const string Step01StartProgress = "Tekan tombol air merah, lalu arahkan gelas ukur ke aliran air.";
    private const string Step01GelasArrowLabel = "\u2193\nGelas ukur\nIsi sampai 100 ml";
    private const string Step01WasherArrowLabel = "\u2193\nTombol air\nTekan untuk menyalakan";
    private const string Step02Instruction = "Step 2: Siapkan kertas perkamen";
    private const string Step02Progress = "Ambil dua kertas perkamen, lalu lepaskan di piring kiri dan kanan neraca sampai tersnap.";
    private const string Step02PerkamenArrowLabel = "\u2193\nAmbil kertas\nperkamen";
    private const string Step02TimbanganArrowLabel = "\u2193\nLetakkan di\npiring neraca";
    private const string Step03Instruction = "Step 3: Lanjutkan tahap berikutnya";
    private const string Step03Progress = "Tahap berikutnya belum disambungkan.";
    private const string TopBackdropName = "IMG_SyrupTopInstructionBackdrop";
    private static readonly string[] ExcludedOutlineNameParts =
    {
        "WaterSpawnPoint",
        "WaterHitZone",
        "WaterStream",
        "WaterFlow",
        "WaterParticle",
        "WaterVisual",
        "Liquid",
        "LiquidSpace",
        "FillZone",
        "PourPoint"
    };

    private static readonly Vector2 TopBackdropPosition = new Vector2(0f, -28f);
    private static readonly Vector2 TopBackdropSize = new Vector2(1200f, 154f);
    private static readonly Vector2 InstructionPosition = new Vector2(0f, -38f);
    private static readonly Vector2 InstructionSize = new Vector2(1160f, 60f);
    private static readonly Vector2 ProgressPosition = new Vector2(0f, -100f);
    private static readonly Vector2 ProgressSize = new Vector2(1080f, 58f);
    private static readonly Vector2 DoneIconPosition = new Vector2(0f, -158f);
    private static readonly Vector2 DoneIconSize = new Vector2(56f, 48f);
    private static readonly Vector2 ChecklistPanelPosition = new Vector2(48f, 116f);
    private static readonly Vector2 ChecklistPanelSize = new Vector2(840f, 148f);
    private static readonly Vector2 ChecklistStep1ActivePosition = new Vector2(0f, 36f);
    private static readonly Vector2 ChecklistStep1DonePosition = new Vector2(0f, 78f);
    private static readonly Vector2 ChecklistStep2HiddenPosition = new Vector2(0f, -24f);
    private static readonly Vector2 ChecklistStep2ActivePosition = new Vector2(0f, 36f);
    private static readonly Vector2 ChecklistStepSize = new Vector2(800f, 42f);
    private static readonly Vector3 Step01GelasArrowOffset = new Vector3(0f, 0.72f, 0f);
    private static readonly Vector3 Step01WasherArrowOffset = new Vector3(0f, 0.32f, 0f);
    private static readonly Vector3 Step02PerkamenArrowOffset = new Vector3(0f, 0.35f, 0f);
    private static readonly Vector3 Step02TimbanganArrowOffset = new Vector3(0f, 0.55f, 0f);
    private static readonly Color ProcedureHighlightColor = new Color(1f, 0.92f, 0.02f, 1f);

    private float stableTimer;
    private bool stepDone;
    private bool isAnimating;
    private readonly Dictionary<Outlinable, bool> outlinePreviousStates = new Dictionary<Outlinable, bool>();

    private void OnEnable()
    {
        ResolveSceneReferences();
        ForceDisableProcedureOutlines();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ForceDisableProcedureOutlines();
    }
    private void ForceDisableProcedureOutlines()
    {
        SetProcedureOutlineOff(gelasUkurOutline);
        SetProcedureOutlineOff(washerOutline);
        SetProcedureOutlineOff(waterSwitchOutline);
        SetProcedureOutlineOff(perkamenStackOutline);
        SetProcedureOutlineOff(timbanganOutline);

        foreach (KeyValuePair<Outlinable, bool> entry in outlinePreviousStates)
            SetProcedureOutlineOff(entry.Key);

        SetStep1ArrowsActive(false);
        SetStep2ArrowsActive(false);
        SetPerkamenSnapTargetsActive(false);

        outlinePreviousStates.Clear();
    }

    private void SetProcedureOutlineOff(Outlinable outlinable)
    {
        if (outlinable != null)
            outlinable.enabled = false;
    }

    private void SetStep1ArrowsActive(bool active)
    {
        bool finalActive = active && useStepArrowPointer;

        Transform gelasTarget = gelasUkurContainer != null ? gelasUkurContainer.transform : null;
        SetGuideArrow(ref gelasUkurStepArrow, "ARW_Step1_GelasUkur", gelasTarget, Step01GelasArrowLabel, Step01GelasArrowOffset, finalActive);

        Transform switchTarget = waterSwitchTarget != null
            ? waterSwitchTarget
            : (washerOutline != null ? washerOutline.transform : null);

        SetGuideArrow(ref washerStepArrow, "ARW_Step1_Washer", switchTarget, Step01WasherArrowLabel, Step01WasherArrowOffset, finalActive && showWasherHighlight);
    }

    private void SetStep2ArrowsActive(bool active)
    {
        bool finalActive = active && useStepArrowPointer && prepareStep2Guidance;
        Transform timbanganArrowTarget = timbanganTarget;

        if (leftPerkamenSnapTarget != null && leftPerkamenSnapTarget.isActiveAndEnabled)
            timbanganArrowTarget = leftPerkamenSnapTarget.transform;
        else if (rightPerkamenSnapTarget != null && rightPerkamenSnapTarget.isActiveAndEnabled)
            timbanganArrowTarget = rightPerkamenSnapTarget.transform;

        SetGuideArrow(ref perkamenStepArrow, "ARW_Step2_Perkamen", perkamenStackTarget, Step02PerkamenArrowLabel, Step02PerkamenArrowOffset, finalActive);
        SetGuideArrow(ref timbanganStepArrow, "ARW_Step2_Timbangan", timbanganArrowTarget, Step02TimbanganArrowLabel, Step02TimbanganArrowOffset, finalActive);
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
        ClearProcedureOutlines();
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
            instructionText.text = Step01Instruction;
        }

        if (progressText != null)
        {
            progressText.gameObject.SetActive(true);
            progressText.text = Step01StartProgress;
        }

        if (doneIcon != null)
            doneIcon.SetActive(false);

        SetupChecklist();
        SetProcedureOutlineActive(gelasUkurOutline, true);
        SetProcedureOutlineActive(GetActiveWaterSwitchOutline(), showWasherHighlight);
        SetStep1ArrowsActive(true);

        Debug.Log("[SyrupProcedure] Step 1 started.");
    }


    public void ShowDefaultStep03()
    {
        StopAllCoroutines();
        ClearProcedureOutlines();
        ResolveSceneReferences();

        currentStep = SyrupStep.Step_03_Default;
        stepDone = false;
        stableTimer = 0f;
        isAnimating = false;

        if (forceUILayout)
            ApplyUILayout();

        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(true);
            instructionText.text = Step03Instruction;
        }

        if (progressText != null)
        {
            progressText.gameObject.SetActive(true);
            progressText.text = Step03Progress;
        }

        if (doneIcon != null)
            doneIcon.SetActive(false);
    }

    private void SetupChecklist()
    {
        if (checklistStep1Text != null)
        {
            checklistStep1Text.gameObject.SetActive(true);
            checklistStep1Text.text = $"Step 1 - Isi gelas ukur sampai {targetWaterMl:0} ml";
            checklistStep1Text.fontStyle = FontStyles.Normal;
            SetTextAlpha(checklistStep1Text, 1f);
            SetAnchoredPosition(checklistStep1Text.rectTransform, ChecklistStep1ActivePosition);
        }

        if (checklistStep2Text != null)
        {
            checklistStep2Text.text = "Step 2 - Snap perkamen di piring kiri dan kanan neraca";
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
                progressText.text = "Gelas ukur belum tersambung ke sistem volume.";
            return;
        }

        if (!TryReadCurrentMl(gelasUkurContainer, out float currentVolume))
        {
            if (progressText != null)
                progressText.text = "Volume gelas ukur belum terbaca.";
            return;
        }

        if (progressText != null)
            progressText.text = $"Air di gelas ukur: {currentVolume:0.0} / {targetWaterMl:0} ml. Isi sampai garis {targetWaterMl:0} ml.";

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
            progressText.text = "Step 1 selesai. Volume air sudah pas.";

        if (doneIcon != null)
            doneIcon.SetActive(true);

        ClearProcedureOutlines();

        StartCoroutine(AnimateStep1CompleteThenShowStep2());

        Debug.Log("[SyrupProcedure] Step 1 complete.");
    }

    private IEnumerator AnimateStep1CompleteThenShowStep2()
    {
        isAnimating = true;

        if (instructionText != null)
            instructionText.text = Step02Instruction;

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

        currentStep = SyrupStep.Step_02_PlaceParchmentOnScale;

        if (progressText != null)
            progressText.text = Step02Progress;

        if (doneIcon != null)
            doneIcon.SetActive(false);

        SetStep2GuidanceActive(true);

        isAnimating = false;
    }

    private void SetStep2GuidanceActive(bool active)
    {
        bool finalActive = active && prepareStep2Guidance;

        if (finalActive)
        {
            ResolveSceneReferences();
            EnsurePerkamenSnapTargets();
            SetProcedureOutlineActive(perkamenStackOutline, true);
            SetProcedureOutlineActive(timbanganOutline, true);
        }
        else
        {
            SetPerkamenSnapTargetsActive(false);
        }

        SetStep2ArrowsActive(finalActive);
    }

    private void EnsurePerkamenSnapTargets()
    {
        if (!setupPerkamenSnapTargets)
        {
            SetPerkamenSnapTargetsActive(false);
            return;
        }

        Transform timbanganRoot = timbanganTarget;

        if (timbanganRoot == null)
        {
            GameObject timbanganObject = FindSceneObjectByName("timbanganNeraca");
            if (timbanganObject != null)
                timbanganRoot = timbanganObject.transform;
        }

        if (timbanganRoot == null)
            return;

        Transform leftPan = FindDeepChild(timbanganRoot, "Balance_WeightLeft");
        Transform rightPan = FindDeepChild(timbanganRoot, "Balance_WeightRight");

        if (leftPan != null)
            leftPerkamenSnapTarget = EnsurePerkamenSnapTarget(leftPerkamenSnapTarget, "SYS_Snap_Perkamen_Left", leftPan);

        if (rightPan != null)
            rightPerkamenSnapTarget = EnsurePerkamenSnapTarget(rightPerkamenSnapTarget, "SYS_Snap_Perkamen_Right", rightPan);
    }

    private SyrupPerkamenSnapTarget EnsurePerkamenSnapTarget(SyrupPerkamenSnapTarget snapTarget, string objectName, Transform pan)
    {
        if (snapTarget == null)
            snapTarget = FindPerkamenSnapTarget(objectName);

        if (snapTarget == null)
        {
            Debug.LogWarning($"[SyrupProcedure] Snap target '{objectName}' belum ada di scene. Buat object dengan SyrupPerkamenSnapTarget agar Step 2 bisa diedit dari hierarchy.", this);
            return null;
        }

        snapTarget.Configure(pan, perkamenSnapWorldOffset, perkamenSnapTriggerSize);
        return snapTarget;
    }

    private SyrupPerkamenSnapTarget FindPerkamenSnapTarget(string objectName)
    {
        Transform child = FindDeepChild(transform, objectName);
        if (child != null && child.TryGetComponent(out SyrupPerkamenSnapTarget snapTarget))
            return snapTarget;

        return FindSceneComponentByName<SyrupPerkamenSnapTarget>(objectName);
    }

    private void SetPerkamenSnapTargetsActive(bool active)
    {
        if (leftPerkamenSnapTarget != null)
            leftPerkamenSnapTarget.gameObject.SetActive(active);

        if (rightPerkamenSnapTarget != null)
            rightPerkamenSnapTarget.gameObject.SetActive(active);
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

        if (gelasUkurOutline == null && gelasUkurContainer != null)
            gelasUkurOutline = gelasUkurContainer.GetComponent<Outlinable>();

        if (waterSwitchTarget == null)
            waterSwitchTarget = FindWaterSwitchTarget();

        if (waterSwitchOutline == null && waterSwitchTarget != null)
            waterSwitchOutline = waterSwitchTarget.GetComponent<Outlinable>();

        if (washerOutline == null)
        {
            GameObject washerObject = FindSceneObjectByName("Washer_Right");
            if (washerObject == null)
                washerObject = FindSceneObjectByName("Washer1");
            if (washerObject != null)
                washerOutline = washerObject.GetComponent<Outlinable>();
        }

        if (perkamenStackTarget == null)
        {
            GameObject perkamenObject = FindSceneObjectByName("stackperkamen");
            if (perkamenObject != null)
                perkamenStackTarget = perkamenObject.transform;
        }

        if (perkamenStackOutline == null && perkamenStackTarget != null)
            perkamenStackOutline = perkamenStackTarget.GetComponent<Outlinable>();

        if (timbanganTarget == null)
        {
            GameObject timbanganObject = FindSceneObjectByName("timbanganNeraca");
            if (timbanganObject != null)
                timbanganTarget = timbanganObject.transform;
        }

        if (timbanganOutline == null && timbanganTarget != null)
            timbanganOutline = timbanganTarget.GetComponent<Outlinable>();

        if (gelasUkurStepArrow == null)
            gelasUkurStepArrow = FindSceneComponentByName<WorldStepArrow>("ARW_Step1_GelasUkur");

        if (washerStepArrow == null)
            washerStepArrow = FindSceneComponentByName<WorldStepArrow>("ARW_Step1_Washer");

        if (perkamenStepArrow == null)
            perkamenStepArrow = FindSceneComponentByName<WorldStepArrow>("ARW_Step2_Perkamen");

        if (timbanganStepArrow == null)
            timbanganStepArrow = FindSceneComponentByName<WorldStepArrow>("ARW_Step2_Timbangan");
    }

    private void SetGuideArrow(ref WorldStepArrow arrow, string objectName, Transform target, string label, Vector3 offset, bool active)
    {
        if (arrow == null)
            arrow = FindSceneComponentByName<WorldStepArrow>(objectName);

        if (arrow == null)
        {
            if (active)
                Debug.LogWarning($"[SyrupProcedure] Arrow '{objectName}' belum ada di scene. Buat object WorldStepArrow agar panduan bisa diedit dari hierarchy.", this);

            return;
        }

        if (!active || target == null)
        {
            arrow.SetVisible(false);
            return;
        }

        arrow.Configure(target, label, offset);
        arrow.SetVisible(true);
    }

    private Transform FindWaterSwitchTarget()
    {
        WorldStepArrow sceneWasherArrow = washerStepArrow != null
            ? washerStepArrow
            : FindSceneComponentByName<WorldStepArrow>("ARW_Step1_Washer");

        if (sceneWasherArrow != null && sceneWasherArrow.Target != null)
            return sceneWasherArrow.Target;

        WasherRedBallSwitch[] switches = Resources.FindObjectsOfTypeAll<WasherRedBallSwitch>();
        Transform fallback = null;

        foreach (WasherRedBallSwitch waterSwitch in switches)
        {
            if (waterSwitch == null || waterSwitch.gameObject == null)
                continue;

            if (!waterSwitch.gameObject.scene.IsValid())
                continue;

            if (IsInNamedParent(waterSwitch.transform, "Washer1"))
                return waterSwitch.transform;

            if (fallback == null)
                fallback = waterSwitch.transform;
        }

        return fallback;
    }

    private bool IsInNamedParent(Transform targetTransform, string parentName)
    {
        Transform current = targetTransform;

        while (current != null)
        {
            if (string.Equals(current.name, parentName, StringComparison.OrdinalIgnoreCase))
                return true;

            current = current.parent;
        }

        return false;
    }

    private Outlinable GetActiveWaterSwitchOutline()
    {
        return waterSwitchOutline != null ? waterSwitchOutline : washerOutline;
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

        EnsureTopBackdrop();
        ConfigureTopText(instructionText, InstructionPosition, InstructionSize, 44f, FontStyles.Bold);
        ConfigureTopText(progressText, ProgressPosition, ProgressSize, 28f, FontStyles.Normal);
        ConfigureRect(doneIcon != null ? doneIcon.transform as RectTransform : null, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), DoneIconPosition, DoneIconSize);

        if (checklistPanel != null)
        {
            checklistPanel.localScale = Vector3.one;
            ConfigureRect(checklistPanel, Vector2.zero, Vector2.zero, Vector2.zero, ChecklistPanelPosition, ChecklistPanelSize);

            Image panelImage = checklistPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0.02f, 0.025f, 0.03f, 0.62f);
                panelImage.raycastTarget = false;
            }
        }

        ConfigureChecklistText(checklistStep1Text);
        ConfigureChecklistText(checklistStep2Text);

        if (strikeStep1Line != null)
        {
            strikeStep1Line.localScale = Vector3.one;
            ConfigureRect(strikeStep1Line, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), GetStrikePosition(ChecklistStep1ActivePosition), new Vector2(0f, 3f));

            Image strikeImage = strikeStep1Line.GetComponent<Image>();
            if (strikeImage != null)
                strikeImage.raycastTarget = false;
        }
    }

    private void EnsureTopBackdrop()
    {
        if (stepCanvasRoot == null)
            return;

        Transform existing = stepCanvasRoot.Find(TopBackdropName);
        RectTransform backdrop = existing as RectTransform;

        if (backdrop == null)
        {
            GameObject backdropObject = FindSceneObjectByName(TopBackdropName);
            if (backdropObject != null)
            {
                backdrop = backdropObject.transform as RectTransform;
                if (backdrop != null && backdrop.parent != stepCanvasRoot)
                    backdrop.SetParent(stepCanvasRoot, false);
            }
        }

        if (backdrop == null)
        {
            Debug.LogWarning($"[SyrupProcedure] {TopBackdropName} belum ada di SyrupStepUI. Buat Image backdrop di scene agar layout bisa diedit dari hierarchy.", this);
            return;
        }

        ConfigureRect(backdrop, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), TopBackdropPosition, TopBackdropSize);
        backdrop.SetAsFirstSibling();

        Image image = backdrop.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.02f, 0.025f, 0.03f, 0.68f);
            image.raycastTarget = false;
        }
    }

    private void ConfigureTopText(TMP_Text text, Vector2 position, Vector2 size, float fontSize, FontStyles style)
    {
        if (text == null)
            return;

        text.raycastTarget = false;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(16f, fontSize * 0.65f);
        text.fontSizeMax = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = Color.white;
        text.rectTransform.localScale = Vector3.one;
        ApplyReadableTextEffects(text, true);

        ConfigureRect(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, size);
    }

    private void ConfigureChecklistText(TMP_Text text)
    {
        if (text == null)
            return;

        text.raycastTarget = false;
        text.fontSize = 24f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 18f;
        text.fontSizeMax = 26f;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = Color.white;
        text.rectTransform.localScale = Vector3.one;
        ApplyReadableTextEffects(text, false);

        ConfigureRect(text.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), ChecklistStep1ActivePosition, ChecklistStepSize);
    }

    private void ApplyReadableTextEffects(TMP_Text text, bool strong)
    {
        if (text == null)
            return;

        UnityEngine.UI.Outline outline = text.GetComponent<UnityEngine.UI.Outline>();
        if (outline == null)
        {
            Debug.LogWarning($"[SyrupProcedure] {text.name} belum punya komponen UI Outline. Tambahkan dari scene agar styling teks bisa diedit dari Inspector.", text);
            return;
        }

        outline.effectColor = strong ? new Color(0f, 0f, 0f, 0.9f) : new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = strong ? new Vector2(2f, -2f) : new Vector2(1.25f, -1.25f);
        outline.useGraphicAlpha = false;
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

    private void SetProcedureOutlineActive(Outlinable outlinable, bool active)
    {
        if (!active || outlinable == null)
            return;

        if (outlinable.OutlineTargetsCount == 0)
            PopulateProcedureOutlineTargets(outlinable, outlinable.transform);

        if (!outlinePreviousStates.ContainsKey(outlinable))
            outlinePreviousStates.Add(outlinable, outlinable.enabled);

        ConfigureProcedureOutlineStyle(outlinable);
        outlinable.enabled = true;
    }

    private void ConfigureProcedureOutlineStyle(Outlinable outlinable)
    {
        if (outlinable == null)
            return;

        outlinable.OutlineParameters.Enabled = true;
        outlinable.OutlineParameters.Color = ProcedureHighlightColor;
        outlinable.OutlineParameters.DilateShift = 1f;
        outlinable.OutlineParameters.BlurShift = 1f;

        outlinable.FrontParameters.Enabled = true;
        outlinable.FrontParameters.Color = ProcedureHighlightColor;
        outlinable.FrontParameters.DilateShift = 1f;
        outlinable.FrontParameters.BlurShift = 1f;

        outlinable.BackParameters.Enabled = true;
        outlinable.BackParameters.Color = ProcedureHighlightColor;
        outlinable.BackParameters.DilateShift = 1f;
        outlinable.BackParameters.BlurShift = 1f;
    }

    private void PopulateProcedureOutlineTargets(Outlinable outlinable, Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer childRenderer in renderers)
        {
            if (!IsSupportedProcedureOutlineRenderer(childRenderer))
                continue;

            if (ShouldSkipProcedureOutlineRenderer(childRenderer, root))
                continue;

            outlinable.AddRenderer(childRenderer);
        }

        if (outlinable.OutlineTargetsCount == 0)
            outlinable.AddAllChildRenderersToRenderingList(RenderersAddingMode.MeshRenderer);
    }

    private bool IsSupportedProcedureOutlineRenderer(Renderer rendererToCheck)
    {
        return rendererToCheck is MeshRenderer ||
               rendererToCheck is SkinnedMeshRenderer ||
               rendererToCheck is SpriteRenderer;
    }

    private bool ShouldSkipProcedureOutlineRenderer(Renderer rendererToCheck, Transform root)
    {
        Transform current = rendererToCheck.transform;

        while (current != null)
        {
            for (int i = 0; i < ExcludedOutlineNameParts.Length; i++)
            {
                if (current.name.IndexOf(ExcludedOutlineNameParts[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            if (current == root)
                break;

            current = current.parent;
        }

        return false;
    }

    private void ClearProcedureOutlines()
    {
        ForceDisableProcedureOutlines();
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

    private Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root)
        {
            if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                return child;

            Transform found = FindDeepChild(child, childName);
            if (found != null)
                return found;
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
