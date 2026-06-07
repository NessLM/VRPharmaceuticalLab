using System.Reflection;
using UnityEngine;
using TMPro;

public class SyrupProcedureManager : MonoBehaviour
{
    private enum SyrupStep
    {
        Step_01_MeasureWater100ml,
        Done
    }

    [Header("Current Step")]
    [SerializeField] private SyrupStep currentStep;

    [Header("Step 01 - Measure Water 100 ml")]
    [SerializeField] private LiquidContainer gelasUkurContainer;
    [SerializeField] private float targetWaterMl = 100f;
    [SerializeField] private float toleranceMl = 2f;
    [SerializeField] private float stableRequiredTime = 0.5f;

    [Header("UI")]
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private GameObject doneIcon;

    [Header("Highlights")]
    [SerializeField] private GameObject highlightGelasUkur100ml;
    [SerializeField] private GameObject highlightWasher;

    private float stableTimer;
    private bool stepDone;

    private void OnEnable()
    {
        BeginSyrupProcedure();
    }

    private void Update()
    {
        if (currentStep == SyrupStep.Step_01_MeasureWater100ml)
            CheckStep01MeasureWater100ml();
    }

    public void BeginSyrupProcedure()
    {
        currentStep = SyrupStep.Step_01_MeasureWater100ml;
        stepDone = false;
        stableTimer = 0f;

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

        Debug.Log("[SyrupProcedure] Step 1 started.");
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
        stepDone = true;
        currentStep = SyrupStep.Done;

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

        Debug.Log("[SyrupProcedure] Step 1 complete.");
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