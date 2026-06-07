using System.Collections;
using UnityEngine;
using TMPro;

public class SyrupIntroPanelController : MonoBehaviour
{
    [Header("Recipe Window")]
    [SerializeField] private GameObject recipeWindow;

    [Header("Buttons")]
    [SerializeField] private GameObject buttonStartSyrup;
    [SerializeField] private GameObject buttonInfoToggle;
    [SerializeField] private TMP_Text infoToggleText;

    [Header("Next System")]
    [SerializeField] private GameObject syrupStepManager;
    [SerializeField] private GameObject syrupStepUI;

    [Header("Safety")]
    [SerializeField] private float startButtonAppearDelay = 1.0f;

    private bool hasStarted;
    private bool recipeVisible;
    private bool canStart;

    private void OnEnable()
    {
        ResetIntro();
    }

    private void ResetIntro()
    {
        StopAllCoroutines();

        hasStarted = false;
        recipeVisible = true;
        canStart = false;

        if (recipeWindow != null)
            recipeWindow.SetActive(true);

        // Ini penting: tombol Start dimatikan dulu, jangan langsung muncul.
        if (buttonStartSyrup != null)
            buttonStartSyrup.SetActive(false);

        if (buttonInfoToggle != null)
            buttonInfoToggle.SetActive(false);

        if (syrupStepManager != null)
            syrupStepManager.SetActive(false);

        if (syrupStepUI != null)
            syrupStepUI.SetActive(false);

        StartCoroutine(ShowStartButtonAfterDelay());

        Debug.Log("[SyrupIntro] Intro opened. Start button locked.");
    }

    private IEnumerator ShowStartButtonAfterDelay()
    {
        yield return new WaitForSeconds(startButtonAppearDelay);

        canStart = true;

        if (buttonStartSyrup != null)
            buttonStartSyrup.SetActive(true);

        Debug.Log("[SyrupIntro] Start button unlocked.");
    }

    public void StartSyrup()
    {
        if (!canStart)
        {
            Debug.Log("[SyrupIntro] Start blocked. Button not ready yet.");
            return;
        }

        hasStarted = true;
        recipeVisible = false;
        canStart = false;

        if (recipeWindow != null)
            recipeWindow.SetActive(false);

        if (buttonStartSyrup != null)
            buttonStartSyrup.SetActive(false);

        if (buttonInfoToggle != null)
            buttonInfoToggle.SetActive(true);

        if (infoToggleText != null)
            infoToggleText.text = "!";

        if (syrupStepUI != null)
            syrupStepUI.SetActive(true);

        if (syrupStepManager != null)
            syrupStepManager.SetActive(true);

        Debug.Log("[SyrupIntro] START ACCEPTED. Syrup simulation started.");
    }

    public void ToggleInformation()
    {
        if (!hasStarted)
            return;

        recipeVisible = !recipeVisible;

        if (recipeWindow != null)
            recipeWindow.SetActive(recipeVisible);

        if (buttonStartSyrup != null)
            buttonStartSyrup.SetActive(false);

        if (infoToggleText != null)
            infoToggleText.text = recipeVisible ? "X" : "!";

        Debug.Log("[SyrupIntro] Toggle recipe: " + recipeVisible);
    }
}