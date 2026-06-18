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
        Step_03_WeighPowder,
        Step_04_MovePowderToMortar,
        Done
    }

    [Header("Current Step")]
    [SerializeField] private SyrupStep currentStep;

    [Header("Recipe")]
    [SerializeField] private SyrupRecipeDefinition activeRecipe;

    private float Step3TargetPowderMg => activeRecipe != null ? activeRecipe.targetPowderMg : 250f;
    private float Step3ToleranceMg => activeRecipe != null ? activeRecipe.toleranceMg : 10f;
    private float Step3ScoopStepMg => activeRecipe != null ? activeRecipe.scoopStepMg : 50f;
    private string Step3PowderName => activeRecipe != null ? activeRecipe.powderName : "Difenhidramin";
    private float Step3PowderVisualMaxMg => activeRecipe != null ? activeRecipe.powderVisualMaxMg : Step3TargetPowderMg;

    [Header("Step 01 - Measure Water 100 ml")]
    [SerializeField] private LiquidContainer gelasUkurContainer;
    [SerializeField] private float targetWaterMl = 100f;
    [SerializeField] private float toleranceMl = 2f;
    [SerializeField] private float stableRequiredTime = 0.5f;

    [Header("Main UI")]
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private GameObject doneIcon;

    [Header("Layout")]
    [SerializeField] private RectTransform stepCanvasRoot;
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

    [Header("Balance Zones")]
    [SerializeField] private WeightingZone leftWeighingZone;
    [SerializeField] private WeightingZone rightWeighingZone;

    [Header("Step 3 Weigh Powder")]
    [SerializeField] private PowderDepositZone powderDepositZone;
    [SerializeField] private Transform leftPanTarget;
    [SerializeField] private Transform rightPanTarget;
    [SerializeField] private WorldStepArrow rightWeightStepArrow;
    [SerializeField] private WorldStepArrow leftPowderStepArrow;

    [Header("Step 4 - Move Powder To Mortar")]
    [SerializeField] private MortarController mortarController;
    [SerializeField] private Transform mortarTarget;
    [SerializeField] private Outlinable mortarOutline;
    [SerializeField] private WorldStepArrow mortarStepArrow;
    [SerializeField] private float step4StableRequiredTime = 0.5f;
    [SerializeField] private SpoonPowderPlateTransfer spoonPowderPlateTransfer;

    private const string Step01Instruction = "Step 1: Isi gelas ukur sampai 100 ml";
    private const string Step01StartProgress = "Tekan tombol air merah, lalu arahkan gelas ukur ke aliran air.";
    private const string Step01GelasArrowLabel = "\u2193\nGelas ukur\nIsi sampai 100 ml";
    private const string Step01WasherArrowLabel = "\u2193\nTombol air\nTekan untuk menyalakan";
    private const string Step02Instruction = "Step 2: Siapkan kertas perkamen";
    private const string Step02Progress = "Ambil dua kertas perkamen, lalu lepaskan di piring kiri dan kanan neraca sampai tersnap.";
    private const string Step02PerkamenArrowLabel = "\u2193\nAmbil kertas\nperkamen";
    private const string Step02TimbanganArrowLabel = "\u2193\nLetakkan di\npiring neraca";
    private const string Step03Instruction = "Step 3: Timbang bubuk";
    private const string Step03Progress = "Letakkan anak timbangan di piring kanan, lalu masukkan bubuk Difenhidramin ke piring kiri.";
    private const string Step04Instruction = "Step 4: Masukkan bubuk ke mortar";
    private const string Step04Progress = "Ambil bubuk Difenhidramin yang sudah ditimbang dari piring kiri, lalu masukkan semuanya ke dalam mortar.";
    private const string Step04MortarArrowLabel = "\u2193\nMortar\nMasukkan bubuk";
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
    private static readonly Vector2 TopBackdropSize = new Vector2(1200f, 176f);
    private static readonly Vector2 InstructionPosition = new Vector2(0f, -38f);
    private static readonly Vector2 InstructionSize = new Vector2(1160f, 60f);
    private static readonly Vector2 ProgressPosition = new Vector2(0f, -108f);
    private static readonly Vector2 ProgressSize = new Vector2(1120f, 76f);
    private static readonly Vector2 DoneIconPosition = new Vector2(0f, -176f);
    private static readonly Vector2 DoneIconSize = new Vector2(56f, 48f);
    private static readonly Vector3 Step01GelasArrowOffset = new Vector3(0f, 0.72f, 0f);
    private static readonly Vector3 Step01WasherArrowOffset = new Vector3(0f, 0.32f, 0f);
    private static readonly Vector3 Step02PerkamenArrowOffset = new Vector3(0f, 0.35f, 0f);
    private static readonly Vector3 Step02TimbanganArrowOffset = new Vector3(0f, 0.55f, 0f);
    private static readonly Color ProcedureHighlightColor = new Color(1f, 0.92f, 0.02f, 1f);

    private float stableTimer;
    private bool stepDone;
    private bool isAnimating;
    private readonly Dictionary<Outlinable, bool> outlinePreviousStates = new Dictionary<Outlinable, bool>();

    public bool IsParchmentOnLeft => leftWeighingZone != null && leftWeighingZone.HasParchment;
    public bool IsParchmentOnRight => rightWeighingZone != null && rightWeighingZone.HasParchment;
    public float LeftMass => GetStep3LeftMass();
    public float RightMass => GetStep3RightMass();
    public float DifferenceMass => RightMass - LeftMass;

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
        SetProcedureOutlineOff(mortarOutline);

        foreach (KeyValuePair<Outlinable, bool> entry in outlinePreviousStates)
            SetProcedureOutlineOff(entry.Key);

        SetStep1ArrowsActive(false);
        SetStep2ArrowsActive(false);
        SetStep3ArrowsActive(false);
        SetStep4ArrowsActive(false);

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
        Transform timbanganArrowTarget = leftWeighingZone != null
            ? leftWeighingZone.transform
            : (rightWeighingZone != null ? rightWeighingZone.transform : timbanganTarget);

        SetGuideArrow(ref perkamenStepArrow, "ARW_Step2_Perkamen", perkamenStackTarget, Step02PerkamenArrowLabel, Step02PerkamenArrowOffset, finalActive);
        SetGuideArrow(ref timbanganStepArrow, "ARW_Step2_Timbangan", timbanganArrowTarget, Step02TimbanganArrowLabel, Step02TimbanganArrowOffset, finalActive);
    }

    private void SetStep3ArrowsActive(bool active, bool showRight = true, bool showLeft = true)
    {
        bool finalActive = active && useStepArrowPointer;

        Transform rightTarget = rightPanTarget != null
            ? rightPanTarget
            : (rightWeighingZone != null ? rightWeighingZone.transform : timbanganTarget);

        Transform leftTarget = leftPanTarget != null
            ? leftPanTarget
            : (leftWeighingZone != null ? leftWeighingZone.transform : timbanganTarget);

        SetEditableGuideArrow(ref rightWeightStepArrow, "ARW_Step3_AnakTimbangan", rightTarget, finalActive && showRight);
        SetEditableGuideArrow(ref leftPowderStepArrow, "ARW_Step3_Bubuk", leftTarget, finalActive && showLeft);
    }

    private void SetStep4ArrowsActive(bool active)
    {
        bool finalActive = active && useStepArrowPointer;

        Transform sourceTarget = leftPanTarget != null
            ? leftPanTarget
            : (leftWeighingZone != null ? leftWeighingZone.transform : timbanganTarget);

        Transform targetMortar = mortarTarget != null
            ? mortarTarget
            : (mortarController != null ? mortarController.transform : null);

        SetEditableGuideArrow(ref leftPowderStepArrow, "ARW_Step3_Bubuk", sourceTarget, finalActive);
        SetGuideArrow(ref mortarStepArrow, "ARW_Step4_Mortar", targetMortar, Step04MortarArrowLabel, new Vector3(0f, 0.45f, 0f), finalActive);
    }

    private void Update()
    {
        if (isAnimating)
            return;

        if (currentStep == SyrupStep.Step_01_MeasureWater100ml)
            CheckStep01MeasureWater100ml();
        else if (currentStep == SyrupStep.Step_02_PlaceParchmentOnScale)
            CheckStep02PlaceParchmentOnScale();
        else if (currentStep == SyrupStep.Step_03_WeighPowder)
            CheckStep03WeighPowder();
        else if (currentStep == SyrupStep.Step_04_MovePowderToMortar)
            CheckStep04MovePowderToMortar();
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
        ApplyStep3RecipeSettings();

        if (powderDepositZone != null)
            powderDepositZone.SetAcceptingDeposits(true);

        if (spoonPowderPlateTransfer != null)
            spoonPowderPlateTransfer.SetTransferEnabled(false);

        currentStep = SyrupStep.Step_03_WeighPowder;
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

        CheckStep03WeighPowder();
    }

    private void ApplyStep3RecipeSettings()
    {
        if (powderDepositZone == null)
            return;

        powderDepositZone.ConfigureForRecipe(
            Step3ScoopStepMg,
            Step3TargetPowderMg * 2f,
            Step3PowderVisualMaxMg
        );
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

        yield return new WaitForSeconds(0.15f);

        currentStep = SyrupStep.Step_02_PlaceParchmentOnScale;
        stepDone = false;
        stableTimer = 0f;

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
            SetProcedureOutlineActive(perkamenStackOutline, true);
            SetProcedureOutlineActive(timbanganOutline, true);
        }

        SetStep2ArrowsActive(finalActive);
    }

    private void CheckStep02PlaceParchmentOnScale()
    {
        if (stepDone)
            return;

        ResolveStep3References();

        if (leftWeighingZone == null || rightWeighingZone == null)
        {
            if (progressText != null)
                progressText.text = "Zona timbang kiri/kanan belum tersambung di scene.";
            return;
        }

        bool leftSnapped = leftWeighingZone.HasParchment;
        bool rightSnapped = rightWeighingZone.HasParchment;
        bool usingTwoDifferentParchments = leftWeighingZone.ParchmentObject != null &&
                                           rightWeighingZone.ParchmentObject != null &&
                                           leftWeighingZone.ParchmentObject != rightWeighingZone.ParchmentObject;

        if (progressText != null)
        {
            string leftStatus = leftSnapped ? "OK" : "belum";
            string rightStatus = rightSnapped ? "OK" : "belum";
            string extra = leftSnapped && rightSnapped && !usingTwoDifferentParchments
                ? " Gunakan dua lembar perkamen yang berbeda."
                : string.Empty;
            progressText.text = $"Perkamen kiri: {leftStatus} | kanan: {rightStatus}. Lepaskan kertas di kedua piring neraca sampai tersnap.{extra}";
        }

        if (leftSnapped && rightSnapped && usingTwoDifferentParchments)
            CompleteStep02();
    }

    private void CompleteStep02()
    {
        if (stepDone)
            return;

        stepDone = true;

        if (progressText != null)
            progressText.text = "Step 2 selesai. Perkamen kiri dan kanan sudah tersnap.";

        if (doneIcon != null)
            doneIcon.SetActive(true);

        ClearProcedureOutlines();
        StartCoroutine(ShowStep03AfterStep02());

        Debug.Log("[SyrupProcedure] Step 2 complete.");
    }

    private void CheckStep03WeighPowder()
    {
        ResolveStep3References();

        float rightMassG = GetStep3RightMass();
        float leftMassG = GetStep3LeftMass();

        float rightMg = rightMassG * 1000f;
        float leftMg = leftMassG * 1000f;

        float targetMg = Step3TargetPowderMg;
        float toleranceMg = Step3ToleranceMg;

        bool rightReady = Mathf.Abs(rightMg - targetMg) <= toleranceMg;
        bool powderReady = Mathf.Abs(leftMg - targetMg) <= toleranceMg;
        bool balanced = Mathf.Abs(rightMg - leftMg) <= toleranceMg;

        if (progressText != null)
        {
            string rightStatus = rightReady
                ? $"{rightMg:0} mg OK"
                : rightMg <= 0.1f
                    ? "belum"
                    : $"{rightMg:0} / {targetMg:0} mg";

            string leftStatus = powderReady
                ? $"{leftMg:0} mg OK"
                : leftMg <= 0.1f
                    ? "belum"
                    : $"{leftMg:0} / {targetMg:0} mg";

            string nextAction;

            if (!rightReady)
            {
                if (rightMg > targetMg + toleranceMg)
                    nextAction = "Anak timbangan terlalu banyak. Tekan Reset, lalu ulangi.";
                else
                    nextAction = $"Taruh anak timbangan total {targetMg:0} mg di piring kanan.";
            }
            else if (!powderReady)
            {
                if (leftMg > targetMg + toleranceMg)
                    nextAction = "Bubuk terlalu banyak. Tekan Reset, lalu ulangi penimbangan.";
                else
                    nextAction = $"Ambil bubuk {Step3PowderName} dengan sendok tanduk, lalu tuang ke piring kiri per {Step3ScoopStepMg:0} mg.";
            }
            else if (!balanced)
            {
                nextAction = "Keduanya sudah masuk. Sesuaikan sampai neraca seimbang.";
            }
            else
            {
                nextAction = $"Step 3 selesai. {Step3PowderName} sudah mencapai {targetMg:0} mg.";
            }

            progressText.text = $"Kanan: {rightStatus} | Kiri: {leftStatus}\n{nextAction}";
        }

        SetStep3ArrowsActive(true, !rightReady, !powderReady);

        if (doneIcon != null)
            doneIcon.SetActive(rightReady && powderReady && balanced);

        if (rightReady && powderReady && balanced)
        {
            stableTimer += Time.deltaTime;

            if (stableTimer >= stableRequiredTime)
                CompleteStep03();
        }
        else
        {
            stableTimer = 0f;
        }
    }

    private void CompleteStep03()
    {
        if (stepDone)
            return;

        stepDone = true;

        if (progressText != null)
            progressText.text = $"Step 3 selesai. Bubuk {Step3PowderName} {Step3TargetPowderMg:0} mg sudah ditimbang.";

        if (doneIcon != null)
            doneIcon.SetActive(true);

        SetStep3ArrowsActive(false);
        ClearProcedureOutlines();

        StartCoroutine(ShowStep04AfterStep03());

        Debug.Log("[SyrupProcedure] Step 3 complete.");
    }

    private IEnumerator ShowStep04AfterStep03()
    {
        isAnimating = true;

        yield return new WaitForSeconds(0.5f);

        ShowDefaultStep04();

        isAnimating = false;
    }

    public void ShowDefaultStep04()
    {
        ClearProcedureOutlines();
        ResolveSceneReferences();
        ResolveStep4References();

        if (powderDepositZone != null)
            powderDepositZone.SetAcceptingDeposits(false);

        if (spoonPowderPlateTransfer != null)
            spoonPowderPlateTransfer.SetTransferEnabled(true);

        currentStep = SyrupStep.Step_04_MovePowderToMortar;
        stepDone = false;
        stableTimer = 0f;

        if (forceUILayout)
            ApplyUILayout();

        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(true);
            instructionText.text = Step04Instruction;
        }

        if (progressText != null)
        {
            progressText.gameObject.SetActive(true);
            progressText.text = Step04Progress;
        }

        if (doneIcon != null)
            doneIcon.SetActive(false);

        SetProcedureOutlineActive(mortarOutline, true);
        SetStep4ArrowsActive(true);

        Debug.Log("[SyrupProcedure] Step 4 started.");
    }

    private void CheckStep04MovePowderToMortar()
    {
        if (stepDone)
            return;

        ResolveStep4References();

        if (mortarController == null)
        {
            if (progressText != null)
                progressText.text = "Mortar belum tersambung ke Step 4.";
            return;
        }

        float mortarMg = GetMortarPowderMg();
        float targetMg = Step3TargetPowderMg;
        float toleranceMg = Step3ToleranceMg;

        bool powderMoved = Mathf.Abs(mortarMg - targetMg) <= toleranceMg;

        if (progressText != null)
        {
            if (mortarMg <= 0.1f)
            {
                progressText.text = $"Mortar: belum ada bubuk.\nAmbil bubuk {Step3PowderName} dari piring kiri, lalu tuang ke mortar.";
            }
            else if (mortarMg < targetMg - toleranceMg)
            {
                progressText.text = $"Mortar: {mortarMg:0} / {targetMg:0} mg.\nMasukkan semua bubuk yang sudah ditimbang ke mortar.";
            }
            else if (mortarMg > targetMg + toleranceMg)
            {
                progressText.text = $"Mortar: {mortarMg:0} mg. Terlalu banyak.\nTekan Reset, lalu ulangi bagian penimbangan.";
            }
            else
            {
                progressText.text = $"Step 4 selesai. Bubuk {Step3PowderName} {targetMg:0} mg sudah masuk ke mortar.";
            }
        }

        SetStep4ArrowsActive(!powderMoved);

        if (doneIcon != null)
            doneIcon.SetActive(powderMoved);

        if (powderMoved)
        {
            stableTimer += Time.deltaTime;

            if (stableTimer >= step4StableRequiredTime)
                CompleteStep04();
        }
        else
        {
            stableTimer = 0f;
        }
    }

    private void CompleteStep04()
    {
        if (stepDone)
            return;

        stepDone = true;
        currentStep = SyrupStep.Done;

        if (progressText != null)
            progressText.text = $"Step 4 selesai. Bubuk {Step3PowderName} sudah dipindahkan ke mortar.";

        if (doneIcon != null)
            doneIcon.SetActive(true);

        if (spoonPowderPlateTransfer != null)
            spoonPowderPlateTransfer.SetTransferEnabled(false);

        SetStep4ArrowsActive(false);
        ClearProcedureOutlines();

        Debug.Log("[SyrupProcedure] Step 4 complete.");
    }

    private IEnumerator ShowStep03AfterStep02()
    {
        isAnimating = true;
        yield return new WaitForSeconds(0.35f);
        ShowDefaultStep03();
    }

    private float GetStep3RightMass()
    {
        return rightWeighingZone != null ? rightWeighingZone.TotalGrams : 0f;
    }

    private float GetStep3LeftMass()
    {
        if (powderDepositZone != null)
            return powderDepositZone.DepositedGrams;

        return leftWeighingZone != null ? leftWeighingZone.TotalGrams : 0f;
    }

    private float GetMortarPowderMg()
    {
        if (mortarController == null)
            return 0f;

        if (TryReadCurrentMg(mortarController, out float currentMg))
            return currentMg;

        return 0f;
    }

    private bool TryReadCurrentMg(object target, out float value)
    {
        value = 0f;

        if (target == null)
            return false;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        string[] names =
        {
        "CurrentAmountMg",
        "currentAmountMg",
        "CurrentMg",
        "currentMg",
        "AmountMg",
        "amountMg",
        "powderMg",
        "currentPowderMg"
    };

        System.Type type = target.GetType();

        foreach (string name in names)
        {
            FieldInfo field = type.GetField(name, flags);
            if (field != null)
                return ConvertToFloat(field.GetValue(target), out value);

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.CanRead)
                return ConvertToFloat(property.GetValue(target), out value);
        }

        return false;
    }

    private static string FormatMass(float grams)
    {
        if (grams < 1f)
            return $"{grams * 1000f:0.###} mg";

        return $"{grams:0.###} g";
    }

    private void ResolveSceneReferences()
    {
        if (instructionText == null)
            instructionText = FindSceneComponentByName<TMP_Text>("TXT_SyrupInstruction");

        if (progressText == null)
            progressText = FindSceneComponentByName<TMP_Text>("TXT_SyrupProgress");

        if (doneIcon == null)
            doneIcon = FindSceneObjectByName("IMG_SyrupDoneIcon");

        if (stepCanvasRoot == null && instructionText != null && instructionText.canvas != null)
            stepCanvasRoot = instructionText.canvas.transform as RectTransform;

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

        if (rightWeightStepArrow == null)
            rightWeightStepArrow = FindSceneComponentByName<WorldStepArrow>("ARW_Step3_AnakTimbangan");

        if (leftPowderStepArrow == null)
            leftPowderStepArrow = FindSceneComponentByName<WorldStepArrow>("ARW_Step3_Bubuk");

        ResolveStep3References();
        ResolveStep4References();
    }

    private void ResolveStep3References()
    {
        if (leftWeighingZone == null)
            leftWeighingZone = FindSceneComponentByName<WeightingZone>("Collider_Piring_Kiri");

        if (leftWeighingZone == null)
            leftWeighingZone = FindSceneComponentByName<WeightingZone>("LeftWeighingZone");

        if (rightWeighingZone == null)
            rightWeighingZone = FindSceneComponentByName<WeightingZone>("Collider_Piring_Kanan");

        if (rightWeighingZone == null)
            rightWeighingZone = FindSceneComponentByName<WeightingZone>("RightWeighingZone");

        if (powderDepositZone == null && leftWeighingZone != null)
            powderDepositZone = leftWeighingZone.GetComponent<PowderDepositZone>();

        if (leftPanTarget == null)
        {
            GameObject leftPanObject = FindSceneObjectByName("Balance_WeightLeft");
            if (leftPanObject != null)
                leftPanTarget = leftPanObject.transform;
        }

        if (rightPanTarget == null)
        {
            GameObject rightPanObject = FindSceneObjectByName("Balance_WeightRight");
            if (rightPanObject != null)
                rightPanTarget = rightPanObject.transform;
        }
    }

    private void ResolveStep4References()
    {
        if (mortarController == null)
            mortarController = FindSceneComponentByName<MortarController>("Mortar");

        if (mortarController == null)
            mortarController = FindSceneComponentByName<MortarController>("mortar");

        if (mortarTarget == null && mortarController != null)
            mortarTarget = mortarController.transform;

        if (mortarOutline == null && mortarController != null)
            mortarOutline = mortarController.GetComponent<Outlinable>();

        if (mortarStepArrow == null)
            mortarStepArrow = FindSceneComponentByName<WorldStepArrow>("ARW_Step4_Mortar");

        if (spoonPowderPlateTransfer == null)
            spoonPowderPlateTransfer = FindSceneComponentByName<SpoonPowderPlateTransfer>("sendokTanduk");
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

    private void SetEditableGuideArrow(ref WorldStepArrow arrow, string objectName, Transform target, bool active)
    {
        if (arrow == null)
            arrow = FindSceneComponentByName<WorldStepArrow>(objectName);

        if (arrow == null)
        {
            if (active)
                Debug.LogWarning($"[SyrupProcedure] Arrow '{objectName}' belum ada di scene. Buat object WorldStepArrow agar teks dan offset bisa diedit dari hierarchy.", this);

            return;
        }

        if (!active || target == null)
        {
            arrow.SetVisible(false);
            return;
        }

        arrow.SetTarget(target);
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
        ConfigureTopText(progressText, ProgressPosition, ProgressSize, 24f, FontStyles.Normal);
        ConfigureRect(doneIcon != null ? doneIcon.transform as RectTransform : null, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), DoneIconPosition, DoneIconSize);

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
