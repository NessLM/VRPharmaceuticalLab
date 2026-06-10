using System.Collections;
using System.Reflection;
using UnityEngine;
using TMPro;

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
    [SerializeField] private float strikeLineTargetWidth = 650f;
    [SerializeField] private float strikeAnimationDuration = 0.35f;

    [Header("Highlights")]
    [SerializeField] private GameObject highlightGelasUkur100ml;
    [SerializeField] private GameObject highlightWasher;

    private float stableTimer;
    private bool stepDone;
    private bool isAnimating;

    private void OnEnable()
    {
        BeginSyrupProcedure();
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
        currentStep = SyrupStep.Step_01_MeasureWater100ml;
        stepDone = false;
        stableTimer = 0f;
        isAnimating = false;

        if (instructionText != null)
            instructionText.text = "Step 1: Isi Gelas Ukur dengan aquadest sampai 100 ml dari Washer.";

        if (progressText != null)
            progressText.text = "Air: 0 / 100 ml";

        if (doneIcon != null)
            doneIcon.SetActive(false);

        if (highlightGelasUkur100ml != null)
            highlightGelasUkur100ml.SetActive(true);

        if (highlightWasher != null)
            highlightWasher.SetActive(true);

        SetupChecklist();

        Debug.Log("[SyrupProcedure] Step 1 started.");
    }

    private void SetupChecklist()
    {
        if (checklistStep1Text != null)
        {
            checklistStep1Text.gameObject.SetActive(true);
            checklistStep1Text.text = "- Step 1: Isi aquadest 100 ml ke Gelas Ukur.";
            checklistStep1Text.fontStyle = FontStyles.Normal;
        }

        if (checklistStep2Text != null)
        {
            checklistStep2Text.gameObject.SetActive(false);
            checklistStep2Text.text = "- Step 2: Lanjutkan ke tahap berikutnya.";
            checklistStep2Text.fontStyle = FontStyles.Normal;
        }

        if (strikeStep1Line != null)
        {
            strikeStep1Line.gameObject.SetActive(false);

            Vector2 size = strikeStep1Line.sizeDelta;
            size.x = 0f;
            strikeStep1Line.sizeDelta = size;
        }
    }

    private void CheckStep01MeasureWater100ml()
    {
        if (stepDone)
            return;

        if (gelasUkurContainer == null)
        {
            if (progressText != null)
                progressText.text = "Gelas Ukur belum dihubungkan.";
            return;
        }

        if (!TryReadCurrentMl(gelasUkurContainer, out float currentVolume))
        {
            if (progressText != null)
                progressText.text = "Current Ml belum terbaca.";
            return;
        }

        if (progressText != null)
            progressText.text = $"Air: {currentVolume:0.0} / {targetWaterMl:0} ml";

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

        if (instructionText != null)
            instructionText.text = "Step 1 selesai: aquadest 100 ml sudah siap di Gelas Ukur.";

        if (progressText != null)
            progressText.text = "Step 1 selesai.";

        if (doneIcon != null)
            doneIcon.SetActive(true);

        if (highlightGelasUkur100ml != null)
            highlightGelasUkur100ml.SetActive(false);

        if (highlightWasher != null)
            highlightWasher.SetActive(false);

        StartCoroutine(AnimateStep1CompleteThenShowStep2());

        Debug.Log("[SyrupProcedure] Step 1 complete.");
    }

    private IEnumerator AnimateStep1CompleteThenShowStep2()
    {
        isAnimating = true;

        if (strikeStep1Line != null)
        {
            strikeStep1Line.gameObject.SetActive(true);

            float timer = 0f;

            while (timer < strikeAnimationDuration)
            {
                timer += Time.deltaTime;

                float t = Mathf.Clamp01(timer / strikeAnimationDuration);
                float width = Mathf.Lerp(0f, strikeLineTargetWidth, t);

                Vector2 size = strikeStep1Line.sizeDelta;
                size.x = width;
                strikeStep1Line.sizeDelta = size;

                yield return null;
            }

            Vector2 finalSize = strikeStep1Line.sizeDelta;
            finalSize.x = strikeLineTargetWidth;
            strikeStep1Line.sizeDelta = finalSize;
        }

        if (checklistStep1Text != null)
            checklistStep1Text.fontStyle = FontStyles.Strikethrough;

        yield return new WaitForSeconds(0.25f);

        if (checklistStep2Text != null)
            checklistStep2Text.gameObject.SetActive(true);

        currentStep = SyrupStep.Step_02_Placeholder;

        if (instructionText != null)
            instructionText.text = "Step 2: Lanjutkan ke tahap berikutnya.";

        if (progressText != null)
            progressText.text = "Menunggu aksi Step 2.";

        if (doneIcon != null)
            doneIcon.SetActive(false);

        isAnimating = false;
    }

    private bool TryReadCurrentMl(LiquidContainer container, out float value)
    {
        value = 0f;

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