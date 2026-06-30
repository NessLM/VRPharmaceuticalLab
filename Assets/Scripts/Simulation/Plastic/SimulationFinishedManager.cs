using UnityEngine;
using UnityEngine.SceneManagement;

public class SimulationFinishedManager : MonoBehaviour
{
    [Header("Nama Scene Main Menu")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // Tombol Ulangi Simulasi
    public void RepeatSimulation()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Tombol Kembali ke Menu
    public void BackToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}