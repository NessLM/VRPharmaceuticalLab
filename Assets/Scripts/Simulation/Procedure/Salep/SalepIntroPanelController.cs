using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class SalepIntroPanelController : MonoBehaviour
{
    [Header("Recipe Window")]
    [SerializeField] private GameObject recipeWindow;

    [Header("Buttons")]
    [SerializeField] private GameObject buttonStartSalep;
    [SerializeField] private GameObject buttonInfoToggle;
    [SerializeField] private TMP_Text infoToggleText;

    [Header("Next System")]
    [SerializeField] private GameObject salepStepManager;
    [SerializeField] private GameObject salepStepUI;
    [FormerlySerializedAs("salepObjectsRoot")]
    [SerializeField] private GameObject salepIngredientsRoot;

    [Header("Reset")]
    [SerializeField] private SimulationResetManager resetManager;

    [Header("Safety")]
    [SerializeField] private float startButtonAppearDelay = 1f;

    private bool hasStarted;
    private bool recipeVisible;
    private bool canStart;

    private void OnEnable()
    {
        ResetIntro();
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        if (salepIngredientsRoot != null)
            salepIngredientsRoot.SetActive(false);
    }

    public void ResetIntro()
    {
        StopAllCoroutines();
        hasStarted = false;
        recipeVisible = true;
        canStart = false;

        if (recipeWindow != null)
            recipeWindow.SetActive(true);

        if (buttonStartSalep != null)
            buttonStartSalep.SetActive(false);

        if (buttonInfoToggle != null)
            buttonInfoToggle.SetActive(false);

        if (salepStepManager != null)
            salepStepManager.SetActive(false);

        if (salepStepUI != null)
            salepStepUI.SetActive(false);

        if (salepIngredientsRoot != null)
            salepIngredientsRoot.SetActive(true);

        StartCoroutine(ShowStartButtonAfterDelay());
    }

    public void ReturnToInitialState()
    {
        ResetIntro();
    }

    public void StartSalep()
    {
        if (!canStart)
            return;

        hasStarted = true;
        recipeVisible = false;
        canStart = false;

        if (recipeWindow != null)
            recipeWindow.SetActive(false);

        if (buttonStartSalep != null)
            buttonStartSalep.SetActive(false);

        if (buttonInfoToggle != null)
            buttonInfoToggle.SetActive(true);

        if (infoToggleText != null)
            infoToggleText.text = "!";

        if (salepStepUI != null)
            salepStepUI.SetActive(true);

        if (salepStepManager != null)
        {
            salepStepManager.SetActive(true);
            SalepProcedureManager manager = salepStepManager.GetComponent<SalepProcedureManager>();
            if (manager != null)
                manager.BeginSalepProcedure();
        }
    }

    public void ToggleInformation()
    {
        if (!hasStarted)
            return;

        recipeVisible = !recipeVisible;

        if (recipeWindow != null)
            recipeWindow.SetActive(recipeVisible);

        if (buttonStartSalep != null)
            buttonStartSalep.SetActive(false);

        if (infoToggleText != null)
            infoToggleText.text = recipeVisible ? "X" : "!";
    }

    public void BackToMainMenu()
    {
        if (resetManager != null)
            resetManager.ResetAllAndReturnToMainMenu();
    }

    private IEnumerator ShowStartButtonAfterDelay()
    {
        yield return new WaitForSeconds(startButtonAppearDelay);
        canStart = true;

        if (buttonStartSalep != null)
            buttonStartSalep.SetActive(true);
    }
}
