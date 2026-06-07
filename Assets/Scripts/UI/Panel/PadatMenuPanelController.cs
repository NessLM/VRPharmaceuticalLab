using UnityEngine;
using UnityEngine.SceneManagement;

public class PadatMenuPanelController : MonoBehaviour
{
    [Header("Main Panels")]
    [SerializeField] private GameObject panelPilihJenisSediaan;
    [SerializeField] private GameObject panelMenuPadat;

    [Header("Start State")]
    [SerializeField] private bool showPadatMenuOnStart = true;

    private void Start()
    {
        if (showPadatMenuOnStart)
        {
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
        SceneManager.LoadScene("VRLabSimulation");
    }

    public void LoadPadatScene()
    {
        SceneManager.LoadScene("VRLabSimulation_Padat");
    }
}