using UnityEngine;
using UnityEngine.SceneManagement;

public class PanelSimulation : MonoBehaviour
{
    [Header("Panel Utama")]
    [SerializeField] private GameObject panelPilihJenisSediaan;

    [Header("Menu Setelah Pilih Jenis")]
    [SerializeField] private GameObject panelMenuCairSemiPadat;
    [SerializeField] private GameObject panelMenuPadat;

    [Header("Panel Pilihan Simulasi")]
    [SerializeField] private GameObject panelSimulasiCairSemiPadat;
    [SerializeField] private GameObject panelSimulasiPadat;

    [Header("Nama Scene Simulasi - Optional")]
    [SerializeField] private string sceneSirup;
    [SerializeField] private string sceneSalep;
    [SerializeField] private string scenePuyer;
    [SerializeField] private string sceneKapsul;

    private void Start()
    {
        ShowPanelPilihJenisSediaan();
    }

    private void HideAllPanels()
    {
        if (panelPilihJenisSediaan != null)
            panelPilihJenisSediaan.SetActive(false);

        if (panelMenuCairSemiPadat != null)
            panelMenuCairSemiPadat.SetActive(false);

        if (panelMenuPadat != null)
            panelMenuPadat.SetActive(false);

        if (panelSimulasiCairSemiPadat != null)
            panelSimulasiCairSemiPadat.SetActive(false);

        if (panelSimulasiPadat != null)
            panelSimulasiPadat.SetActive(false);
    }

    public void ShowPanelPilihJenisSediaan()
    {
        HideAllPanels();

        if (panelPilihJenisSediaan != null)
            panelPilihJenisSediaan.SetActive(true);
    }

    public void PilihCairSemiPadat()
    {
        HideAllPanels();

        if (panelMenuCairSemiPadat != null)
            panelMenuCairSemiPadat.SetActive(true);
    }

    public void PilihPadat()
    {
        HideAllPanels();

        if (panelMenuPadat != null)
            panelMenuPadat.SetActive(true);
    }

    public void BukaSimulasiCairSemiPadat()
    {
        HideAllPanels();

        if (panelSimulasiCairSemiPadat != null)
            panelSimulasiCairSemiPadat.SetActive(true);
    }

    public void BukaSimulasiPadat()
    {
        HideAllPanels();

        if (panelSimulasiPadat != null)
            panelSimulasiPadat.SetActive(true);
    }

    public void BackToMenuCairSemiPadat()
    {
        HideAllPanels();

        if (panelMenuCairSemiPadat != null)
            panelMenuCairSemiPadat.SetActive(true);
    }

    public void BackToMenuPadat()
    {
        HideAllPanels();

        if (panelMenuPadat != null)
            panelMenuPadat.SetActive(true);
    }

    public void MulaiSirup()
    {
        LoadSimulationScene(sceneSirup, "Sirup");
    }

    public void MulaiSalep()
    {
        LoadSimulationScene(sceneSalep, "Salep");
    }

    public void MulaiPuyer()
    {
        LoadSimulationScene(scenePuyer, "Puyer");
    }

    public void MulaiKapsul()
    {
        LoadSimulationScene(sceneKapsul, "Kapsul");
    }

    private void LoadSimulationScene(string sceneName, string simulationName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.Log("Simulasi dipilih: " + simulationName + ". Scene belum diisi di Inspector.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}