using UnityEngine;
using UnityEngine.SceneManagement;

public class PadatMenuPanelController : MonoBehaviour
{
    [Header("Main Panels")]
    [SerializeField] private GameObject panelPilihJenisSediaan;

    [Tooltip("Isi dengan PanelMenu_CairSemiPadat di scene VRLabSimulation, dan PanelMenu_Padat di scene VRLabSimulation_Padat.")]
    [SerializeField] private GameObject panelMenuPadat;

    private static string sceneYangHarusLangsungBukaMenu = "";

    private void Start()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (sceneYangHarusLangsungBukaMenu == currentScene)
        {
            sceneYangHarusLangsungBukaMenu = "";
            ShowPanelMenuPadat();
        }
        else
        {
            ShowPanelPilihJenisSediaan();
        }
    }

    public void ShowPanelMenuPadat()
    {
        if (panelPilihJenisSediaan != null)
            panelPilihJenisSediaan.SetActive(false);

        if (panelMenuPadat != null)
            panelMenuPadat.SetActive(true);
    }

    public void ShowPanelPilihJenisSediaan()
    {
        if (panelMenuPadat != null)
            panelMenuPadat.SetActive(false);

        if (panelPilihJenisSediaan != null)
            panelPilihJenisSediaan.SetActive(true);
    }

    public void LoadCairSemiPadatScene()
    {
        sceneYangHarusLangsungBukaMenu = "VRLabSimulation";
        SceneManager.LoadScene("VRLabSimulation");
    }

    public void LoadPadatScene()
    {
        sceneYangHarusLangsungBukaMenu = "VRLabSimulation_Padat";
        SceneManager.LoadScene("VRLabSimulation_Padat");
    }
}